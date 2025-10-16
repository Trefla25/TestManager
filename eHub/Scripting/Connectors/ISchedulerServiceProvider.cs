using System.Diagnostics.CodeAnalysis;
using eScheduler.Library.Abstractions;

namespace eHub.Scripting.Connectors;

public interface ISchedulerServiceProvider
{
    public bool TryGetSchedulerService(string connectorName, [NotNullWhen(true)] out ISchedulerService? schedulerService);
}
