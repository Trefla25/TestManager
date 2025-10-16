using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using eController.Util.DependencyInjection;
using eHub.Config;
using eHub.PlugIn;
using ElementLogic.Configuration.Api;
using ElementLogic.Configuration.Configurations;
using ElementLogic.Configuration.Schema;
using Json.Schema;
using Json.Schema.Generation;

namespace eHub.Scripting.Connectors;

partial class ConnectorManager
{
    private ImmutableArray<ConfigurationUnitLocator> _registeredUnits = [];
    private JsonSchema? _configurationSchema;

    private async Task RefreshConfigs(IEnumerable<string> exposedConfigFiles)
    {
        // TODO improve diffing
        await UnregisterAllUnits();

        GenerateConfigurationSchema();

        _registeredUnits = (await Task.WhenAll(
            exposedConfigFiles
            .Select(async file => await _registry.AddJsonFileSource(file, _configurationSchema))))
            .ToImmutableArray();
    }

    [MemberNotNull(nameof(_configurationSchema))]
    private void GenerateConfigurationSchema()
    {
        var connectorTypes = _pluginPublisher.Plugins
            .SelectMany(x => x.Assemblies)
            .SelectMany(x => x.ExportedTypes)
            .Where(x => x.IsClass && !x.IsAbstract && x.IsAssignableTo(typeof(IConnector)))
            .ToList();

        if (connectorTypes.Count == 0)
        {
            _configurationSchema = JsonSchema.Empty;
            return;
        }

        var availableConnectorTypesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum(connectorTypes.SelectMany(x => new[] { x.Name, x.FullName }).SelectNotNull(x => x))
            .Build();

        var availableTypes = connectorTypes.Select(GenerateSchemaConnectorConfig);

        _configurationSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(
                new JsonSchemaBuilder()
                .Title("Connector Template")
                .OneOf(availableTypes)
            )
            .Build();
    }

    private static JsonSchema GenerateSchemaConnectorConfig(Type connectorType)
    {
        var typeName = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum(new[] { connectorType.Name, connectorType.FullName }.SelectNotNull(x => x))
            .Build();

        var schema = new JsonSchemaBuilder()
            .Title(connectorType.FullName ?? connectorType.Name)
            .Properties(
                (nameof(ConnectorTemplate.Type), typeName),
                (nameof(ConnectorTemplate.Config), GenerateSchemaForMainOptionType(connectorType)),
                (nameof(ConnectorTemplate.Enabled), new JsonSchemaBuilder().FromType(typeof(bool)))
            )
            .Build();

        return schema;
    }

    private static JsonSchema GenerateSchemaForMainOptionType(Type connectorType)
    {
        var injectionInfo = InjectionInfo.OfType(connectorType);
        var optionType = ScriptActivator.RequestedOptionTypes(injectionInfo).FirstOrDefault();

        if (optionType == null)
        {
            return JsonSchema.Empty;
        }

        var schema = EffortlessSchema.FromType(optionType).Schema;

        //var schema = new JsonSchemaBuilder()
        //    .FromType(optionType, DefaultValidators.Configuration)
        //    .Build();

        return schema;
    }

    private Task UnregisterAllUnits()
        => Task.WhenAll(_registeredUnits.Select(_registry.RemoveConfigurationUnitAsync));
}
