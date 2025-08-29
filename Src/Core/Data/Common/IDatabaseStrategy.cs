using Microsoft.EntityFrameworkCore;

namespace EAABAddIn.Src.Core.Data.Common;

/// <summary>
/// Interfaz que define la estrategia para conexiones a bases de datos
/// siguiendo el patrón Strategy
/// </summary>
public interface IDatabaseStrategy
{
    /// <summary>
    /// Configura el DbContext con la estrategia específica de la base de datos
    /// </summary>
    /// <param name="optionsBuilder">DbContextOptionsBuilder para configurar</param>
    void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder);

    /// <summary>
    /// Obtiene la cadena de conexión para la base de datos
    /// </summary>
    /// <returns>String de conexión</returns>
    string GetConnectionString();

    /// <summary>
    /// Verifica si la conexión a la base de datos está disponible
    /// </summary>
    /// <returns>True si la conexión está disponible, False en caso contrario</returns>
    bool TestConnection();
}
