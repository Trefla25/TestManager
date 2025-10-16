using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eHub.PlugIn;

namespace eHub.Playground.ScriptsMultipleDbContexts;
public class MyEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    public required string Data { get; set; }
    public required int Status { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateChanged { get; set; }
}
