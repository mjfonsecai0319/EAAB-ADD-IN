using EAAB.AddIn.Src.Core.Data.Common;

using Microsoft.EntityFrameworkCore;

namespace EAAB.AddIn.Src.Core.Data;

/// <summary>
/// Clase base para el contexto de Entity Framework
/// </summary>
public class DatabaseContext : DbContext
{
    private readonly IDatabaseStrategy _databaseStrategy;

    /// <summary>
    /// Constructor que recibe la estrategia de base de datos a utilizar
    /// </summary>
    /// <param name="databaseStrategy">Estrategia de base de datos</param>
    public DatabaseContext(IDatabaseStrategy databaseStrategy)
    {
        _databaseStrategy = databaseStrategy;
    }

    /// <summary>
    /// Configura las opciones del contexto utilizando la estrategia de base de datos
    /// </summary>
    /// <param name="optionsBuilder">Builder para configurar opciones</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            _databaseStrategy.ConfigureDbContext(optionsBuilder);
        }

        base.OnConfiguring(optionsBuilder);
    }

    /// <summary>
    /// Configura el modelo de la base de datos
    /// </summary>
    /// <param name="modelBuilder">Builder para configurar el modelo</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}

