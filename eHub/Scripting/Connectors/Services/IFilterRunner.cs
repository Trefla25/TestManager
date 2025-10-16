using eHub.Contracts;

namespace eHub.Scripting.Connectors.Services;

public interface IFilterRunner
{
    Task StartAsync(PacketRequestDto filter, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
