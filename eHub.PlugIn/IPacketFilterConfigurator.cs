using eHub.PlugIn.UI;

namespace eHub.PlugIn;

/// <summary>
/// Provides a mechanism for configuring additional filters on packet data that will be exposed in the UI.
/// </summary>
public interface IPacketFilterConfigurator : IPacketTransfer
{
    /// <summary>
    /// Configures additional custom filters for packets, besides the standard column filters.
    /// Use the provided <see cref="ICustomFilterBuilder"/> to define filter conditions that can later be applied 
    /// from the UI, allowing users to search or filter packets with more advanced conditions.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="ICustomFilterBuilder"/> instance that enables the fluent configuration of custom filter definitions.
    /// </param>
    void ConfigureCustomFilters(ICustomFilterBuilder builder);
}
