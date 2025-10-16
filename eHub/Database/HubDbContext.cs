using eHub.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace eHub.Database;

public class HubDbContext : DbContext
{
    public DbSet<Packet> Packet { get; set; }

    public HubDbContext() : base()
    {

    }

    public HubDbContext(DbContextOptions<HubDbContext> options) : base(options)
    {

    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // A quick and dirty way to inspec generated SQL Querys, uncomment if needed
        // options.LogTo(Console.WriteLine);
        SQLitePCL.Batteries.Init();
        if (!options.IsConfigured)
        {
            options.UseSqlite("dummy.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Packet>()
            .HasIndex(p => p.DateCreated);

        modelBuilder.Entity<Packet>()
            .HasIndex(p => new { p.Id, p.DateCreated });

        modelBuilder.Entity<Packet>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<Packet>()
            .HasIndex(p => new { p.Channel, p.Status });

        modelBuilder.Entity<Packet>()
            .HasIndex(p => p.ParentId);
    }
}
