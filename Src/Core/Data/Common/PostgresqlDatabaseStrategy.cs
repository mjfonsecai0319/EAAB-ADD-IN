using System;

using Microsoft.EntityFrameworkCore;

namespace EAAB.AddIn.Src.Core.Data.Common;

/// <summary>
/// Implementación de estrategia para bases de datos PostgreSQL
/// </summary>
public class PostgresqlDatabaseStrategy : IDatabaseStrategy
{
    private readonly string _connectionString;

    /// <summary>
    /// Constructor con configuración hardcodeada para PostgreSQL
    /// </summary>
    public PostgresqlDatabaseStrategy()
    {
        _connectionString = "Host=localhost;Port=5432;Database=eaab;Username=postgres;Password=admin;";
    }

    /// <summary>
    /// Configura el DbContext para usar PostgreSQL
    /// </summary>
    /// <param name="optionsBuilder">Builder para configurar opciones</param>
    public void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(_connectionString, npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(30);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null
            );
            npgsqlOptions.MigrationsAssembly("EAABAddIn");
        });
    }

    /// <summary>
    /// Obtiene la cadena de conexión para PostgreSQL
    /// </summary>
    /// <returns>String de conexión</returns>
    public string GetConnectionString()
    {
        return _connectionString;
    }

    /// <summary>
    /// Verifica si la conexión a PostgreSQL está disponible
    /// </summary>
    /// <returns>True si la conexión está disponible, False en caso contrario</returns>
    public bool TestConnection()
    {
        try
        {
            using (var context = new DatabaseContext(this))
            {
                return context.Database.CanConnect();
            }
        }
        catch
        {
            return false;
        }
    }
}
