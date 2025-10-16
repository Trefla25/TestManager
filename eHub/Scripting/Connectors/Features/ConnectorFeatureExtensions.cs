namespace eHub.Scripting.Connectors.Features;

public static class ConnectorFeatureExtensions
{
    public static IServiceCollection AddConnectorFeature<T>(this IServiceCollection services)
        where T : class, IConnectorFeature
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        if (typeof(T) == typeof(IConnectorFeature) || typeof(T).IsAbstract || typeof(T).IsInterface)
        {
            throw new ArgumentException("Cannot add IConnectorFeature directly. Use a concrete implementation instead.");
        }
        return services
                .AddSingleton<T>()
                .AddSingleton<IConnectorFeature>(x => x.GetRequiredService<T>());
    }
}
