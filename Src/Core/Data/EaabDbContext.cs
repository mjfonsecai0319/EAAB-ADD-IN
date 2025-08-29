using EAAB.AddIn.Src.Core.Data.Common;

using EAABAddIn.Src.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace EAAB.AddIn.Src.Core.Data;

public class EaabDbContext : DbContext
{
    private readonly IDatabaseStrategy _databaseStrategy;

    public EaabDbContext(IDatabaseStrategy databaseStrategy)
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

    /// <summary>
    /// DbSet para la entidad AddressLexEntity
    /// </summary>
    public DbSet<AddressLexEntity> AddressLexEntities { get; set; }

    /// <summary>
    /// Configura el modelo de la base de datos
    /// </summary>
    /// <param name="modelBuilder">Builder para configurar el modelo</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AddressLexEntity>(entity =>
        {
            
        });
        base.OnModelCreating(modelBuilder);
    }
}

