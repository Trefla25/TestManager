using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.Helper;
using eHub.Contracts.UIConfig;
using eHub.Database;
using eMessenger;
using Microsoft.EntityFrameworkCore;

namespace eHub.Scripting.Connectors.Services;

public class FilterRunner(
    string filterRunnerId,
    ConnectorMetadata metadata,
    PacketDtoService packetDtoService,
    ILogger<FilterRunner> logger,
    IScopedMessenger messenger,
    IDbContextFactory<HubDbContext> dbContextFactory)
    : IFilterRunner
{
    private readonly string _filterRunnerId = filterRunnerId;
    private readonly ConnectorMetadata _metadata = metadata;
    private readonly PacketDtoService _packetDtoService = packetDtoService;
    private readonly ILogger<FilterRunner> _logger = logger;
    private readonly IScopedMessenger _messenger = messenger;
    private readonly IDbContextFactory<HubDbContext> _dbContextFactory = dbContextFactory;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _runnerTask;

    public bool IsRunning => _runnerTask is not null && !_runnerTask.IsCompleted;

    public async Task StartAsync(PacketRequestDto filter, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            await StopRunnerTask(cancellationToken);
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runnerTask = RunAsync(filter, _cancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await StopRunnerTask(cancellationToken);

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    public async Task RunAsync(PacketRequestDto filter, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await using var hubDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = _packetDtoService.CreateFilteredQuery(hubDbContext, filter).AsNoTracking();

        query = query.OrderByDescending(p => p.Id);

        var topCount = filter.Limit ?? 100;
        var lastId = long.MaxValue;
        var uiPackets = new List<PacketDto>();
        var ignoredIds = new HashSet<long>();
        var dataFilters = filter.ColumnFilters?.Where(x => x.ColumnName == nameof(PacketDto.Data)).ToImmutableArray() ?? [];
        var previewDataFilters = filter.ColumnFilters?.Where(x => x.ColumnName == nameof(PacketDto.PreviewData)).ToImmutableArray() ?? [];


        _logger.LogInformation("Started Filter Runner instance {connectorName}:{filterRunnerId}.", _metadata.TemplateName, _filterRunnerId);

        do
        {
            try
            {
                var packetsBatch = await query.Where(p => p.Id < lastId).Take(topCount).ToArrayAsync(cancellationToken);
                if (packetsBatch.Length == 0)
                {
                    break;
                }

                lastId = packetsBatch[^1].Id;

                foreach (var packet in packetsBatch)
                {
                    if (ignoredIds.Contains(packet.Id))
                    {
                        continue;
                    }

                    var uiPacket = await _packetDtoService.BuildDtoAsync(packet);

                    var matches = dataFilters.Matches(uiPacket.Data) && previewDataFilters.Matches(uiPacket.PreviewData);

                    if (!matches)
                    {
                        continue;
                    }

                    ignoredIds.Add(packet.Id);
                    uiPackets.Add(uiPacket);
                }

                if (uiPackets.Count >= topCount)
                {
                    var packetsToSend = uiPackets.Take(topCount).ToList();
                    uiPackets.RemoveRange(0, topCount);

                    await _packetDtoService.FillChildAndParentPacketsAsync(hubDbContext, packetsToSend, ignoredIds);

                    var packetWrapper = new PacketWrapperDto
                    {
                        ConnectorIdentifier = _metadata.ConnectorIdentifier,
                        Packets = [.. packetsToSend]
                    };

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var alive = await _messenger.AskAsync<PacketWrapperDto, bool>(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), packetWrapper).FirstOrDefaultResponse();
                        if (!alive)
                        {
                            await (_cancellationTokenSource?.CancelAsync() ?? Task.CompletedTask);
                            break;
                        }
                    }
                }

                await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered error while executing Filter Runner.");
            }

        } while (!cancellationToken.IsCancellationRequested);

        if (uiPackets.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            await _packetDtoService.FillChildAndParentPacketsAsync(hubDbContext, uiPackets, ignoredIds);
            var packetWrapper = new PacketWrapperDto
            {
                ConnectorIdentifier = _metadata.ConnectorIdentifier,
                Packets = [.. uiPackets]
            };

            await _messenger.SendAsync(ConnectorContract.PushFilteredPacketsTopic(_filterRunnerId), packetWrapper);
        }

        await _messenger.SendAsync(ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerId), _metadata.ConnectorIdentifier);
        _logger.LogInformation("Stopped Filter Runner instance {connectorName}:{filterRunnerId}.", _metadata.TemplateName, _filterRunnerId);
    }

    private async Task StopRunnerTask(CancellationToken cancellationToken = default)
    {
        if (_cancellationTokenSource is { IsCancellationRequested: false })
        {
            await _cancellationTokenSource.CancelAsync();
        }

        if (_runnerTask is not null)
        {
            try
            {
                await _runnerTask.WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch (OperationCanceledException) { /* expected */ }
        }
    }
}
