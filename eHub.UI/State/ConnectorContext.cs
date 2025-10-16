using System.Collections.Frozen;
using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eMessenger;

namespace eHub.UI.State;

public class ConnectorContext(
    ConnectorIdentifier connectorIdentifier,
    ConnectorUiData connectorUiData,
    IMessenger messenger) : IConnectorContext
{
    private readonly ConnectorIdentifier _connectorIdentifier = connectorIdentifier;
    private readonly ConnectorUiData _connectorUiData = connectorUiData;
    private readonly IMessenger _messenger = messenger;

    private IReadOnlyDictionary<string, Type>? _customFilters;
    private IRegistrationToken _registrationToken = NullRegistrationToken.Instance;
    private bool _disposed = false;

    public event OnConnectorPacketsChangedDelegate? OnConnectorPacketsChanged;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _registrationToken += await _messenger.ListenAsync<ConnectorPacketsChangedDto>(
            ConnectorContract.PacketsChangedTopic(_connectorIdentifier),
            ConnectorPacketsChanged);

        var customFilters = await _messenger.AskAsync<IReadOnlyDictionary<string, string>>(
            ConnectorContract.GetCustomFiltersTopic(_connectorIdentifier))
            .FirstOrDefaultResponse();

        _customFilters = customFilters?.ToDictionary(kvp => kvp.Key, kvp => Type.GetType(kvp.Value) ?? typeof(string)).AsReadOnly();
    }

    public IReadOnlyDictionary<string, Type> GetCustomFilters() => _customFilters ?? FrozenDictionary<string, Type>.Empty;

    public UIViewConfig GetUIViewConfig() => _connectorUiData.UIViewConfig;

    public async Task StopPacketsAsync(HashSet<PacketDto> packets)
    {
        var packetsToStop = packets
            .Where(p => !p.Status.IsProcessed())
            .Select(p => p.Id)
            .ToImmutableArray();

        await _messenger.SendAsync(ConnectorContract.PacketManualStopSequenceTopic(_connectorIdentifier), packetsToStop);
    }

    public async Task DeletePacketsAsync(HashSet<PacketDto> packets)
    {
        var packetsToDelete = packets
            .Select(p => p.Id)
            .ToImmutableArray();

        await _messenger.SendAsync(ConnectorContract.PacketDeleteSequenceTopic(_connectorIdentifier), packetsToDelete);
    }

    public async Task ResendPacketsAsync(HashSet<PacketDto> packets)
    {
        var packetsToResend = packets
            .Select(packet => new PacketResendDto(packet.Id, packet.Data))
            .ToImmutableArray();

        await _messenger.SendAsync(ConnectorContract.PacketResendTopic(_connectorIdentifier), packetsToResend);
    }

    public async Task<bool> ImportPacketsAsync(ConnectorPacketsExportDto importData, bool forceImport)
    {
        return await _messenger.AskAsync<ConnectorPacketsTryImportDto, bool>(
            ConnectorContract.PacketTryImportTopic(_connectorIdentifier),
            new ConnectorPacketsTryImportDto(importData, ForceImport: forceImport))
            .FirstOrDefaultResponse();
    }

    private void ConnectorPacketsChanged(ConnectorPacketsChangedDto changed)
    {
        OnConnectorPacketsChanged?.Invoke(_connectorIdentifier, changed);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _registrationToken.DisposeAsync();
        _disposed = true;
    }
}
