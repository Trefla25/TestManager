using System.ComponentModel.DataAnnotations;

namespace eHub.PlugIn;

/// <summary></summary>
public enum PacketStatus
{
    /// <summary></summary>
    [Display(Name = "Enqueued")]
    Enqueued = 1,
    /// <summary></summary>
    [Display(Name = "Processed")]
    Processed = 2,
    /// <summary></summary>
    [Display(Name = "Error")]
    Error = 3,
    /// <summary></summary>
    [Display(Name = "FatalError")]
    FatalError = 4,
    /// <summary></summary>
    [Display(Name = "InProgress")]
    InProgress = 5,
    /// <summary></summary>
    [Display(Name = "ManualStop")]
    ManualStop = 6,
    /// <summary></summary>
    [Obsolete("Use Enqueued instead")]
    [Display(Name = "Received")]
    Received = Enqueued
}

/// <summary></summary>
public static class PacketStatusExtensions
{
    /// <summary></summary>
    /// <param name="status"></param>
    public static bool IsProcessed(this PacketStatus status)
    {
        return status == PacketStatus.Processed
            || status == PacketStatus.FatalError
            || status == PacketStatus.ManualStop;
    }
}