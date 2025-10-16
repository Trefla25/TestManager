using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace eHub.Playground.ScriptsMultipleDbContexts;
public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }
    public MyDbContext() { }
    public DbSet<MyEntity> TestTable { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        //if (!options.IsConfigured)
        //{
        //    options.UseSqlite("dummy.db");
        //}
    }
}

