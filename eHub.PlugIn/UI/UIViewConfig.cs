using System.Text.Json.Serialization;

namespace eHub.PlugIn.UI;

#pragma warning disable CS8618 // Disable nullability warning
/// <summary>Configurations for the UI.</summary>
public class UIViewConfig
{
    /// <summary>Represents the type identifier for custom UI views. </summary>
    public const string CustomUIViewType = "Custom";
    /// <summary></summary>
    public string? Name { get; set; }
    /// <summary>Collection of all view tabs displayed in the UI.</summary>
    public Dictionary<string, ViewConfig> Views { get; set; } = [];
    ///<summary>Collection of all column configurations inside all data grids</summary>
    public Dictionary<string, ColumnConfig>? Columns { get; set; }
    /// <summary>Only required for custom UI view configurations.</summary>
    public IEnumerable<string> Connectors => Views.Values
                                        .SelectMany(x => x.Channels)
                                        .Select(x => x.Connector)
                                        .Where(x => x != null)
                                        .Distinct();
    /// <summary></summary>
    public UIFilterConfig Filter { get; set; }
    /// <summary>The ammount of packets fetched in live mode, as well as the ammount of packets per page in offline mode.</summary>
    public int ViewPacketCount { get; set; } = 100;
}

/// <summary>Configurations of a UI view tab. List of all connectors used in this view configuration.</summary>
public class ViewConfig
{
    /// <summary>The name of the view.</summary>
    public string Name { get; set; }
    /// <summary>Dictionary containing translations for the displayed view label in multiple languages.</summary>
    public Dictionary<string, string> DisplayName { get; set; }
    /// <summary>List of the connectors that will have this particular view. If not specified, the view will be visible for all connectors.</summary>
    public List<string> Connectors { get; set; }
    /// <summary>Configurations of how the packet data will be displayed.</summary>
    public DataDisplayConfig DataDisplay { get; set; } = new() { Format = DisplayFormat.Dialog, RowCount = 1 };
    /// <summary>List of channel data grid configurations.</summary>
    public List<ChannelConfig> Channels { get; set; } = [];
    ///<summary>Collection of all column configurations inside all data grids in this view.Can be omitted to keep the column configurations defined by<see cref="UIViewConfig.Columns"/>.</summary>
    public Dictionary<string, ColumnConfig> Columns { get; set; }

    /// <summary></summary>
    [Obsolete("Use DataDisplay.Format instead")]
    public DisplayFormat? DataDisplayFormat { get => DataDisplay?.Format; set { } }
    /// <summary></summary>
    [Obsolete("Use DataDisplay.RowCount instead")]
    public int? DataRowCount { get => DataDisplay?.RowCount; set { } }
}

/// <summary>Configurations for a channel data grid.</summary>
public class ChannelConfig
{
    /// <summary>The name of the channel data grid.</summary>
    public string Name { get; set; }
    /// <summary>Specifies the connector to which this channel belongs.</summary>
    public string Connector { get; set; }
    /// <summary>Dictionary containing translations for the displayed data grid title in multiple languages.</summary>
    public Dictionary<string, string> DisplayName { get; set; }
    /// <summary>The channel displayed in this data grid.</summary>
    public string Channel { get; set; }
    /// <summary>Configurations of how the packet data will be displayed.Can be omitted to keep the display configurations defined by <see cref="ViewConfig.DataDisplay"/>.</summary>
    public DataDisplayConfig? DataDisplay { get; set; }
    /// <summary>The position of the channel data grid based on row and column indices.</summary>
    public Position Position { get; set; }
    /// <summary>Collection of all column configurations inside this channel data grid. Can be omitted to keep the column configurations defined by <see cref="ViewConfig.Columns"/>.</summary>
    public Dictionary<string, ColumnConfig> Columns { get; set; }

    /// <summary></summary>
    [Obsolete("Use DataDisplay.Format instead")]
    public DisplayFormat? DataDisplayFormat { get => DataDisplay?.Format; set { } }
    /// <summary></summary>
    [Obsolete("Use DataDisplay.RowCount instead")]
    public int? DataRowCount { get => DataDisplay?.RowCount; set { } }
}

/// <summary>Configurations of a channel data grid column.</summary>
public class ColumnConfig
{
    /// <summary>Whether the column should be visible or not.</summary>
    public bool Visible { get; set; } = false;
    /// <summary>The format in which the column data is displayed.</summary>
    public string? Format { get; set; }
    /// <summary>The default sorting direction of the rows by this columns. Either "Ascending" or "Descending". If not specified the column is not sorted by default.</summary>
    public string? SortDirection { get; set; }
    /// <summary>The width of the column in pixels.</summary>
    public int? Width { get;set; }
}

/// <summary>Configurations of how the packet data data is displayed.</summary>
public class DataDisplayConfig
{
    /// <summary>The format in wich the packet data will be displayed.</summary>
    public DisplayFormat? Format { get; set; }
    /// <summary>The number of visible rows without scrolling.</summary>
    /// <remarks>
    /// Only required for scroll display formats:
    /// <list type="bullet">
    /// <item><see cref="DisplayFormat.MultiLineScroll"/></item>
    /// <item><see cref="DisplayFormat.XmlScroll"/></item>
    /// <item><see cref="DisplayFormat.JsonScroll"/></item>
    /// </list>
    /// </remarks>
    public int? RowCount { get; set; }
    /// <summary>The path to the XML or JSON element displayed as preview.</summary>
    /// <remarks>
    /// Only required for dialog preview display formats:
    /// <list type="bullet">
    /// <item><see cref="DisplayFormat.XmlDialogPreview"/></item>
    /// <item><see cref="DisplayFormat.JsonDialogPreview"/></item>
    /// </list>
    /// </remarks>
    [Obsolete("The preview path shown is now obtained via the PacketConverter")]
    public string? PreviewPath { get; set; }
    /// <summary>The substring indices of the packet data displayed as preview.</summary>
    /// <remarks>
    /// Only required for dialog preview display formats:
    /// <list type="bullet">
    /// <item><see cref="DisplayFormat.XmlDialogPreview"/></item>
    /// <item><see cref="DisplayFormat.JsonDialogPreview"/></item>
    /// </list>
    /// </remarks>
    [Obsolete("The preview path shown is now obtained via the PacketConverter")]
    public SubstringInfo? PreviewSubstring { get; set; }
}

/// <summary>Display formats for the packet data.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisplayFormat
{
    /// <summary>Single line.</summary>
    SingleLine,
    /// <summary>Multiple lines.</summary>
    MultiLineFull,
    /// <summary>Multiple lines with scroll.</summary>
    MultiLineScroll,
    /// <summary>Dialog.</summary>
    Dialog,
    /// <summary>XML on multiple lines.</summary>
    XmlFull,
    /// <summary>XML on multiple lines with scroll.</summary>
    XmlScroll,
    /// <summary>JSON on multiple lines.</summary>
    JsonFull,
    /// <summary>JSON on multiple lines with scroll.</summary>
    JsonScroll,
    /// <summary>XML dialog.</summary>
    [Obsolete("Use Dialog instead")]
    XmlDialog = Dialog,
    /// <summary>Preview XML outside dialog.</summary>
    [Obsolete("Use Dialog instead")]
    XmlDialogPreview = Dialog,
    /// <summary>JSON dialog.</summary>
    [Obsolete("Use Dialog instead")]
    JsonDialog = Dialog,
    /// <summary>Preview JSON outside dialog.</summary>
    [Obsolete("Use Dialog instead")]
    JsonDialogPreview = Dialog,
}

/// <summary>Extensions for <see cref="DisplayFormat"/></summary>
public static class DisplayFormatExtensions
{
    /// <summary>Checks whether the display format has scroll.</summary>
    /// <param name="format"></param>
    public static bool IsScroll(this DisplayFormat format) => format
        is DisplayFormat.MultiLineScroll
        or DisplayFormat.XmlScroll
        or DisplayFormat.JsonScroll;

    /// <summary>Checks whether the display format is of simple text format.</summary>
    /// <param name="format"></param>
    public static bool IsRawText(this DisplayFormat format) => format
        is DisplayFormat.SingleLine
        or DisplayFormat.MultiLineFull
        or DisplayFormat.MultiLineScroll;

    /// <summary>Checks whether the display format is of XML format.</summary>
    /// <param name="format"></param>
    public static bool IsXml(this DisplayFormat format) => format
        is DisplayFormat.XmlFull
        or DisplayFormat.XmlScroll;

    /// <summary>Checks whether the display format is of JSON format.</summary>
    /// <param name="format"></param>
    public static bool IsJson(this DisplayFormat format) => format
        is DisplayFormat.JsonFull
        or DisplayFormat.JsonScroll;

    /// <summary>Checks whether the display format is of dialog format.</summary>
    /// <param name="format"></param>
    public static bool IsDialog(this DisplayFormat format) => format
        is DisplayFormat.Dialog;

    /// <summary>Checks whether the display format is of dialog with preview format.</summary>
	/// <param name="format"></param>
    [Obsolete("Use IsDialog instead")]
	public static bool IsDialogPreview(this DisplayFormat format) => format
        is DisplayFormat.Dialog;
}

/// <summary></summary>
public class UIFilterConfig
{
    /// <summary></summary>
    public TimeSpan StartDateTimeOffset { get; set; }
    /// <summary></summary>
    public TimeSpan EndDateTimeOffset { get; set; }
}

/// <summary>Position of the data grid based on row and column indices.</summary>
/// <param name="RowIndex"></param>
/// <param name="ColIndex"></param>
public record Position(int RowIndex, int ColIndex);

/// <summary>Substring start and end indices.</summary>
/// <param name="StartIndex">The substring start index.</param>
/// <param name="EndIndex">The substring end index.</param>
public record SubstringInfo(int StartIndex, int EndIndex);

#pragma warning restore CS8618
