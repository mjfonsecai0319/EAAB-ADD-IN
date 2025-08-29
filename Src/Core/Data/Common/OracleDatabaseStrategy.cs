using Microsoft.EntityFrameworkCore;

namespace EAAB.AddIn.Src.Core.Data.Common;

/// <summary>
/// Implementación de estrategia para bases de datos Oracle
/// </summary>
public class OracleDatabaseStrategy : IDatabaseStrategy
{
    private readonly string _connectionString;

    /// <summary>
    /// Constructor con configuración hardcodeada para Oracle
    /// </summary>
    public OracleDatabaseStrategy()
    {
        _connectionString = "User Id=eaab;Password=admin;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XE)));";
    }

    /// <summary>
    /// Configura el DbContext para usar Oracle
    /// </summary>
    /// <param name="optionsBuilder">Builder para configurar opciones</param>
    public void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseOracle(_connectionString, oracleOptions =>
        {
            oracleOptions.CommandTimeout(30);
            oracleOptions.MigrationsAssembly("EAABAddIn");
            oracleOptions.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
        });
    }

    /// <summary>
    /// Obtiene la cadena de conexión para Oracle
    /// </summary>
    /// <returns>String de conexión</returns>
    public string GetConnectionString()
    {
        return _connectionString;
    }

    /// <summary>
    /// Verifica si la conexión a Oracle está disponible
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
