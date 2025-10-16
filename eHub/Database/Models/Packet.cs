using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using eHub.PlugIn;

namespace eHub.Database.Models;

public class Packet
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    public byte[] BinaryData { get; set; } = [];
    public string? Metadata { get; set; }
    public required PacketStatus Status { get; set; }
    public required string Channel { get; set; }
    public string? DynamicField { get; set; }
    public long? ParentId { get; set; }
    public int RetryCount { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateChanged { get; set; }
}
