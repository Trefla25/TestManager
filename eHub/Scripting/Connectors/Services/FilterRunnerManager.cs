using System.Diagnostics.CodeAnalysis;
using eController.Util;
using eHub.Database;
using eMessenger;
using Microsoft.EntityFrameworkCore;

namespace eHub.Scripting.Connectors.Services;

public class FilterRunnerManager : IFilterRunnerProvider, IAsyncDisposable
{
    private readonly ConnectorMetadata _metadata;
    private readonly PacketDtoService _packetDtoService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IScopedMessenger _messenger;
    private readonly IDbContextFactory<HubDbContext> _dbContextFactory;
    private readonly Dictionary<string, FilterRunner> _activeRunners = [];
    private readonly CancellationTokenSource _cleanerTokenSource;
    private readonly Task _cleanerTask;
    private readonly Lock _syncLock = new();
    private bool _disposed = false;

    public FilterRunnerManager(
        ConnectorMetadata metadata,
        PacketDtoService packetDtoService,
        ILoggerFactory loggerFactory,
        IScopedMessenger messenger,
        IDbContextFactory<HubDbContext> dbContextFactory)
    {
        _metadata = metadata;
        _packetDtoService = packetDtoService;
        _loggerFactory = loggerFactory;
        _messenger = messenger;
        _dbContextFactory = dbContextFactory;
        _cleanerTokenSource = new();
        _cleanerTask = FilterRunnersCleaner(_cleanerTokenSource.Token);
    }

    public bool TryGetFilterRunner(string filterRunnerId, [NotNullWhen(true)] out IFilterRunner? filterRunner)
    {
        lock (_syncLock)
        {
            if (_activeRunners.TryGetValue(filterRunnerId, out var runner))
            {
                filterRunner = runner;
                return true;
            }
        }

        filterRunner = null;
        return false;
    }

    public IFilterRunner GetOrAddFilterRunner(string filterRunnerId)
    {
        lock (_syncLock)
        {
            if (!_activeRunners.TryGetValue(filterRunnerId, out var filterRunner))
            {
                var logger = _loggerFactory.CreateLogger<FilterRunner>();
                filterRunner = new FilterRunner(filterRunnerId, _metadata, _packetDtoService, logger, _messenger, _dbContextFactory);

                _activeRunners.Add(filterRunnerId, filterRunner);
            }

            return filterRunner;
        }
    }

    public async Task FilterRunnersCleaner(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            lock (_syncLock)
            {
                var staleRunners = _activeRunners.Where(f => !f.Value.IsRunning);
                foreach (var staleRunner in staleRunners)
                {
                    _activeRunners.Remove(staleRunner.Key);
                }
            }

            await TaskUtil.DelayNoexept(TimeSpan.FromHours(1), cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var runner in _activeRunners.Values)
        {
            await runner.StopAsync();
        }

        await _cleanerTokenSource.CancelAsync();
        await _cleanerTask.WaitAsync(TimeSpan.FromMilliseconds(500));

        _cleanerTokenSource.Dispose();
    }
}
