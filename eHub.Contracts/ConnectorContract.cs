using System.Collections.Immutable;
using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;

namespace eHub.Contracts;

public static partial class ConnectorContract
{
    public const string EHubGetOverview = "..."; // TODO

    private const string TopicConnectorUiGet = "ehub.actions.{0}_ui_get";
    private const string TopicConnectorUIPacketsGet = "ehub.actions.{0}_ui_packets_get";
    private const string TopicConnectorPacketsChanged = "ehub.events.{0}_packetsChanged";
    private const string TopicConnectorPacketExport = "ehub.actions.{0}_export";
    private const string TopicConnectorPacketTryImport = "ehub.actions.{0}_tryImport";
    private const string TopicConnectorPacketResend = "ehub.actions.{0}_resend";
    private const string TopicConnectorPacketManualStopSequence = "ehub.actions.{0}_stopSequence";
    private const string TopicConnectorPacketDeleteSequence = "ehub.actions.{0}_deleteSequence";
    private const string TopicConnectorStartFilterRunner = "ehub.actions.{0}_start_filter_runner";
    private const string TopicConnectorStopFilterRunner = "ehub.actions.{0}_stop_filter_runner";
    private const string TopicConnectorPushFilteredPackets = "ehub.actions.push_filtered_packets_{0}";
    private const string TopicConnectorFilterRunnerStoppedNotification = "ehub.actions.filter_runner_stopped_notification_{0}";
    private const string TopicConnectorGetCustomFilters = "ehub.actions.{0}_get_custom_filters";

    public static string EHub_Action_Start_Connector(string eHubInstance) => $"ehub.actions.start_connector.{eHubInstance}";
    public static string EHub_Action_Stop_Connector(string eHubInstance) => $"ehub.actions.stop_connector.{eHubInstance}";
    public static string EHub_RequestConnectorSummary(string eHubInstance) => $"ehub.requestSingleInstance.{eHubInstance}";
    public static string EHub_RequestConnectorStatus(string eHubInstance) => $"ehub.requestStatus.{eHubInstance}";
    public static string EHub_RequestControllerRouteModels(string eHubInstance) => $"ehub.requestControllerRouteModels.{eHubInstance}";
    public static string ConnectorStartedTopic() => "ehub.events.connector_started";
    public static string ConnectorStoppedTopic() => "ehub.events.connector_stopped";
    public static string PollAliveConnectorsTopic() => "ehub.actions.connector_alive_check";
    public static string UIGetTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorUiGet, connectorName.ToString());
    public static string UIPacketsGetTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorUIPacketsGet, connectorName.ToString());
    public static string PacketsChangedTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorPacketsChanged, connectorName.ToString());
    public static string PacketExportTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorPacketExport, connectorName.ToString());
    public static string PacketTryImportTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorPacketTryImport, connectorName.ToString());
    public static string PacketManualStopSequenceTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorPacketManualStopSequence, connectorName.ToString());
    public static string PacketResendTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorPacketResend, connectorName.ToString());
    public static string PacketDeleteSequenceTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorPacketDeleteSequence, connectorName.ToString());
    public static string StartFilterRunnerTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorStartFilterRunner, connectorName.ToString());
    public static string StopFilterRunnerTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorStopFilterRunner, connectorName.ToString());
    public static string PushFilteredPacketsTopic(string filterRunnerIdentifier) => string.Format(TopicConnectorPushFilteredPackets, filterRunnerIdentifier);
    public static string FilterRunnerStoppedNotification(string filterRunnerIdentifier) => string.Format(TopicConnectorFilterRunnerStoppedNotification, filterRunnerIdentifier);
    public static string GetCustomFiltersTopic(ConnectorIdentifier connectorName) => string.Format(TopicConnectorGetCustomFilters, connectorName.ToString());
}

public record ConnectorStartedEventDto(ConnectorIdentifier Identifier, ConnectorUiData Ui);
public record ConnectorStoppedEventDto(ConnectorIdentifier Identifier);
public record ConnectorKeepAliveDto(ConnectorIdentifier Identifier);
public record ConnectorUiData(string ConnectorName, string ConnectorType, UIViewConfig UIViewConfig);

public record PacketRequestDto(
    DateTime? DateTimeStart = null,
    DateTime? DateTimeEnd = null,
    long? MinId = null,
    long? MaxId = null,
    int? Limit = null,
    ImmutableArray<string>? Channels = null,
    ImmutableArray<ColumnFilterDto>? ColumnFilters = null)
{
    public bool IsEmpty => DateTimeStart > DateTimeEnd || MinId > MaxId;
}

public record ConnectorPacketsFilterDto(
    DateTime? DateTimeStart = null,
    DateTime? DateTimeEnd = null,
    long? MinId = null,
    long? MaxId = null,
    ImmutableArray<string>? Channels = null)
{
    public static readonly ConnectorPacketsFilterDto All = new();
    public static readonly ConnectorPacketsFilterDto None = new(
        DateTime.MaxValue,
        DateTime.MinValue,
        long.MaxValue,
        long.MinValue,
        ImmutableArray<string>.Empty);

    public PacketRequestDto ToRequestWith(int? limit, ImmutableArray<ColumnFilterDto>? columnFilters) => new(
        DateTimeStart: DateTimeStart,
        DateTimeEnd: DateTimeEnd,
        MinId: MinId,
        MaxId: MaxId,
        Limit: limit,
        Channels: Channels,
        ColumnFilters: columnFilters);

    public ConnectorPacketsFilterDto Intersect(ConnectorPacketsFilterDto other)
    {
        var intersectStart = IntersectMax(DateTimeStart, other.DateTimeStart);
        var intersectEnd = IntersectMin(DateTimeEnd, other.DateTimeEnd);
        var intersectMinId = IntersectMax(MinId, other.MinId);
        var intersectMaxId = IntersectMin(MaxId, other.MaxId);

        ImmutableArray<string>? intersectChannels;
        if (!Channels.HasValue) { intersectChannels = other.Channels; }
        else if (!other.Channels.HasValue) { intersectChannels = Channels; }
        else { intersectChannels = [..Channels.Value.Intersect(other.Channels.Value)]; }

        return new ConnectorPacketsFilterDto(
            intersectStart,
            intersectEnd,
            intersectMinId,
            intersectMaxId,
            intersectChannels);

        static T? IntersectMin<T>(T? a, T? b) where T : struct => new T?[] { a, b }.Min();
        static T? IntersectMax<T>(T? a, T? b) where T : struct => new T?[] { a, b }.Max();
    }

    public ConnectorPacketsFilterDto Union(ConnectorPacketsFilterDto other)
    {
        var unionStart = UnionMin(DateTimeStart, other.DateTimeStart);
        var unionEnd = UnionMax(DateTimeEnd, other.DateTimeEnd);
        var unionMinId = UnionMin(MinId, other.MinId);
        var unionMaxId = UnionMax(MaxId, other.MaxId);

        ImmutableArray<string>? unionChannels;
        if (!Channels.HasValue || !other.Channels.HasValue) { unionChannels = null; }
        else { unionChannels = [..Channels.Value.Union(other.Channels.Value)]; }

        return new ConnectorPacketsFilterDto(
            unionStart,
            unionEnd,
            unionMinId,
            unionMaxId,
            unionChannels);

        static T? UnionMin<T>(T? a, T? b) where T : struct => (!a.HasValue || !b.HasValue) ? null : new T?[] { a, b }.Min();
        static T? UnionMax<T>(T? a, T? b) where T : struct => (!a.HasValue || !b.HasValue) ? null : new T?[] { a, b }.Max();
    }
}

public record ConnectorPacketsTryImportDto(ConnectorPacketsExportDto Export, bool ForceImport);

public record ConnectorPacketsExportDto(ImmutableArray<PacketDto> Packets);

public record ConnectorPacketsChangedDto(ConnectorPacketsFilterDto FilterHint)
{
    public static readonly ConnectorPacketsChangedDto Any = new(ConnectorPacketsFilterDto.All);
}

public record ColumnFilterDto(string ColumnName, ColumnFilterOperator Operator, string? Value);

public record FilterRunnerRequest(PacketRequestDto PacketRequestDto, string FilterRunnerIdentifier);

public enum ColumnFilterOperator
{
    Equal,
    NotEqual,
    Contains,
    NotContains,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    StartsWith,
    EndsWith,
    Empty,
    NotEmpty
}
