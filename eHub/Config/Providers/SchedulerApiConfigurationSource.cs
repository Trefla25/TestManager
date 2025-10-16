namespace eHub.Config.Providers;

public class SchedulerApiConfigurationSource(IConfigurationRoot configuration) : IConfigurationSource
{
    private readonly IConfigurationRoot _configuration = configuration;
    private SchedulerApiConfigurationProvider? _provider;

    public SchedulerApiConfigurationProvider GetProvider() => _provider!;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        _provider = new SchedulerApiConfigurationProvider(_configuration);
        return _provider;
    }
}
