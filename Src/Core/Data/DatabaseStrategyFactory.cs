using System;

using EAAB.AddIn.Src.Core.Data.Common;

namespace EAAB.AddIn.Src.Core.Data;

/// <summary>
/// Enumeración que define los tipos de base de datos soportados
/// </summary>
public enum DatabaseType
{
    PostgreSQL, Oracle
}

/// <summary>
/// Fábrica para crear estrategias de base de datos
/// </summary>
public static class DatabaseStrategyFactory
{
    /// <summary>
    /// Crea una estrategia de base de datos según el tipo especificado
    /// </summary>
    /// <param name="databaseType">Tipo de base de datos</param>
    /// <returns>Estrategia de base de datos</returns>
    /// <exception cref="ArgumentException">Cuando se especifica un tipo no soportado</exception>
    public static IDatabaseStrategy CreateDatabaseStrategy(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.PostgreSQL => new PostgresqlDatabaseStrategy(),
            DatabaseType.Oracle => new OracleDatabaseStrategy(),
            _ => throw new ArgumentException($"Tipo de base de datos no soportado: {databaseType}")
        };
    }

    /// <summary>
    /// Crea un contexto de base de datos con la estrategia especificada
    /// </summary>
    /// <param name="databaseType">Tipo de base de datos</param>
    /// <returns>Contexto de base de datos</returns>
    public static DatabaseContext CreateDbContext(DatabaseType databaseType)
    {
        IDatabaseStrategy strategy = CreateDatabaseStrategy(databaseType);
        return new DatabaseContext(strategy);
    }
}

