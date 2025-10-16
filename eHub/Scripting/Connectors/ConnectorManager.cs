using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using eController.Util;
using eController.Util.Configuration;
using eController.Util.DependencyInjection;
using eController.Util.Logging;
using eHub.Config;
using eHub.Config.Providers;
using eHub.Contracts;
using eHub.Contracts.Manager;
using eHub.Database;
using eHub.PlugIn;
using eHub.PlugIn.Authorization;
using eHub.PlugIn.UI;
using eHub.Scripting.Connectors.Authorization;
using eHub.Scripting.Connectors.Configuration;
using eHub.Scripting.Connectors.Db;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors.Metrics;
using eHub.Scripting.Connectors.Services;
using ElementLogic.Configuration.Client;
using eMessenger;
using ePlugin.Engine.Client;
using eScheduler.Library.Abstractions;
using eScheduler.Library.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Polly;

namespace eHub.Scripting.Connectors;
using NamedConnectorTemplate = KeyValuePair<string, ConnectorTemplate>;

/// <summary>This component starts, stops and keeps track of running connectors.<br/>
/// Connectors declared in the config will be automatically managed.
/// This means each template will be started at launch and hot reloaded on file changes.
/// <para>
/// Additionally it will provide config objects and optionally config hot reload.<br/>
/// Use <see cref="IOptions{TOptions}"/>, <see cref="IOptionsSnapshot{TOptions}"/> or <see cref="IOptionsMonitor{TOptions}"/> with any custom object to map to.<br/>
/// You can refer to the <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-interfaces">Official ASP Documentation</see> for usage.
/// </para>
/// </summary>
public partial class ConnectorManager : IHostedService, ISchedulerServiceProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly IMessenger _messenger;
    private readonly MessagingContext _messagingContext;

    /// <summary>Loosely coupled services which can subscribe to status updates of connectors.</summary>
    private readonly ImmutableArray<IConnectorMessageHandler> _connectorMessageHandlers;
    /// <inheritdoc cref="IPluginPublisher"/>
    private readonly IPluginPublisher _pluginPublisher;
    /// <summary>The core service provider from this ASP server. Used to inject shared dependencies to connectors.</summary>
    private readonly IServiceProvider _services;
    private readonly IEffortlessConfigurationRegistry _registry;
    private readonly IMeterFactory _meterFactory;

    private readonly SchedulerApiConfigurationProvider _schedulerApiConfigProvider;
    private IRegistrationToken _regToken = NullRegistrationToken.Instance;

    public NamedConnectorTemplateCollection Templates { get; } = [];

    /// <summary>Additional data for each loaded plugin</summary>
    /// <remarks>
    /// <i>Key</i>: The <see cref="PluginData.Name"/> of the plugin.<br/>
    /// <i>Value</i>: The context data.<br/>
    /// </remarks>
    private readonly Dictionary<string, PluginContext> _loadedPlugins = [];

    /// <summary>All currently running connectors</summary>
    /// <remarks>
    /// <i>Key</i>: The connector name. For templates it's the template key. For connectors started by type an arbitrary text.<br/>
    /// <i>Value</i>: The running connector context<br/>
    /// </remarks>
    private Dictionary<string, ConnectorContext> Instances { get; } = [];

    [GeneratedRegex(@"^connector.*\.json$", RegexOptions.IgnoreCase)]
    private static partial Regex TemplateFileNameReg();

    public IEnumerable<NameConnectorTemplateDto> AllConnectorTemplates => Templates
        .Select(x => new NameConnectorTemplateDto(x.Key, x.Value.Type));
    public IEnumerable<ScriptInstanceDto> ActiveConnectors
        => from scriptCtx in _loadedPlugins.Values
           from scriptCtxInstance in Instances.Values
           select new ScriptInstanceDto(
               scriptCtxInstance.Metadata.TemplateName,
               scriptCtxInstance.Metadata.ConnectorType,
               scriptCtxInstance.ServiceProvider?.GetService<ConnectorStatusFeature>()?.ConnectorStatus,
               scriptCtxInstance.Plugin.Name);
    public IEnumerable<string> AvailableConnectorTypes => _pluginPublisher.Plugins
        .SelectMany(plugin => plugin.Assemblies)
        .SelectMany(x => x.GetTypes())
        .Where(x => x.IsAssignableTo(typeof(IConnector)))
        .Select(x => x.FullName!);

    public ConnectorManager(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IMessenger messenger,
        MessagingContext messagingContext,
        IPluginPublisher pluginPublisher,
        IServiceProvider services,
        IEnumerable<IConnectorMessageHandler> connectorMessageHandlers,
        IEffortlessConfigurationRegistry registry,
        SchedulerApiConfigurationProvider schedulerApiConfigurationProvider,
        IMeterFactory meterFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<ConnectorManager>();
        _messenger = messenger;
        _messagingContext = messagingContext;
        _pluginPublisher = pluginPublisher;
        _services = services;
        _registry = registry;
        _schedulerApiConfigProvider = schedulerApiConfigurationProvider;
        _meterFactory = meterFactory;
        _connectorMessageHandlers = [.. connectorMessageHandlers];

        pluginPublisher.SubscribeAndCatchUp(ScriptAssemblyProvider_ScriptAdded);
        pluginPublisher.AfterPluginRemoved += ScriptAssemblyProvider_ScriptRemoved;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var confDir = _registry.AppConfigFolder.SubDirectory("CustomViewConfigs");

        if (!confDir.Exists)
        {
            return;
        }

        foreach (var file in confDir.EnumerateFiles())
        {
            try
            {
                using var stream = new StreamReader(file.FullName);
                var customHistory = await JsonSerializer.DeserializeAsync<UIViewConfig>(stream.BaseStream, options: null, cancellationToken);

                if (customHistory is null)
                {
                    continue;
                }

                var customHistoryName = customHistory.Name ?? Path.GetFileNameWithoutExtension(file.FullName);
                var connectorIdentifier = new ConnectorIdentifier(_messagingContext.OwnId.ToString(), customHistoryName);

                _regToken += await _messenger.AnswerAsync(
                    ConnectorContract.PollAliveConnectorsTopic(),
                    () => new ConnectorKeepAliveDto(connectorIdentifier));

                _regToken += await _messenger.AnswerAsync(
                    ConnectorContract.UIGetTopic(connectorIdentifier),
                    () => new ConnectorUiData(customHistoryName, UIViewConfig.CustomUIViewType, customHistory));

                _messenger.Send(
                    ConnectorContract.ConnectorStartedTopic(),
                    new ConnectorStartedEventDto(connectorIdentifier, new(customHistoryName, UIViewConfig.CustomUIViewType, customHistory)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize custom UI config file {path}", file);
                continue;
            }
        }
    }
    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _regToken.DisposeAsync();
        await UnregisterAllUnits();
        await UnregisterSchedulerConfigurations();

        await Task.WhenAll(Instances.Values.Select(x => x.DisposeAsync().AsTask()));
    }

    /// <inheritdoc cref="TryCreateConnector(string, IConfiguration?)"/>
    public async ValueTask<ConnectorCreateResult> StartConnectorByTemplate(string templateName, IConfiguration? additionalConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(templateName);

        return await TryCreateConnector(templateName, additionalConfiguration);
    }

    /// <summary>Starts a new connector by searching for any loaded connector with the requested type. The name will be a random unique string.</summary>
    /// <param name="connectorType">The full type name (As given by <see cref="Type.FullName"/>).</param>
    /// <param name="additionalConfiguration"><inheritdoc cref="TryCreateConnector(NamedConnectorTemplate, IConfiguration)" path="/param[@name='additionalConfiguration']" /></param>
    /// <returns><inheritdoc cref="TryCreateConnector(NamedConnectorTemplate, IConfiguration)" path="/returns" /></returns>
    public async ValueTask<ConnectorCreateResult> StartConnectorByType(string connectorType, IConfiguration? additionalConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(connectorType);

        return await TryCreateConnector(
            new NamedConnectorTemplate(Guid.NewGuid().ToString(), new ConnectorTemplate() { Type = connectorType, }),
            additionalConfiguration);
    }

    /// <inheritdoc cref="StopConnector(string)"/>
    public async ValueTask StopConnectorByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        await StopConnector(name);
    }

    public bool TryGetSchedulerService(string connectorName, [NotNullWhen(true)] out ISchedulerService? schedulerService)
    {
        if (Instances.TryGetValue(connectorName, out var instanceCtx) && instanceCtx.ServiceProvider is { } serviceProvider)
        {
            schedulerService = serviceProvider.GetService<ISchedulerService>();
            return schedulerService is not null;
        }

        schedulerService = null;
        return false;
    }

    private void ScriptAssemblyProvider_ScriptAdded(PluginData plugin)
    {
        var scriptCtx = _loadedPlugins.GetOrAdd(plugin.Name, () => new PluginContext(plugin));
        scriptCtx.ConfigChanged += OnPluginContextChanged;

        OnPluginContextChanged();
    }

    private void ScriptAssemblyProvider_ScriptRemoved(PluginData plugin)
    {
        if (_loadedPlugins.Remove(plugin.Name, out var scriptCtx))
        {
            scriptCtx.Dispose();
        }

        var dropInstances = Instances.Where(x => x.Value.Plugin == plugin)
            .Select(x => x.Key)
            .ToList();

        foreach (var instanceName in dropInstances)
        {
            if (Instances.Remove(instanceName, out var instanceCtx))
            {
                instanceCtx.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        OnPluginContextChanged();
    }

    private void OnPluginContextChanged()
    {
        ReloadConnectorTemplatesConfig().Wait();
    }

    /// <inheritdoc cref="TryCreateConnector(NamedConnectorTemplate, IConfiguration?)"/>
    /// <param name="templateName">The named template to load.</param>
    /// <param name="additionalConfiguration"><inheritdoc cref="TryCreateConnector(NamedConnectorTemplate, IConfiguration)" path="/param[@name='additionalConfiguration']" /></param>
    private async ValueTask<ConnectorCreateResult> TryCreateConnector(string templateName, IConfiguration? additionalConfiguration = null)
    {
        if (!Templates.TryGetValue(templateName, out var template))
        {
            return ConnectorCreateResult.Err("Template not found");
        }

        return await TryCreateConnector(new NamedConnectorTemplate(templateName, template), additionalConfiguration);
    }

    /// <summary>Starts a connector defined by a template. If a connector with this name already runs this call does nothing.</summary>
    /// <param name="namedTemplate">The connector template to load.</param>
    /// <param name="additionalConfiguration">Additional configuration which will be applied last and may therefore overwrite any value with a new final value.</param>
    /// <returns>Summary whether the operation was successful, or error details if not.</returns>
    private async ValueTask<ConnectorCreateResult> TryCreateConnector(NamedConnectorTemplate namedTemplate, IConfiguration? additionalConfiguration = null)
    {
        if (Instances.ContainsKey(namedTemplate.Key))
        {
            return ConnectorCreateResult.Err($"Template '{namedTemplate.Key}' is already running");
        }

        if (!TryFindConnectorType(namedTemplate.Value.Type, out var plugin, out var connectorType, out var errorMessage))
        {
            return ConnectorCreateResult.Err(errorMessage);
        }

        var template = namedTemplate.Value;

        ConnectorMetadata metadata = new(_messagingContext, namedTemplate.Key, connectorType.FullName!);
        ConnectorContext? connectorCtx = new(_logger, metadata, plugin);

        try
        {
            using var _ = ScopeUtil.HideScope();
            ScopeUtil.BeginKvpScope(("Connector", metadata.TemplateName));

            // Add all configuration sources. They will be merged in the following order (later wins)
            // TODO, with the next global config rework:
            // x. config from <global>      ->      :*
            // x. connectors.json           ->      :Connectors:*
            // x. UIViewConfig.json         ->      :Connectors:<templateName>:UI:*
            // x. additionalConfiguration   ->      :Connectors:<templateName>:Config:*

            connectorCtx.ReloadConfig(template);

            var connectorServices = new ServiceCollection();
            connectorServices.AddSingleton(_loggerFactory);
            connectorServices.AddLogging(x => x.ClearProviders());
            {
                var servicesScope = _services.CreateAsyncScope();
                connectorCtx.OnDispose += async () => await servicesScope.DisposeAsync();
                connectorServices.AddSingleton(servicesScope.ServiceProvider.GetRequiredService<IScopedMessenger>());
            }
            connectorServices.AddSingleton(metadata);
            connectorServices.AddSingleton(connectorType);
            connectorServices.AddSingleton(typeof(IConnector), x => x.GetRequiredService(connectorType));
            connectorServices.AddConnectorFeature<ConnectorStatusFeature>();
            _connectorMessageHandlers.ForEach(x => connectorServices.AddSingleton(x));
            connectorServices.AddSingleton(_meterFactory);
            connectorServices.AddSingleton<ConnectorMetrics>();

            var hostConfigBuilder = new ConfigurationBuilder();
            hostConfigBuilder.AddConfiguration(_configuration);

            // Build the configuration for the connector
            {
                var connectorConfigBuilder = new ConfigurationBuilder();
                connectorConfigBuilder.Add(connectorCtx.OptionsSource);
                if (additionalConfiguration != null)
                {
                    connectorConfigBuilder.AddConfiguration(additionalConfiguration);
                }
                var connectorConfig = connectorConfigBuilder.Build();
                hostConfigBuilder.AddConfiguration(connectorConfig);

                var injectionInfo = InjectionInfo.OfType(connectorType);
                foreach (var injectionPart in injectionInfo.InitList.Where(x => x.Kind == InjectionPartKind.InjectAttribute))
                {
                    _logger.LogError("Injection with [Inject] is deprecated. Use constructor injection instead. Requested service: {Service}", injectionPart.InjectedType.FullName ?? "Unknown");
                }
                ScriptActivator.AddOptions(connectorServices, injectionInfo, connectorConfig);
            }

            // Register all API services
            AddApiServices(connectorServices, template, connectorCtx.Plugin);

            // Register all PacketTransfer related services
            if (TypeUtilities.IsPacketTransfer(connectorType))
            {
                var connectionString = DbUtil.BootstrapSqliteConnectionString(template.PacketTransfer?.DbPath, $"./Databases/{namedTemplate.Key}.db");
                hostConfigBuilder.AddInMemoryCollection([new KeyValuePair<string, string?>("ConnectionStrings:PacketTransfer", connectionString)]);
                connectorServices
                    .AddDbContextFactory<HubDbContext>((sp, opt) => opt.UseSqlite(connectionString)
                    .AddInterceptors(new SqlitePragmasInterceptor(template.PacketTransfer?.SqlitePragmas ?? [])));

                connectorServices.AddSingleton<PacketDtoService>();
                connectorServices.AddSingleton<IFilterRunnerProvider, FilterRunnerManager>();
                connectorServices.AddSingleton(typeof(IPacketTransfer), x => x.GetRequiredService(connectorType));
                connectorServices.AddSingleton<IPacketRepository, PacketRepository>();
                connectorServices.AddConnectorFeature<PacketTransferFeature>();
            }
            // Register all HttpConnector related services
            if (TypeUtilities.IsHttpConnector(connectorType))
            {
                connectorServices.AddSingleton(typeof(IHttpConnector), x => x.GetRequiredService(connectorType));
                connectorServices.AddConnectorFeature<HttpConnectorFeature>();
            }

            // Allow connector to add dependencies to the service collection.
            DependencyInjectionFeature.InvokeDependencySetup(connectorType, connectorServices,
                template.Config ?? NullConfiguration.Instance as IConfiguration);

            bool addScheduler = connectorCtx.Plugin.Assemblies
                .SelectMany(s => s.GetTypes())
                .Any(x => x.IsAssignableTo(typeof(IScheduledTask)));

            // Register all Scheduler related services
            if (addScheduler)
            {
                var schedulerConfigPath = GetSchedulerConfigPath(namedTemplate.Key, template);
                await AddSchedulerConfiguration(namedTemplate.Key, schedulerConfigPath);

                _schedulerApiConfigProvider.AddConnectorApi(namedTemplate.Key);
                connectorCtx.OnDispose += () => { _schedulerApiConfigProvider.RemoveConnectorApi(namedTemplate.Key); return Task.CompletedTask; };
                connectorServices.AddEScheduler(template.Scheduler);
            }

            connectorServices.AddSingleton(_registry);
            connectorServices.AddSingleton(plugin);
            connectorServices.AddSingleton(template);
            connectorServices.AddSingleton<IConfiguration>(hostConfigBuilder.Build());

            var connectorServiceProvider = connectorServices.BuildServiceProvider(new ServiceProviderOptions() { ValidateScopes = true, ValidateOnBuild = true });
            connectorCtx.OnDispose += async () => await connectorServiceProvider.DisposeAsync();
            connectorCtx.ServiceProvider = connectorServiceProvider;

            connectorCtx.WorkTask = TaskUtil.RunNonBlocking(async () =>
            {
                try
                {
                    await Task.WhenAll(connectorServiceProvider.GetServices<IConnectorFeature>()
                        .Select(x => x.StartAsync(connectorCtx.StopSource.Token)));

                    await Task.WhenAll(connectorServiceProvider.GetServices<IHostedService>()
                        .Select(x => x.StartAsync(connectorCtx.StopSource.Token)));

                    await connectorServiceProvider.GetRequiredService<IConnector>().Run(connectorCtx.StopSource.Token);
                }
                catch (OperationCanceledException) // Expected when Token gets cancelled.
                {
                    _logger.LogInformation("Connector {Connector} stopped by cancellation.", namedTemplate.Key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connector {Connector} crashed", namedTemplate.Key);
                    await connectorCtx.StopSource.CancelAsync();
                }
            });

            Instances.Add(metadata.TemplateName, connectorCtx);

            _logger.LogInformation("Created Connector");
            return ConnectorCreateResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connector {Template}", namedTemplate.Key);

            await connectorCtx.DisposeAsync();
            return ConnectorCreateResult.Err("Failed to create connector", ex);
        }
    }

    /// <summary>Stops the connector with the requested <paramref name="name"/>. Does nothing if no connector was found.</summary>
    /// <param name="name">The template name or unique string for the running connector</param>
    private async ValueTask StopConnector(string name)
    {
        if (Instances.Remove(name, out var instanceCtx))
        {
            await instanceCtx.DisposeAsync();
        }
    }

    private static ServiceCollection AddApiServices(ServiceCollection services, ConnectorTemplate connector, PluginData plugin)
    {
        if (connector.HttpOutgoing?.Api is null)
        {
            services.AddHttpClient();
            return services;
        }

        foreach (var api in connector.HttpOutgoing.Api)
        {
            AddAuthorizationService(api.Key, api.Value.Authorization);

            if (api.Key is null or "" or "default")
            {
                services.ConfigureHttpClientDefaults(ConfigureClient);
            }
            else
            {
                var httpBuilder = services.AddHttpClient(api.Key);
                ConfigureClient(httpBuilder);
            }

            continue;

            void AddAuthorizationService(string apiKey, AuthorizationConfig? authConfig)
            {
                if (authConfig is null)
                {
                    return;
                }

                switch (authConfig.Type)
                {
                    case AuthorizationType.Basic:
                        if (authConfig.Basic is null)
                        {
                            throw new InvalidOperationException("Missing basic authorization configurations.");
                        }

                        services.AddSingleton(Options.Create(authConfig.Basic));
                        services.AddKeyedSingleton<BasicAuthService>(apiKey);
                        services.AddKeyedSingleton<IAuthorizationService>(apiKey,
                            (provider, key) => provider.GetRequiredKeyedService<BasicAuthService>(key));
                        break;
                    case AuthorizationType.BearerToken:
                        if (authConfig.BearerToken is null)
                        {
                            throw new InvalidOperationException("Missing bearer token authorization configurations.");
                        }

                        services.AddSingleton(Options.Create(authConfig.BearerToken));
                        services.AddKeyedSingleton<BearerTokenAuthService>(apiKey);
                        services.AddKeyedSingleton<IAuthorizationService>(apiKey,
                            (provider, key) => provider.GetRequiredKeyedService<BearerTokenAuthService>(key));
                        break;
                    case AuthorizationType.OAuth2:
                        if (authConfig.OAuth2 is null)
                        {
                            throw new InvalidOperationException("Missing oauth2 authorization configurations.");
                        }

                        services.AddSingleton(Options.Create(authConfig.OAuth2));
                        services.AddKeyedSingleton<OAuth2Service>(apiKey);
                        services.AddKeyedSingleton<IAuthorizationService>(apiKey,
                            (provider, key) => provider.GetRequiredKeyedService<OAuth2Service>(key));
                        break;
                    case AuthorizationType.Custom:
                    {
                        var typeName = authConfig.Custom?.TypeName
                            ?? throw new InvalidOperationException("Missing custom authorization type name.");

                        var implType = plugin.Assemblies.GetTypeSmart(typeName)
                            ?? throw new InvalidOperationException("Missing custom authorization type class.");

                        if (!implType.IsAssignableTo(typeof(IAuthorizationService)))
                        {
                            throw new InvalidOperationException($"\"{implType.FullName}\" is not assignable to IAuthorizationService.");
                        }

                        services.AddKeyedSingleton(serviceType: implType, serviceKey: apiKey);
                        services.AddKeyedSingleton<IAuthorizationService>(apiKey,
                            (provider, key) => (IAuthorizationService)provider.GetRequiredKeyedService(implType, key));
                        break;
                    }
                    case AuthorizationType.None:
                    default: break;
                }
            }

            void ConfigureClient(IHttpClientBuilder httpBuilder)
            {
                httpBuilder.ConfigureHttpClient(httpClient =>
                {
                    if (!string.IsNullOrEmpty(api.Value.BaseAddress))
                    {
                        // Ensure BaseAddress ends with a '/'
                        var baseAddress = api.Value.BaseAddress;
                        if (!baseAddress.EndsWith('/'))
                        {
                            baseAddress += '/';
                        }

                        httpClient.BaseAddress = new Uri(baseAddress);
                    }

                    if (api.Value.Timeout != TimeSpan.Zero)
                    {
                        httpClient.Timeout = api.Value.Timeout;
                    }

                    foreach (var header in api.Value.Headers)
                    {
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                });

                ConfigureRetryPolicy(httpBuilder);
                ConfigureAuthorizationHandlers(httpBuilder);
            }

            void ConfigureRetryPolicy(IHttpClientBuilder httpBuilder)
            {
                var policyConfig = api.Value.RetryPolicy;

                if (policyConfig is null)
                {
                    return;
                }

                var retryPolicyBuilder =
                    Policy.HandleResult<HttpResponseMessage>(r => policyConfig.ShouldRetryStatus((int)r.StatusCode));

                if (policyConfig.RetryOnException)
                {
                    retryPolicyBuilder.Or<HttpRequestException>();
                }

                var retryPolicy = retryPolicyBuilder
                    .WaitAndRetryAsync(policyConfig.RetryCount, retryAttempt => policyConfig.RetryDelay);

                httpBuilder.AddPolicyHandler(retryPolicy);
            }

            void ConfigureAuthorizationHandlers(IHttpClientBuilder httpBuilder)
            {
                if (api.Value.Authorization is not { } auth)
                {
                    return;
                }

                switch (auth.Type)
                {
                    case AuthorizationType.Basic:
                        httpBuilder.AddHttpMessageHandler(serviceProvider =>
                        {
                            var basicAuthService = serviceProvider.GetRequiredKeyedService<BasicAuthService>(api.Key);
                            return new BasicAuthHandler(basicAuthService);
                        });
                        break;
                    case AuthorizationType.OAuth2:
                        httpBuilder.AddHttpMessageHandler(serviceProvider =>
                        {
                            var oAuth2Service = serviceProvider.GetRequiredKeyedService<OAuth2Service>(api.Key);
                            return new OAuth2Handler(oAuth2Service);
                        });
                        break;
                    case AuthorizationType.BearerToken:
                        httpBuilder.AddHttpMessageHandler(serviceProvider =>
                        {
                            var bearerTokenAuthService = serviceProvider.GetRequiredKeyedService<BearerTokenAuthService>(api.Key);
                            return new BearerTokenAuthHandler(bearerTokenAuthService);
                        });
                        break;
                    case AuthorizationType.Custom:
                        var implType = plugin.Assemblies.GetTypeSmart(api.Value.Authorization.Custom?.TypeName!);
                        httpBuilder.AddHttpMessageHandler(serviceProvider =>
                        {
                            var authService = serviceProvider.GetRequiredKeyedService<IAuthorizationService>(api.Key);
                            return new CustomAuthorizationHandler(authService);
                        });
                        break;
                    case AuthorizationType.None:
                    default: break;
                }
            }
        }

        return services;
    }

    private bool TryFindConnectorType(string? templateType, [NotNullWhen(true)] out PluginData? plugin, [NotNullWhen(true)] out Type? connectorType, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(templateType))
        {
            errorMessage = "Connector type name is empty.";
            plugin = null;
            connectorType = null;
            return false;
        }

        var findResult = FindType(templateType);

        if (findResult.Count == 0)
        {
            errorMessage = $"Connector type '{templateType}' not found.";
            plugin = null;
            connectorType = null;
            return false;
        }
        else if (findResult.Count > 1)
        {
            errorMessage = $"Multiple connector types found for '{templateType}': {string.Join(", ", findResult.Select(x => $"{x.Plugin.Name}::{x.Type.FullName}"))}";
            plugin = null;
            connectorType = null;
            return false;
        }

        errorMessage = null;
        plugin = findResult[0].Plugin;
        connectorType = findResult[0].Type;

        return true;
    }

    private async Task ReloadConnectorTemplatesConfig()
    {
        List<IFileInfo> templateFiles = [];
        HashSet<string> physicalPaths = [];

        void AddFiles(IEnumerable<IFileInfo> files)
        {
            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file.PhysicalPath) || physicalPaths.Add(file.PhysicalPath))
                {
                    templateFiles.Add(file);
                }
            }
        }

        AddFiles(_pluginPublisher.Plugins
            .SelectMany(plug => plug.Files.Config.GetFilesRecursive(""))
            .Where(f => TemplateFileNameReg().IsMatch(f.Name)));

        _registry.AppConfigFolder.Refresh();
        if (_registry.AppConfigFolder.Exists)
        {
            AddFiles(_registry.AppConfigFolder.GetFiles("*.json")
                .Where(f => TemplateFileNameReg().IsMatch(f.Name))
                .Select(f => new PhysicalFileInfo(f)));
        }

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddConfiguration(_configuration.GetSection("Connectors"));

        List<IDisposable> buildCleanUp = [];

        foreach (var file in templateFiles)
        {
            var stream = file.CreateReadStream();
            buildCleanUp.Add(stream);
            configBuilder.AddJsonStream(stream);
        }

        var templatesConfiguration = configBuilder.Build();
        buildCleanUp.ForEach(x => x.Dispose());

        var connectorSections = templatesConfiguration
            .GetChildren()
            .Where(section => !section.Key.StartsWith('$'));

        var readTemplates = new Dictionary<string, ConnectorTemplate>();
        foreach (var template in connectorSections)
        {
            try
            {
                ConnectorTemplate connectorTemplate;
                var templateType = template[nameof(ConnectorTemplate.Type)];

                if (!TryFindConnectorType(templateType, out var plugin, out var connectorType, out var errorMessage))
                {
                    throw new Exception(errorMessage);
                }

                // Build Configuration defaults
                if (TypeUtilities.IsConnectorConfigurator(connectorType))
                {
                    var method = connectorType.GetMethod(nameof(IConnectorConfigurator.ConfigureDefaults), BindingFlags.Static | BindingFlags.Public)
                        ?? throw new Exception("Connector does not implement 'ConfigureDefaults' method.");

                    var configurationBuilder = new ConnectorConfigBuilder(template.GetSection("Config"));
                    method.Invoke(null, [configurationBuilder]);

                    var defaults = configurationBuilder.BuildConfiguration();

                    var mergedConfiguration = new ConfigurationBuilder()
                        .AddConfiguration(defaults)
                        .AddConfiguration(template)
                        .Build();

                    connectorTemplate = mergedConfiguration.Get<ConnectorTemplate>()
                        ?? throw new Exception("Failed to build connector template.");

                    // Keep kestrel builder to apply on the connector web host
                    if (connectorTemplate.HttpIncoming is { } httpIncoming)
                    {
                        httpIncoming.KestrelBuilder = configurationBuilder.KestrelBuilder;
                    }

                    // Keep authentication schemes to apply to the connector web host
                    if (connectorTemplate.HttpIncoming?.Auth is { } auth)
                    {
                        auth.Schemes = configurationBuilder.Schemes;
                    }
                }
                else
                {
                    connectorTemplate = template.Get<ConnectorTemplate>()
                        ?? throw new Exception("Failed to build connector template.");
                }

                var jsonString = JsonSerializer.Serialize(connectorTemplate);

                if (connectorTemplate.PacketTransfer is { } packetTransfer)
                {
                    var defaultRetentions = templatesConfiguration
                        .GetSection($"{template.Key}:{nameof(ConnectorTemplate.PacketTransfer)}:{nameof(PacketTransfer.ChannelGroups)}")
                        .GetChildren()
                        .Select(g => KeyValuePair.Create(g.Key, g[nameof(ChannelGroup.PacketRetention)]))
                        .Where(g => !string.IsNullOrEmpty(g.Value))
                        .ToDictionary();

                    defaultRetentions.ForEach(kvp => packetTransfer.ChannelGroups[kvp.Key].PacketRetention["Default"] = kvp.Value!);
                }

                readTemplates.Add(template.Key, connectorTemplate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create connector {Template}", template.Key);
            }
        }

        foreach (var templateKey in Templates.Keys.ToArray())
        {
            if (!readTemplates.ContainsKey(templateKey))
            {
                Templates.Remove(templateKey);
                if (Instances.Remove(templateKey, out var instanceCtx))
                {
                    await instanceCtx.DisposeAsync();
                }
            }
        }

        foreach (var template in readTemplates)
        {
            bool tryRun;

            if (Templates.TryGetValue(template.Key, out var currentTemplate))
            {
                Templates[template.Key] = template.Value;

                if (!currentTemplate.Equals(template.Value))
                {
                    if (Instances.Remove(template.Key, out var instanceCtx))
                    {
                        await instanceCtx.DisposeAsync();
                    }
                    tryRun = true;
                }
                else
                {
                    if (Instances.TryGetValue(template.Key, out var instanceCtx))
                    {
                        instanceCtx.ReloadConfig(template.Value);
                    }
                    tryRun = false;
                }
            }
            else
            {
                Templates.Add(template.Key, template.Value);

                tryRun = !Instances.ContainsKey(template.Key);
            }

            if (tryRun && template.Value.Enabled)
            {
                (await TryCreateConnector(template)).Log(_logger);
            }
        }

        await RefreshConfigs(templateFiles.SelectNotNull(x => x.PhysicalPath).Where(f => f != ""));
        NotifyAboutConfigChanges();
    }

    /// <summary>To be called after a file change introduced and completed a config hot reload.<br/>
    /// Notifies <see cref="IConnectorMessageHandler"/> listeners about a config change.</summary>
    private void NotifyAboutConfigChanges()
    {
        foreach (var handler in _connectorMessageHandlers)
        {
            handler.ConfigChanged();
        }
    }


    private List<(PluginData Plugin, Type Type)> FindType(string typeName)
    {
        IEnumerable<PluginData> searchPlugins = _pluginPublisher.Plugins;

        // check if typename is declared as pluginnamespace::typename
        if (typeName.Split("::", 2) is [var pluginName, var restTypeName])
        {
            searchPlugins = searchPlugins.Where(x => string.Equals(x.Name, pluginName, StringComparison.InvariantCultureIgnoreCase));
            typeName = restTypeName;
        }

        List<(PluginData Plugin, Type Type)> foundTypes = [];

        foreach (var plugin in searchPlugins)
        {
            var connectorType = plugin.Assemblies.GetTypeSmart(typeName);
            if (connectorType is null)
            {
                continue;
            }

            foundTypes.Add((plugin, connectorType));
        }

        return foundTypes;
    }
}

internal sealed class PluginContext : IDisposable
{
    private readonly IDisposable _configChangeHandle;

    public event Action? ConfigChanged;

    public PluginContext(PluginData plugin)
    {
        _configChangeHandle = ChangeToken.OnChange(
            () => plugin.Files.Config.Watch("**/*.json"),
            () => ConfigChanged?.Invoke()
        );
    }

    public void Dispose()
    {
        ConfigChanged = null;
        _configChangeHandle.Dispose();
    }
}

internal sealed class ConnectorContext(
    ILogger logger,
    ConnectorMetadata metadata,
    PluginData plugin)
    : IAsyncDisposable
{
    private bool _disposed;
    public CancellationTokenSource StopSource { get; } = new();
    public Task? WorkTask { get; set; }
    public ReloadableMemoryConfigurationSource OptionsSource { get; } = new();
    public ConnectorMetadata Metadata { get; } = metadata;
    public PluginData Plugin { get; } = plugin;
    public ServiceProvider? ServiceProvider { get; set; }

    public event AsyncEvent.SimpleHandler? OnDispose;

    public void ReloadConfig(ConnectorTemplate template)
    {
        var newConfig = template.Config?.AsEnumerable(true) ?? [];
        OptionsSource.Update(data =>
        {
            data.Clear();
            data.AddRange(newConfig);
        });
    }

    /// <summary>Stops</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        using var logScope = logger.BeginKvpScope(("Template", Metadata.TemplateName));

        await StopSource.CancelAsync();

        try
        {
            if (WorkTask is not null)
            {
                await WorkTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            logger.LogInformation("Connector stopped.");
        }
        catch (OperationCanceledException) // Expected when Token gets cancelled.
        {
            logger.LogInformation("Connector stopped by cancellation.");
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Timed out waiting for graceful shutdown of connector");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop connector");
        }

        if (ServiceProvider != null)
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Task.WhenAll(ServiceProvider.GetServices<IHostedService>()
                    .Reverse().Select(x => x.StopAsync(cts.Token)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop connector hosted services");
            }
        }

        try
        {
            await OnDispose.InvokeAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to dispose service context");
        }

        StopSource.Dispose();
    }
}
