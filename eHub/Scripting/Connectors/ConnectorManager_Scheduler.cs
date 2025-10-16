using eHub.Config;
using ElementLogic.Configuration.Api;
using ElementLogic.Configuration.Configurations;
using ElementLogic.Configuration.Schema;
using eScheduler.Library.Abstractions;
using eScheduler.Library.Config;

namespace eHub.Scripting.Connectors;

partial class ConnectorManager
{
    private readonly Dictionary<string, ConfigurationUnitLocator> _registeredSchedulerUnits = [];

    private string GetSchedulerConfigPath(string templateName, ConnectorTemplate connectorTemplate)
    {
        var schedulerPath = connectorTemplate.Scheduler?[$"{nameof(SchedulerOptions.DataPath)}"];
        var instanceName = connectorTemplate.Scheduler?[$"{nameof(SchedulerOptions.InstanceName)}"];

        if (connectorTemplate.Scheduler is null || schedulerPath is null || instanceName is null)
        {
            var connectorDirectory = Path.Combine(_registry.AppConfigFolder.FullName, templateName);
            if (schedulerPath is null && !Directory.Exists(connectorDirectory))
            {
                Directory.CreateDirectory(connectorDirectory);
            }

            schedulerPath ??= Path.Combine(connectorDirectory, $"SchedulerConfig.{templateName}.json");
            instanceName ??= templateName;

            var schedulerConfig = new ConfigurationBuilder()
                .AddInMemoryCollection([
                    new($"{SchedulerOptions.DefaultKey}:{nameof(SchedulerOptions.DataPath)}", schedulerPath),
                    new($"{SchedulerOptions.DefaultKey}:{nameof(SchedulerOptions.InstanceName)}", instanceName)])
                .Build();

            connectorTemplate.Scheduler = schedulerConfig.GetSection(SchedulerOptions.DefaultKey);
        }

        return schedulerPath;
    }

    private async Task AddSchedulerConfiguration(string templateName, string filePath)
    {
        if (!_registeredSchedulerUnits.TryGetValue(templateName, out _))
        {
            var registeredUnit = await _registry.AddJsonFileSource(filePath, EffortlessSchema.FromType(typeof(ScheduledTaskConfig[])).Schema);
            _registeredSchedulerUnits[templateName] = registeredUnit;
        }
    }

    private async Task UnregisterSchedulerConfigurations()
    {
        await Task.WhenAll(_registeredSchedulerUnits.Select((kvp) => _registry.RemoveConfigurationUnitAsync(kvp.Value)));
    }
}
