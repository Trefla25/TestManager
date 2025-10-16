using System.Diagnostics.CodeAnalysis;

namespace eHub.Scripting.Connectors.Services;

public interface IFilterRunnerProvider
{
    IFilterRunner GetOrAddFilterRunner(string filterRunnerId);
    bool TryGetFilterRunner(string filterRunnerId, [NotNullWhen(true)] out IFilterRunner? filterRunner);
}
