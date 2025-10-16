namespace eHub.PlugIn.UI;

/// <summary>
/// Provides a fluent API for configuring custom packet filters that extend the default UI packet columns filters
/// (e.g. <see cref="PacketData.Id"/>, <see cref="PacketData.BinaryData"/>, <see cref="PacketData.Status"/>).
/// </summary>
public interface ICustomFilterBuilder
{
    /// <summary>
    /// Adds a custom filter definition.
    /// The type parameter <typeparamref name="T"/> determines which filtering operators will be available in the UI.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the filter value.
    /// </typeparam>
    /// <param name="filterKey">
    /// A unique key used to identify the filter. This key must not match any of the standard packet column names
    /// (e.g. <see cref="PacketData.Id"/>, <see cref="PacketData.BinaryData"/>, <see cref="PacketData.Status"/>).
    /// </param>
    /// <param name="sqlExpression">
    /// A SQL expression that defines how to extract or compute the filtered value from packet.
    /// </param>
    /// <param name="valueBuilder">
    /// An optional function that transforms the raw string input into the final value used by the SQL parameter.
    /// </param>
    /// <returns>
    /// The current <see cref="ICustomFilterBuilder"/> instance to allow chaining additional custom filter definitions.
    /// </returns>
    ICustomFilterBuilder AddCustomFilter<T>(string filterKey, string sqlExpression, Func<T?,T?>? valueBuilder = null) where T : IConvertible;
}
