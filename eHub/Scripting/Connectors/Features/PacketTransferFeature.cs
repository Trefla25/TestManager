using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using eController.Util.Serialization;
using eController.Util.Tasks;
using eHub.Config;
using eHub.Contracts;
using eHub.Contracts.Helper;
using eHub.Contracts.UIConfig;
using eHub.Database;
using eHub.Database.Models;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eHub.Scripting.Connectors.Db;
using eHub.Scripting.Connectors.Metrics;
using eHub.Scripting.Connectors.Services;
using ElementLogic.Configuration.Client;
using eMessenger;
using ePlugin.Engine.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace eHub.Scripting.Connectors.Features;

public class PacketTransferFeature : IConnectorFeature
{
    private readonly ILogger<PacketTransferFeature> _logger;
    private readonly IDbContextFactory<HubDbContext> _dbContextFactory;
    private readonly IMessenger _messenger;
    private readonly IPacketTransfer _packetTransfer;
    private readonly PacketRepository _packetRepository;
    private readonly IFilterRunnerProvider _filterRunnerProvider;
    private readonly ConnectorMetadata _metadata;
    private readonly ConnectorTemplate _connectorTemplate;
    private readonly PacketDtoService _packetDtoService;
    private readonly IEffortlessConfigurationRegistry _registry;
    private readonly PluginData _plugin;
    private readonly ConnectorMetrics _connectorMetrics;
    private readonly FrozenDictionary<string, string> _channelGroupMapping;
    private readonly FrozenDictionary<string, AwaitableAutoResetEvent> _processPacketTriggers;
    private readonly FrozenDictionary<string, ChannelGroup> _channelGroups;
    private UIViewConfig _viewConfig = new();
    private Task _workTask = Task.CompletedTask;

    private CancellationTokenSource? _cancellationTokenSource;

    private IRegistrationToken _regToken = NullRegistrationToken.Instance;

    public PacketTransferFeature(
        ILogger<PacketTransferFeature> logger,
        IDbContextFactory<HubDbContext> dbContextFactory,
        IScopedMessenger messenger,
        IPacketTransfer packetTransfer,
        IFilterRunnerProvider filterRunnerProvider,
        IEffortlessConfigurationRegistry registry,
        PacketDtoService packetDtoService,
        ConnectorMetadata metadata,
        ConnectorTemplate connectorTemplate,
        PluginData plugin,
        ConnectorMetrics connectorMetrics)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _messenger = messenger;
        _packetTransfer = packetTransfer;
        _filterRunnerProvider = filterRunnerProvider;
        _packetRepository = new PacketRepository(this);
        _metadata = metadata;
        _packetDtoService = packetDtoService;
        _registry = registry;
        _plugin = plugin;
        _connectorMetrics = connectorMetrics;
        _channelGroups = connectorTemplate.PacketTransfer?.ChannelGroups.ToFrozenDictionary() ?? FrozenDictionary<string, ChannelGroup>.Empty;

        if (_channelGroups.Values
            .SelectMany(group => _channelGroups.Values
                .Where(otherGroup => group != otherGroup)
                .Select(otherGroup => (group.Channels, otherGroup.Channels))
            )
            .Any(channelsPair => channelsPair.Item1.Overlaps(channelsPair.Item2)))
        {
            throw new Exception("Channel groups can not overlap!");
        }

        _channelGroupMapping = _channelGroups
            .SelectMany(group => group.Value.Channels
                .Select(channel => (Channel: channel, Group: group.Key)))
            .ToFrozenDictionary(item => item.Channel, item => item.Group);

        _processPacketTriggers = _channelGroups.ToFrozenDictionary(x => x.Key, x => new AwaitableAutoResetEvent());
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cancellationTokenSource is { })
        {
            throw new InvalidOperationException("Packet Transfer Feature already started");
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _packetTransfer.PacketRepository = _packetRepository;
        _packetTransfer.TriggerPacketProcess += TriggerPacketProcess;
#pragma warning disable CS0618 // Ignore obsolete for backwards compatibility impl
        _packetTransfer.PacketReceived += ConnectorPacketReceived;
        _packetTransfer.ResetPacketRetryCount += ResetPacketRetryCount;
        _packetTransfer.ResetAllPacketsRetryCount += ResetAllPacketsRetryCount;
#pragma warning restore CS0618

        await InitializeDatabase();

        await ReloadConnectorUIViewConfig();

        StartPacketSchedulers(_cancellationTokenSource.Token);

        await ConnectorStarted();
    }

    private void StartPacketSchedulers(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>();
        foreach (var (groupName, groupConfig) in _channelGroups)
        {
            if (groupConfig.Channels.Count == 0)
            {
                _logger.LogWarning("Channel group '{Group}' has no channels configured, skipping.", groupName);
                continue;
            }

            switch (groupConfig.Mode)
            {
            case ChannelMode.Sequential:
                tasks.Add(RunSequentialChannelProcessing(groupName, stoppingToken));
                break;
            case ChannelMode.Concurrent:
                tasks.Add(RunConcurrentChannelProcessing(groupName, stoppingToken));
                break;
            default:
                throw new NotSupportedException($"Unknown channel mode: {groupConfig.Mode}");
            }
        }

        _workTask = Task.WhenAll(tasks);
    }

    private async Task ReloadConnectorUIViewConfig()
    {
        var uiViewConfigFile = Path.Combine(_registry.AppConfigFolder.FullName, _metadata.TemplateName, "UIViewConfig.json");

        using var fileStream = File.Exists(uiViewConfigFile)
            ? new FileStream(uiViewConfigFile, FileMode.Open, FileAccess.Read)
            : _plugin.Files.Config.GetFilesRecursive("")
                .Where(f => f.Name.Equals("UIViewConfig.json", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault()?.CreateReadStream();

        UIViewConfig? viewConfig = null;
        if (fileStream != null)
        {
            viewConfig = JsonSerializer.Deserialize<UIViewConfig>(fileStream);
        }

        viewConfig ??= await _packetTransfer.BuildUIViewConfig();
        viewConfig ??= CreateDefaultView();

        viewConfig.Views = viewConfig.Views
            .Where(view => view.Value.Connectors == null // TODO: null should not be accepted here because it is needed in the UI for PacketGridGroups
                || view.Value.Connectors.Contains(_metadata.TemplateName))
            .ToDictionary(v => v.Key, v => v.Value);

        // Set all channels to this connector if not set
        foreach (var view in viewConfig.Views.Values)
        {
            foreach (var channel in view.Channels.Where(c => string.IsNullOrWhiteSpace(c.Connector)))
            {
                channel.Connector = _metadata.TemplateName;
            }
        }

        _viewConfig = viewConfig;
    }

    private async Task InitializeDatabase()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var currentMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var dbExists = currentMigrations.Length != 0;
        if (dbExists)
        {
            _logger.LogInformation("PacketTransfer database on schema version: {CurrentSchemaVersion}", currentMigrations.Last());
        }
        else
        {
            _logger.LogInformation("PacketTransfer database does not exist yet");
        }

        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pendingMigrations.Length > 0)
        {
            _logger.LogInformation("Applying {Count} migrations.", pendingMigrations.Length);
            await db.Database.MigrateAsync();

            var migratedTo = (await db.Database.GetAppliedMigrationsAsync()).Last();
            _logger.LogInformation("PacketTransfer database upgraded to schema version: {NewSchemaVersion}", migratedTo);
        }
    }

    private async Task ConnectorStarted()
    {
        _regToken += await _messenger.AnswerAsync(ConnectorContract.PollAliveConnectorsTopic(),
            () => new ConnectorKeepAliveDto(_metadata.ConnectorIdentifier));

        _regToken += await _messenger.AnswerAsync(
            ConnectorContract.UIGetTopic(_metadata.ConnectorIdentifier),
            () => new ConnectorUiData(_metadata.TemplateName, _metadata.ConnectorType, _viewConfig));

        _regToken += await _messenger.AnswerAsync<PacketRequestDto, PacketWrapperDto>(
            ConnectorContract.UIPacketsGetTopic(_metadata.ConnectorIdentifier),
            GetFilteredPacketsForUI);

        _regToken += await _messenger.ListenAsync<ImmutableArray<PacketResendDto>>(
            ConnectorContract.PacketResendTopic(_metadata.ConnectorIdentifier),
            PacketResend);

        _regToken += await _messenger.ListenAsync<ImmutableArray<long>>(
            ConnectorContract.PacketManualStopSequenceTopic(_metadata.ConnectorIdentifier),
            PacketManualStopSequence);

        _regToken += await _messenger.ListenAsync<ImmutableArray<long>>(
            ConnectorContract.PacketDeleteSequenceTopic(_metadata.ConnectorIdentifier),
            PacketDeleteSequence);

        _regToken += await _messenger.AnswerAsync<PacketRequestDto, ConnectorPacketsExportDto>(
            ConnectorContract.PacketExportTopic(_metadata.ConnectorIdentifier),
            GetFilteredPackets);

        _regToken += await _messenger.AnswerAsync<ConnectorPacketsTryImportDto, bool>(
            ConnectorContract.PacketTryImportTopic(_metadata.ConnectorIdentifier),
            TryImportPackets);

        _regToken += await _messenger.ListenAsync<FilterRunnerRequest>(
            ConnectorContract.StartFilterRunnerTopic(_metadata.ConnectorIdentifier),
            StartFilterRunner);

        _regToken += await _messenger.ListenAsync<string>(
            ConnectorContract.StopFilterRunnerTopic(_metadata.ConnectorIdentifier),
            StopFilterRunner);

        _regToken += await _messenger.AnswerAsync(
            ConnectorContract.GetCustomFiltersTopic(_metadata.ConnectorIdentifier),
            _packetDtoService.GetUICustomFilters);

        await _messenger.SendAsync(ConnectorContract.ConnectorStartedTopic(),
            new ConnectorStartedEventDto(_metadata.ConnectorIdentifier, new(_metadata.TemplateName, _metadata.ConnectorType, _viewConfig)));
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource is null)
        {
            return;
        }

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;

        await _regToken.DisposeAsync();

        await _workTask.WaitAsync(TimeSpan.FromSeconds(5));

        await _messenger.SendAsync(ConnectorContract.ConnectorStoppedTopic(),
            new ConnectorStoppedEventDto(_metadata.ConnectorIdentifier));

        _packetTransfer.TriggerPacketProcess -= TriggerPacketProcess;

#pragma warning disable CS0618 // Ignore obsolete for backwards compatibility impl
        _packetTransfer.PacketReceived -= ConnectorPacketReceived;
        _packetTransfer.ResetPacketRetryCount -= ResetPacketRetryCount;
        _packetTransfer.ResetAllPacketsRetryCount -= ResetAllPacketsRetryCount;
#pragma warning restore CS0618
    }

    private void TriggerPacketProcess(string channelName)
    {
        if (_channelGroupMapping.TryGetValue(channelName, out var group))
        {
            _logger.LogInformation("Trigger process packet loop on {group}.", group);
            _processPacketTriggers[group].Set();
        }
        else
        {
            _logger.LogInformation("{channelName} has no associated group.", channelName);
        }
    }

    public async Task<Packet[]> CreatePackets(Packet[] packets, CancellationToken cancellationToken = default)
    {
        if (packets.Length == 0)
        {
            return [];
        }

        var dateTimeNow = DateTime.Now;
        foreach (var packet in packets)
        {
            if (packet.DateCreated == default)
            {
                packet.DateCreated = dateTimeNow;
            }
            else if (packet.DateCreated > dateTimeNow)
            {
                _logger.LogWarning("New Packet has a future creation date {DateCreated}, setting to now.", packet.DateCreated);
                packet.DateCreated = dateTimeNow;
            }
        }

        try
        {
            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await hubDbContext.Packet.AddRangeAsync(packets, cancellationToken);
            await hubDbContext.SaveChangesAsync(cancellationToken);

            await RefreshUI(new ConnectorPacketsChangedDto(new ConnectorPacketsFilterDto(MinId: packets.Min(p => p.Id), MaxId: packets.Max(p => p.Id))));

            foreach (var packet in packets)
            {
                _connectorMetrics.TrackPacketCreated(packet.Channel, packet.Status);
            }

            // Trigger all channel groups with enqueued packets
            var groups = packets
                .Where(p => p.Status is PacketStatus.Enqueued)
                .Select(p => p.Channel)
                .Distinct()
                .Select
                (channel =>
                {
                    if (!_channelGroupMapping.TryGetValue(channel, out var channelGroup))
                    {
                        _logger.LogWarning("The channel '{channel}' is not associated with any channel group, therefore the packet will not be processed.", channel);
                    }
                    return channelGroup;
                })
                .Where(group => group is not null)
                .Distinct();

            foreach (var groupName in groups)
            {
                _processPacketTriggers[groupName!].Set();
            }

            return packets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PacketCreate(Packet packet)");
            return [];
        }
    }

    public async Task<Packet[]> GetPackets(Expression<Func<Packet, bool>>? condition = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            IQueryable<Packet> query = hubDbContext.Packet;

            if (condition is { })
            {
                query = query.Where(condition);
            }

            return await query.ToArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get packets from database!");
            return [];
        }
    }

    static readonly ValueTuple<Type, Expression, Expression> UpdateDateChangedToNow
        = (typeof(DateTime?),
            (Expression<Func<Packet, DateTime?>>)(p => p.DateChanged),
            ((Expression<Func<DateTime?>>)(() => DateTime.Now)).Body);

    public async Task<int> UpdatePackets(IEnumerable<(Type, Expression, Expression)> updateActions, Expression<Func<Packet, bool>>? condition = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            IQueryable<Packet> query = hubDbContext.Packet;

            if (condition is { })
            {
                query = query.Where(condition);
            }

            var updatedRows = await query.ExecuteUpdateAsync([.. updateActions, UpdateDateChangedToNow], cancellationToken);

            if (updatedRows > 0)
            {
                await RefreshUI(ConnectorPacketsChangedDto.Any);
            }

            return updatedRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update the database!");
            return 0;
        }
    }

    public async ValueTask StartFilterRunner(FilterRunnerRequest filterRunnerRequest)
    {
        var filterRunner = _filterRunnerProvider.GetOrAddFilterRunner(filterRunnerRequest.FilterRunnerIdentifier);
        await filterRunner.StartAsync(filterRunnerRequest.PacketRequestDto, _cancellationTokenSource?.Token ?? default);
    }

    public async ValueTask StopFilterRunner(string filterRunnerIdentifier)
    {
        if (_filterRunnerProvider.TryGetFilterRunner(filterRunnerIdentifier, out var filterRunner))
        {
            await filterRunner.StopAsync(_cancellationTokenSource?.Token ?? default);
        }
    }

    private async ValueTask<ConnectorPacketsExportDto> GetFilteredPackets(PacketRequestDto filter)
    {
        try
        {
            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
            var query = _packetDtoService.CreateFilteredQuery(hubDbContext, filter).AsNoTracking();
            var exportPackets = await GetPacketsForUI(query, filter);
            return new ConnectorPacketsExportDto([.. exportPackets]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFilteredPackets(ConnectorPacketsFilter filter)");
            return new ConnectorPacketsExportDto([]);
        }
    }

    private async ValueTask<PacketWrapperDto> GetFilteredPacketsForUI(PacketRequestDto filter)
    {
        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
        var query = _packetDtoService.CreateFilteredQuery(hubDbContext, filter).AsNoTracking();
        var uiPackets = await GetPacketsForUI(query, filter);
        var packetWrapper = GetPacketWrapperFromPackets(uiPackets);
        return packetWrapper;
    }

    private PacketWrapperDto GetPacketWrapperFromPackets(IEnumerable<PacketDto> packets)
    {
        var packetWrapper = new PacketWrapperDto()
        {
            ConnectorIdentifier = _metadata.ConnectorIdentifier,
            Packets = [.. packets]
        };

        return packetWrapper;
    }

    private async ValueTask<List<PacketDto>> GetPacketsForUI(IQueryable<Packet> query, PacketRequestDto? filter = null)
    {
        query = query.OrderByDescending(p => p.Id);

        var uiPackets = new List<PacketDto>();
        var childPacketsCount = 0;
        var lastId = long.MaxValue;
        var dataFilters = filter?.ColumnFilters?.Where(x => x.ColumnName == nameof(PacketDto.Data)).ToImmutableArray() ?? [];
        var previewDataFilters = filter?.ColumnFilters?.Where(x => x.ColumnName == nameof(PacketDto.PreviewData)).ToImmutableArray() ?? [];

        do
        {
            Packet[] packetsBatch;

            packetsBatch = filter?.Limit != null
                ? await query.Where(p => p.Id < lastId).Take(filter.Limit.Value).ToArrayAsync()
                : await query.ToArrayAsync();

            if (packetsBatch.Length == 0)
            {
                break;
            }

            lastId = packetsBatch[^1].Id;

            foreach (var packet in packetsBatch)
            {
                var uiPacket = await _packetDtoService.BuildDtoAsync(packet);

                var matches = dataFilters.Matches(uiPacket.Data) && previewDataFilters.Matches(uiPacket.PreviewData);

                if (!matches)
                {
                    continue;
                }

                uiPackets.Add(uiPacket);

                // Do not count child packets towards the top count
                if (packet.ParentId != null)
                {
                    childPacketsCount++;
                }

                if (uiPackets.Count - childPacketsCount == filter?.Limit)
                {
                    break;
                }
            }
        } while (filter?.Limit != null && uiPackets.Count - childPacketsCount < filter.Limit);

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
        await _packetDtoService.FillChildAndParentPacketsAsync(hubDbContext, uiPackets, [.. uiPackets.Select(p => p.Id)]);

        return uiPackets;
    }

    private async ValueTask PacketResend(ImmutableArray<PacketResendDto> packets)
    {
        try
        {
            Packet[] dbPackets = [];
            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
            var packetIds = packets.Select(p => p.Id);

            // Only packets in resendable channel groups are allowed to be resent
            var resendableChannels = _channelGroups.Values.Where(g => g.CanResend).SelectMany(g => g.Channels);
            dbPackets = await hubDbContext.Packet.Where(p => packetIds.Contains(p.Id) && resendableChannels.Contains(p.Channel)).ToArrayAsync();

            if (dbPackets.Length < packets.Length)
            {
                _logger.LogWarning("PacketResend: Could not find all packets in the database. IDs not found: {ids}", packetIds.Where(id => dbPackets.Any(p => p.Id == id)).Join(", "));
            }

            // Iterate on the ImmutableArray<PacketResendDto> packets instead of dbPackets, to keep the order
            foreach (var resendDto in packets)
            {
                if (dbPackets.FirstOrDefault(p => p.Id == resendDto.Id) is not { } dbPacket)
                {
                    continue;
                }

                if (resendDto.NewData is { } data)
                {
                    var packetData = dbPacket.ToData();
                    var conversionData = await _packetTransfer.Converter.PacketToUIDataConverter(packetData);
                    conversionData.Data = data;
                    var binaryData = await _packetTransfer.Converter.UIToBinaryDataConverter(conversionData, packetData.Metadata);
                    dbPacket.BinaryData = binaryData.ToArray();
                }

                dbPacket.Id = 0;
                dbPacket.Status = PacketStatus.Enqueued;
                dbPacket.RetryCount = 0;
                dbPacket.DateCreated = DateTime.Now;
                dbPacket.DateChanged = null;
            }

            await CreatePackets(dbPackets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PacketResend(ImmutableArray<PacketDto> packets)");
        }
    }

    private async ValueTask PacketManualStopSequence(ImmutableArray<long> packetIds)
    {
        try
        {
            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
            var updatedRows = await hubDbContext.Packet
                .Where(p => packetIds.Contains(p.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, PacketStatus.ManualStop)
                    .SetProperty(p => p.DateChanged, DateTime.Now));

            if (updatedRows > 0)
            {
                var changeData = updatedRows > 1000
                    ? ConnectorPacketsChangedDto.Any
                    : new ConnectorPacketsChangedDto(new ConnectorPacketsFilterDto(MinId: packetIds.Min(), MaxId: packetIds.Max()));
                await RefreshUI(changeData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PacketRunSequence");
        }
    }

    private async ValueTask PacketDeleteSequence(ImmutableArray<long> packetIds)
    {
        try
        {
            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
            var deletedRows = await hubDbContext.Packet.Where(p => packetIds.Contains(p.Id)).ExecuteDeleteAsync();

            if (deletedRows > 0)
            {
                await RefreshUI(ConnectorPacketsChangedDto.Any);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PacketDeleteSequence");
        }
    }

    private async Task RefreshUI(ConnectorPacketsChangedDto packetsChangedFilter)
    {
        await _messenger.SendAsync(ConnectorContract.PacketsChangedTopic(_metadata.ConnectorIdentifier), packetsChangedFilter);
    }

    private async ValueTask<bool> TryImportPackets(ConnectorPacketsTryImportDto importDto)
    {
        try
        {
            var packets = importDto.Export.Packets;

            await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync();
            var dateMin = packets.Min(p => p.DateCreated);

            var canImport = importDto.ForceImport || !hubDbContext.Packet.Any(p => p.DateCreated >= dateMin);

            if (canImport)
            {
                await hubDbContext.Packet.Where(p => p.DateCreated >= dateMin).ExecuteDeleteAsync();

                var dbPackets = await Task.WhenAll(packets.Select(async p =>
                {
                    var conversionData = new UIConversionInfo(p.Data ?? string.Empty, p.PreviewData ?? string.Empty, p.DataType);
                    var binaryData = await _packetTransfer.Converter.UIToBinaryDataConverter(conversionData, p.Metadata);

                    var dbPacket = p.ToDbModel();
                    dbPacket.Id = 0;
                    dbPacket.BinaryData = binaryData.ToArray();
                    return dbPacket;
                }));

                await hubDbContext.Packet.AddRangeAsync(dbPackets);
                await hubDbContext.SaveChangesAsync();

                await RefreshUI(ConnectorPacketsChangedDto.Any);
            }

            return canImport;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportPackets(IEnumerable<Packet> packets)");
            return false;
        }
    }

    private async Task RunSequentialChannelProcessing(string groupName, CancellationToken cancellationToken)
    {
        if (!_processPacketTriggers.TryGetValue(groupName, out var groupTrigger))
        {
            _logger.LogError("The group '{Group}' is not configured.", groupName);
            return;
        }

        if (!_channelGroups.TryGetValue(groupName, out var groupDef))
        {
            _logger.LogError("The group '{Group}' is not configured.", groupName);
            return;
        }

        var dbCleanContext = new DbCleanContext(groupName, groupDef);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await groupTrigger.WaitAsync(groupDef.DbPollInterval, cancellationToken);

                _logger.LogDebug("Triggered packet processing for group {Group}", groupName);

                var runAgain = await CheckUnprocessedPackets(groupDef, cancellationToken);

                // The check method runs in batches. When returning true, there might be more packets to process
                if (runAgain)
                {
                    groupTrigger.Set();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Checking unprocessed packets cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checking unprocessed packets crashed");
            }

            await CheckRemovablePackets(dbCleanContext, cancellationToken);
        }
    }

    private async Task RunConcurrentChannelProcessing(string groupName, CancellationToken cancellationToken)
    {
        if (!_processPacketTriggers.TryGetValue(groupName, out var groupTrigger))
        {
            _logger.LogError("The group '{Group}' is not configured.", groupName);
            return;
        }

        if (!_channelGroups.TryGetValue(groupName, out var groupDef))
        {
            _logger.LogError("The group '{Group}' is not configured.", groupName);
            return;
        }

        int maxConcurrentPackets = groupDef.PacketsPerCycle;
        var dbCleanContext = new DbCleanContext(groupName, groupDef);
        List<ConcurrentPacketTask> packetTasks = [];
        List<ConcurrentPacketTask> completedPackets = [];
        List<Task> waitTasks = [];
        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var unprocessedPackets = await hubDbContext.Packet
                    // Filter packets to process, but not already in processing
                    .Where(x =>
                        groupDef.Channels.Contains(x.Channel)
                        && (x.Status == PacketStatus.Enqueued || x.Status == PacketStatus.Error && groupDef.CanResend == true)
                        && !packetTasks.Select(t => t.Packet.Id).Contains(x.Id))
                    // Prefer the older packets for fairness
                    .OrderBy(x => x.Id)
                    // Limit to fill up to the max number of tasks to run concurrently
                    .Take(maxConcurrentPackets - packetTasks.Count)
                    .ToListAsync(cancellationToken);

                var triggerTask = groupTrigger.WaitAsync(groupDef.DbPollInterval, cancellationToken);

                if (unprocessedPackets.Count == 0 && packetTasks.Count == 0)
                {
                    await triggerTask;
                    continue;
                }

                async Task<ProcessPacketState> ProcessPacket(Packet packet)
                {
                    _logger.LogDebug("Processing packet {Id} with status {Status}", packet.Id, packet.Status);
                    var processStopwatch = Stopwatch.StartNew();
                    ProcessPacketState processResult;
                    try
                    {
                        var packetData = packet.ToData();
                        processResult = await _packetTransfer.ProcessPacket(packetData, cancellationToken);
                        _logger.LogDebug("Packet {Id} processed with result {Result}", packet.Id, processResult);
                    }
                    catch (Exception ex)
                    {
                        processResult = ProcessPacketState.FatalError;
                        _logger.LogError(ex, "ProcessPacket()");
                    }

                    _connectorMetrics.TrackProcessPacketCall(packet.Channel, processResult, processStopwatch.Elapsed);
                    return processResult;
                }

                // Start processing the packets in parallel as separate tasks
                packetTasks.AddRange(unprocessedPackets.Select(p => new ConcurrentPacketTask(p, ProcessPacket(p))));

                // When we are processing the maximum number of packets, we need to wait for at least one to finish
                if (packetTasks.Count == maxConcurrentPackets)
                {
                    await Task.WhenAny(packetTasks.Select(t => t.Task));
                }
                // Otherwise there's still slots for more packets, so we add also include the trigger task
                // This allows adding more packets to process if they are added while we are processing
                else
                {
                    waitTasks.Clear();
                    waitTasks.Add(triggerTask);
                    waitTasks.AddRange(packetTasks.Select(t => t.Task));
                    await Task.WhenAny(waitTasks);
                }

                // Remove completed packets, and store them separately
                completedPackets.Clear();
                packetTasks.RemoveAll(t =>
                {
                    if (t.Task.IsCompleted)
                    {
                        completedPackets.Add(t);
                        return true;
                    }
                    return false;
                });

                // Apply the results to the packets
                foreach (var task in completedPackets)
                {
                    var processPacketState = task.Task.Result; // This is safe because we checked IsCompleted above
                    var packet = task.Packet;

                    ApplyProcessStateToPacket(processPacketState, packet, groupDef);
                }

                // In case the trigger task completed, but no packets were processed, we don't need to save the changes
                if (completedPackets.Count > 0)
                {
                    await hubDbContext.SaveChangesAsync(cancellationToken);

                    // Detach the processed packets to avoid memory leaks
                    foreach (var packet in completedPackets)
                    {
                        hubDbContext.Entry(packet.Packet).State = EntityState.Detached;
                    }

                    await RefreshUI(
                        new ConnectorPacketsChangedDto(
                            new ConnectorPacketsFilterDto(
                                MinId: completedPackets.Min(t => t.Packet.Id),
                                MaxId: completedPackets.Max(t => t.Packet.Id))));

                    completedPackets.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Checking unprocessed packets cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checking unprocessed packets crashed");
            }

            await CheckRemovablePackets(dbCleanContext, cancellationToken);
        }
    }

    /// <summary>Process packets from the database that are in the enqueued state or to be retried.</summary>
    /// <param name="groupDef">The channel group to process packets of.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while processing packets.</param>
    /// <returns><see langword="true"/> if there are more packets waiting that can be processed. <see langword="false"/> otherwise.</returns>
    private async Task<bool> CheckUnprocessedPackets(ChannelGroup groupDef, CancellationToken cancellationToken)
    {
        const int MinFetchCount = 10;
        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var minChangedId = long.MaxValue;
        var maxChangedId = long.MinValue;
        int updatedRows = 0;
        var shouldBreak = false;
        int processedPackets = 0;
        var maxPacketsToProcess = Math.Max(MinFetchCount, groupDef.PacketsPerCycle);
        int saveBatchSize = Math.Max(1, groupDef.PacketsPerCycle);
        Stopwatch processStopwatch = new();

        Expression<Func<Packet, bool>> statusCondition = groupDef.CanResend
            ? (x => x.Status == PacketStatus.Enqueued || x.Status == PacketStatus.Error)
            : (x => x.Status == PacketStatus.Enqueued);

        var packetBatchToProcess = await hubDbContext.Packet
            .Where(x => groupDef.Channels.Contains(x.Channel))
            .Where(statusCondition)
            .OrderBy(x => x.Id)
            .Take(maxPacketsToProcess)
            .ToListAsync(cancellationToken);

        foreach (var packet in packetBatchToProcess)
        {
            processStopwatch.Restart();
            ProcessPacketState processResult;

            try
            {
                _logger.LogDebug("Processing packet {Id} with status {Status}", packet.Id, packet.Status);
                var packetData = packet.ToData();
                processResult = await _packetTransfer.ProcessPacket(packetData, cancellationToken);
                _logger.LogDebug("Packet {Id} processed with result {Result}, breaking {Break}",
                    packet.Id, processResult, shouldBreak);
            }
            catch (Exception ex)
            {
                processResult = ProcessPacketState.FatalError;
                _logger.LogError(ex, "CheckUnprocessedPackets()");
            }

            shouldBreak = ApplyProcessStateToPacket(processResult, packet, groupDef);
            _connectorMetrics.TrackProcessPacketCall(packet.Channel, processResult, processStopwatch.Elapsed);

            minChangedId = Math.Min(minChangedId, packet.Id);
            maxChangedId = Math.Max(maxChangedId, packet.Id);
            processedPackets++;

            if (shouldBreak) { break; }

            _logger.LogDebug("Batch status: {ProcessedPackets} %{BatchSize}", processedPackets, saveBatchSize);
            if (processedPackets % saveBatchSize == 0)
            {
                updatedRows += await hubDbContext.SaveChangesAsync(cancellationToken);
            }
        }

        updatedRows += await hubDbContext.SaveChangesAsync(cancellationToken);

        if (updatedRows > 0)
        {
            await RefreshUI(new ConnectorPacketsChangedDto(new ConnectorPacketsFilterDto(MinId: minChangedId, MaxId: maxChangedId)));
        }

        return processedPackets == maxPacketsToProcess;
    }

    private static bool ApplyProcessStateToPacket(ProcessPacketState processPacketState, Packet packet, ChannelGroup group)
    {
        switch (processPacketState)
        {
        case ProcessPacketState.Success:
            packet.Status = PacketStatus.Processed;
            packet.DateChanged = DateTime.Now;
            return false;
        case ProcessPacketState.Retry:
            packet.Status = group.CanResend ? PacketStatus.Enqueued : PacketStatus.FatalError;
            packet.RetryCount = packet.RetryCount < int.MaxValue ? packet.RetryCount + 1 : 1;
            packet.DateChanged = DateTime.Now;
            return true;
        case ProcessPacketState.InProgress:
            packet.Status = PacketStatus.InProgress;
            packet.DateChanged = DateTime.Now;
            return false;
        case ProcessPacketState.Error:
            packet.RetryCount = packet.RetryCount < int.MaxValue ? packet.RetryCount + 1 : 1;
            packet.Status = group.CanResend ? PacketStatus.Error : PacketStatus.FatalError;
            packet.DateChanged = DateTime.Now;
            return true;
        case ProcessPacketState.RetryUnchanged:
            return true;
        case ProcessPacketState.FatalError:
            packet.Status = PacketStatus.FatalError;
            packet.DateChanged = DateTime.Now;
            return true;
        default:
            throw new ArgumentOutOfRangeException(nameof(processPacketState), processPacketState, "Invalid process state");
        }
    }

    private ValueTask CheckRemovablePackets(DbCleanContext context, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.CompletedTask;
        }

        if (context.LastClean.IsAgoLessThan(context.Group.CleanerInterval))
        {
            return ValueTask.CompletedTask;
        }

        context.LastClean = DateTime.Now;

        return new ValueTask(CheckRemovablePacketsCore());

        async Task CheckRemovablePacketsCore()
        {
            try
            {
                await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var cleanupStopwatch = Stopwatch.StartNew();
                var deletedRows = await DeleteExpiredPackets(context, hubDbContext, cancellationToken);
                _connectorMetrics.TrackCleanupCycle(context.GroupName, cleanupStopwatch.Elapsed);

                if (deletedRows > 0)
                {
                    await RefreshUI(ConnectorPacketsChangedDto.Any);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Checking removable packets cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checking removable packets crashed");
            }
        }
    }

    private UIViewConfig CreateDefaultView()
    {
        // Create 2 default views, one for all channels and one for each channel

        var channels = _channelGroups.Values.SelectMany(group => group.Channels).ToArray();

        return new UIViewConfig()
        {
            Views = new Dictionary<string, ViewConfig>()
                {
                    {
                        "All",
                        new ViewConfig()
                        {
                            Name = "All",
                            Channels = channels.Select(c => new ChannelConfig() {
                                Name = c,
                                Channel = c,
                                Position = new(1, 1)
                            }).ToList()
                        }
                    },
                    {
                        "Each",
                        new ViewConfig()
                        {
                            Name = "Each",
                            Channels = channels.Zip(PositionGenerator(), (c, pos) => new ChannelConfig() {
                                Name = c,
                                Channel = c,
                                Position = pos
                            }).ToList()
                        }
                    },
                }
        };

        // Generates an endless sequence of positions by filling up rectangles of increasing size
        // Eg:
        // 0 1 4 9
        // 2 3 5 .
        // 6 7 8 .
        static IEnumerable<Position> PositionGenerator()
        {
            int x = 0, y = 0;
            var dir = false;
            while (true)
            {
                // Position indices are 1-based, internal count is 0-based
                yield return new(y + 1, x + 1);

                if (dir)
                {
                    Step(ref y, ref x, ref dir);
                }
                else
                {
                    Step(ref x, ref y, ref dir);
                }
            }

            static void Step(ref int main, ref int other, ref bool dir)
            {
                main++;
                if (main > other - (dir ? 1 : 0))
                {
                    dir = !dir;
                    other = 0;
                }
            }
        }
    }
    public async ValueTask<int> DeleteExpiredPackets(DbCleanContext context, HubDbContext hubDbContext, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var defaultRetention = context.Group.PacketRetention["Default"] ?? throw new Exception("No default retention policy provided.");

        var deleteTimes = Enum
            .GetValues<PacketStatus>()
            .Distinct()
            .Select(status => (
                Status: status,
                Time: context.Group.PacketRetention.GetValueOrDefault(status.ToString(), defaultRetention)
            ))
            .Where(x => x.Time != "keep") // Exclude statuses with "keep" retention
            .ToDictionary(
                x => x.Status,
                x => now - HumanTimeSpan.Parse(x.Time) ?? throw new Exception($"Failed to parse retention time for status {x.Status}")
            );

        var statusWithSameDeleteTime = deleteTimes
            .GroupBy(x => x.Value, x => x.Key);

        ParameterExpression param = Expression.Parameter(typeof(Packet), "p");
        Expression deleteExpression = Expression.Constant(false);

        foreach (var deleteGroup in statusWithSameDeleteTime)
        {
            var deleteTime = deleteGroup.Key;
            var statuses = deleteGroup.ToHashSet();
            Expression<Func<Packet, bool>> deletePart = p => statuses.Contains(p.Status) && p.DateCreated < deleteTime;
            deleteExpression = Expression.OrElse(deleteExpression, Expression.Invoke(deletePart, param));
        }

        var deleteLambda = Expression.Lambda<Func<Packet, bool>>(deleteExpression, param);

        return await hubDbContext.Packet
            .Where(p => context.Group.Channels.Contains(p.Channel))
            .Where(deleteLambda)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private record struct ConcurrentPacketTask(Packet Packet, Task<ProcessPacketState> Task);

    public class DbCleanContext(string groupName, ChannelGroup group)
    {
        public DateTime LastClean { get; set; } = DateTime.MinValue;
        public string GroupName { get; } = groupName;
        public ChannelGroup Group { get; } = group;
    }

    #region Backwards Compatibility Methods

    private void ConnectorPacketReceived(PacketData packetData)
        => _packetRepository
            .Create()
            .Add(packetData)
            .CreateAsync()
            .Wait();

    private void ResetPacketRetryCount(long packetId)
        => _packetRepository
            .Update()
            .Where(p => p.Id == packetId)
            .Set(p => p.RetryCount, 0)
            .Set(p => p.DateChanged, DateTime.Now)
            .ExecuteAsync()
            .Wait();

    private void ResetAllPacketsRetryCount(PacketStatus packetStatus)
        => _packetRepository
            .Update()
            .Where(p => p.Status == packetStatus)
            .Set(p => p.RetryCount, 0)
            .Set(p => p.DateChanged, DateTime.Now)
            .ExecuteAsync()
            .Wait();

    #endregion
}
