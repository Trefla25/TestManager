using System.Collections.Frozen;
using eHub.Config;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.Database;
using eHub.Database.Models;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Configuration;
using eHub.Scripting.Connectors.Helper;
using Microsoft.EntityFrameworkCore;

namespace eHub.Scripting.Connectors.Services;

public class PacketDtoService
{
    private readonly ILogger<PacketDtoService> _logger;
    private readonly IPacketTransfer _packetTransfer;
    private readonly ConnectorMetadata _metadata;
    private readonly ConnectorTemplate _connectorTemplate;
    private readonly FrozenDictionary<string, bool> _canResendMap;
    private readonly IReadOnlyDictionary<string, CustomFilterDefinition> _customFilterDefinitions;

    public PacketDtoService(
        ILogger<PacketDtoService> logger,
        IPacketTransfer packetTransfer,
        ConnectorMetadata metadata,
        ConnectorTemplate connectorTemplate)
    {
        _logger = logger;
        _packetTransfer = packetTransfer;
        _metadata = metadata;
        _connectorTemplate = connectorTemplate;

        _canResendMap = (_connectorTemplate.PacketTransfer?.ChannelGroups ?? [])
            .SelectMany(group => group.Value.Channels
                .Select(channel => (Channel: channel, Group: group.Value)))
            .ToFrozenDictionary(item => item.Channel, item => item.Group.CanResend);

        if (_packetTransfer is IPacketFilterConfigurator filterConfigurator)
        {
            var customFilterBuilder = new CustomFilterBuilder();
            filterConfigurator.ConfigureCustomFilters(customFilterBuilder);

            _customFilterDefinitions = customFilterBuilder.GetCustomFilters();
        }
        else
        {
            _customFilterDefinitions = FrozenDictionary<string, CustomFilterDefinition>.Empty;
        }
    }

    public IReadOnlyDictionary<string, string> GetUICustomFilters()
        => _customFilterDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Type.ToString()).AsReadOnly();

    public async ValueTask<PacketDto> BuildDtoAsync(Packet packet)
    {
        // if the channel was not defined, canResend will be false
        var canResend = _canResendMap.GetValueOrDefault(packet.Channel, false);

        UIConversionInfo packetUIData;
        try
        {
            var pd = packet.ToData();

            packetUIData = await _packetTransfer.Converter.PacketToUIDataConverter(pd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert packet for UI, will use default conversion instead.");
            packetUIData = await ((IPacketConverter)StringConverter.Utf8Text).PacketToUIDataConverter(packet.ToData(), CancellationToken.None);
        }

        return packet.ToDto() with
        {
            Data = packetUIData.Data,
            PreviewData = packetUIData.DataPreview,
            DataType = packetUIData.DataType,
            CanResend = canResend,
            ConnectorName = _metadata.TemplateName
        };
    }

    public IQueryable<Packet> CreateFilteredQuery(HubDbContext dbContext, PacketRequestDto filter)
    {
        var conditionFragments = new List<string>();
        var parameters = new List<object?>();
        var index = 0;

        foreach (var columnFilter in filter.ColumnFilters ?? [])
        {
            // BinaryData and custom filters are queried with raw SQL
            if (_customFilterDefinitions.TryGetValue(columnFilter.ColumnName, out var def) || columnFilter.ColumnName == nameof(Packet.BinaryData))
            {
                var expression = def?.SqlExpression ?? "BinaryData";
                var type = def?.Type ?? typeof(string);
                string paramName = $"@p{index}";

                object? value;

                try
                {
                    value = Convert.ChangeType(columnFilter.Value, type);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert filter value '{Value}' to type '{Type}' for column '{ColumnName}'. Skipping this filter.", columnFilter.Value, type, columnFilter.ColumnName);
                    continue;
                }
                var buildValue = def?.ValueBuilder is not null
                ? def.ValueBuilder(value)
                : value;

                // Create a SQL condition fragment for this filter
                var (fragment, paramValue) = columnFilter.Operator switch
                {
                    ColumnFilterOperator.Contains => ($"{expression} LIKE {paramName}", $"%{buildValue}%"),
                    ColumnFilterOperator.NotContains => ($"{expression} NOT LIKE {paramName}", $"%{buildValue}%"),
                    ColumnFilterOperator.Equal when type == typeof(string) => ($"{expression} LIKE {paramName}", buildValue),
                    ColumnFilterOperator.Equal => ($"{expression} = {paramName}", buildValue),
                    ColumnFilterOperator.NotEqual when type == typeof(string) => ($"{expression} NOT LIKE {paramName}", buildValue),
                    ColumnFilterOperator.NotEqual => ($"{expression} <> {paramName}", buildValue),
                    ColumnFilterOperator.GreaterThan => ($"{expression} > {paramName}", buildValue),
                    ColumnFilterOperator.GreaterThanOrEqual => ($"{expression} >= {paramName}", buildValue),
                    ColumnFilterOperator.LessThan => ($"{expression} < {paramName}", buildValue),
                    ColumnFilterOperator.LessThanOrEqual => ($"{expression} <= {paramName}", buildValue),
                    ColumnFilterOperator.StartsWith => ($"{expression} LIKE {paramName}", $"{buildValue}%"),
                    ColumnFilterOperator.EndsWith => ($"{expression} LIKE {paramName}", $"%{buildValue}"),
                    ColumnFilterOperator.Empty => ($"COALESCE(TRIM({expression}), '') = ''", null),
                    ColumnFilterOperator.NotEmpty => ($"COALESCE(TRIM({expression}), '') <> ''", null),
                    _ => throw new InvalidOperationException($"Operator '{columnFilter.Operator}' is not supported.")
                };

                if (paramValue != null)
                {
                    parameters.Add(paramValue);
                    index++;
                }

                conditionFragments.Add(fragment);
            }
        }

        var query = conditionFragments.Count > 0
            ? dbContext.Packet.FromSqlRaw("SELECT * FROM Packet WHERE " + string.Join(" AND ", conditionFragments), [.. parameters])
            : dbContext.Packet;

        return query.ApplyFilterDto(filter);
    }

    public async Task FillChildAndParentPacketsAsync(HubDbContext dbContext, List<PacketDto> packets, HashSet<long> ignoredIds)
    {
        var parentIds = new HashSet<long>();
        var childIds = new HashSet<long>();

        foreach (var packetDto in packets)
        {
            if (packetDto.ParentId is long parentId && !ignoredIds.Contains(parentId))
            {
                parentIds.Add(parentId);
            }
            else if (packetDto.ParentId == null)
            {
                childIds.Add(packetDto.Id);
            }
        }

        // fetch parents
        var parentPackets = await dbContext.Packet
            .AsNoTracking()
            .Where(p => parentIds.Contains(p.Id) && !ignoredIds.Contains(p.Id))
            .ToArrayAsync();

        foreach (var parentPacket in parentPackets)
        {
            var uiPacket = await BuildDtoAsync(parentPacket);
            ignoredIds.Add(parentPacket.Id);
            packets.Add(uiPacket);
        }

        // fetch children
        var childPackets = await dbContext.Packet
            .AsNoTracking()
            .Where(p => p.ParentId != null && childIds.Contains(p.ParentId.Value) && !ignoredIds.Contains(p.Id))
            .ToArrayAsync();

        foreach (var childPacket in childPackets)
        {
            var uiPacket = await BuildDtoAsync(childPacket);
            ignoredIds.Add(childPacket.Id);
            packets.Add(uiPacket);
        }
    }
}
