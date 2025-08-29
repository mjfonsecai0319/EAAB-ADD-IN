using EAAB.AddIn.Src.Core.Data.Common;

using EAABAddIn.Src.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace EAAB.AddIn.Src.Core.Data;

public class DatabaseContext : DbContext
{
    private readonly IDatabaseStrategy _databaseStrategy;

    public DatabaseContext(IDatabaseStrategy databaseStrategy)
    {
        _databaseStrategy = databaseStrategy;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            _databaseStrategy.ConfigureDbContext(optionsBuilder);
        }

        base.OnConfiguring(optionsBuilder);
    }

    public DbSet<AddressLexEntity> AddressLexEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AddressLexEntity>(entity =>
        {
            
        });
        base.OnModelCreating(modelBuilder);
    }
}

