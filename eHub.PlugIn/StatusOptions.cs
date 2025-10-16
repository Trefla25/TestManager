using System.ComponentModel;

namespace eHub.PlugIn;

/// <summary>
/// Contains options how to handle/view the status information form a connector.
/// </summary>
public struct StatusOptions
{
    /// <summary>
    /// Contains options how to handle/view the status information form a connector.
    /// </summary>
    public StatusOptions()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public ElColor Color { get; set; } = ElColor.Inherit;

    /// <summary>
    /// Sets the color how the information should be shown.
    /// </summary>
    public enum ElColor : ushort
    {
        [Description("default")]
        Default,
        [Description("primary")]
        Primary,
        [Description("secondary")]
        Secondary,
        [Description("tertiary")]
        Tertiary,
        [Description("info")]
        Info,
        [Description("success")]
        Success,
        [Description("warning")]
        Warning,
        [Description("error")]
        Error,
        [Description("dark")]
        Dark,
        [Description("transparent")]
        Transparent,
        [Description("inherit")]
        Inherit,
        [Description("surface")]
        Surface
    }
}
