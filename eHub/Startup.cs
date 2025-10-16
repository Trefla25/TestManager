using System.Reflection;
using eHub.Scripting;
using eHub.Scripting.Connectors;
using eController.Util;
using eController.WebUI.Library;
using eController.WebUI.Library.DevLauncher;
using eHub.Authentication;
using eHub.Config;
using eHub.Config.Providers;
using eHub.Filters;
using eHub.Middleware;
using eHub.Scripting;
using eHub.Scripting.Controllers;
using ElementLogic.Configuration.DependencyInjection;
using ElementLogic.Configuration.Hosting;
using ePlugin.Engine;
using eScheduler.Library.Config;
using eScheduler.Library.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;

if (WindowsServiceHelpers.IsWindowsService())
{
    EnvironmentUtil.ForceWorkingDirectoryToExecutable();
}

ILogger logger = NullLogger.Instance;

try
{
    var webAppBuilder = WebApplication.CreateBuilder(args);
    webAppBuilder.Host.UseWindowsService();
    webAppBuilder.Host.UseEffortlessConfiguration(setup =>
    {
        setup.ApplicationType = "eHub";
    });
    var embeddedWebUi = ServerCore.CreateEmbeddedWebUI(webAppBuilder,
        typeof(eHub.Manager.Startup),
        typeof(eHub.UI.Startup),
        typeof(eScheduler.UI.Startup));

    logger = embeddedWebUi.MainLogger;

    await ConfigureServices(embeddedWebUi, embeddedWebUi.PluginContainer, embeddedWebUi.LoggerFactory);
    using var app = embeddedWebUi.Builder.Build();
    await Configure(embeddedWebUi, app);
    await app.RunAsync();
}
catch (Exception exception)
{
    logger.LogError(exception, "Stopped program because of exception");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}


///<summary>This method gets called by the runtime. Use this method to add services to the container.</summary>
async Task ConfigureServices(EmbeddedLaunchSetup setup, PluginServiceProvider pluginContainer, ILoggerFactory loggingFactory)
{
    var configuration = setup.Builder.Configuration;
    var services = setup.Builder.Services;

    services.Configure<IntegrationHubConfig>(configuration.GetSection(IntegrationHubConfig.DefaultKey));
    services.Configure<AuthenticationConfig>(configuration.GetSection(AuthenticationConfig.DefaultKey));

    services.AddOpenTelemetry()
        .WithMetrics(metricsBuilder =>
        {
            metricsBuilder.AddPrometheusExporter();
            metricsBuilder.AddMeter("eHub", "eHub.*");
        });

    services.AddControllers(options => options.RespectBrowserAcceptHeader = true)
        .AddXmlSerializerFormatters()
        .AddXmlDataContractSerializerFormatters()
        .ConfigureApiBehaviorOptions(options =>
        {
            var oldHandler = options.InvalidModelStateResponseFactory;
            var errorApiLogger = loggingFactory.CreateLogger("eHub.ApiError");

            options.InvalidModelStateResponseFactory = context =>
            {
                var response = oldHandler(context);
                var logObject = response is ObjectResult objectResult
                    ? objectResult.Value
                    : response;
                errorApiLogger.LogWarning("Invalid api call. Returning error: {@Response}", logObject);
                return response;
            };
        });

    services.AddSwaggerGen(options =>
    {
        try
        {
            options.IncludeXmlComments(Path.GetFullPath(Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "eHub.xml")));
        }
        catch { }
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "IntegrationHub Rest Api", Version = "v1" });
        options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            In = ParameterLocation.Header,
            Description = "Basic Authorization header using the Bearer scheme."
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "basic"
                    }
                },
                new string[] {}
            }
        });
    });

    PluginContract.EnsureContractAssemblyLoaded();

    // Action Filter for handling the connector schedulers
    services.AddScoped<ConnectorSchedulerServiceFilter>();

    services.AddHubControllers();
    services.AddHubConnectors();

    var schedulerApiConfigSource = new SchedulerApiConfigurationSource(setup.Builder.Configuration);
    setup.Builder.Configuration.Sources.Add(schedulerApiConfigSource);
    services.AddSingleton(schedulerApiConfigSource.GetProvider());

    var authConfig = configuration.GetSection(AuthenticationConfig.DefaultKey).Get<AuthenticationConfig>();
    if (authConfig is { } && authConfig.Enabled)
    {
        services.AddEHubAuthentication(authConfig);
    }

    await ServerCore.ConfigureServices(setup);

    var schedulerConfig = configuration.GetSection(SchedulerOptions.DefaultKey);
    if (!schedulerConfig.GetChildren().Any() && setup.Builder.Host.Properties.TryGetValue("EffortlessSetup", out var obj) && obj is EffortlessSetup effortlessSetup)
    {
        var schedulerConfigPath = Path.Combine(effortlessSetup.ConfigRootFolder, $"{effortlessSetup.ApplicationType}_{effortlessSetup.Environment}", "SchedulerConfig.json");

        schedulerConfig = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new($"{SchedulerOptions.DefaultKey}:{nameof(SchedulerOptions.DataPath)}", schedulerConfigPath),
                new($"{SchedulerOptions.DefaultKey}:{nameof(SchedulerOptions.InstanceName)}", "eHub")])
           .Build()
           .GetSection(SchedulerOptions.DefaultKey);
    }

    services.AddEScheduler(schedulerConfig);
}

///<summary>This method gets called by the runtime. Use this method to configure the HTTP request pipeline.</summary>
async Task Configure(EmbeddedLaunchSetup setup, WebApplication app)
{
    app.UseMiddleware<DebugRequestLog>();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "eHub v1"));

    app.MapPrometheusScrapingEndpoint();

    await ServerCore.Configure(setup, app);
}
