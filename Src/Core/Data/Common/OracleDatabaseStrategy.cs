using System;
using Microsoft.EntityFrameworkCore;
using ArcGIS.Desktop.Framework.Dialogs;

namespace EAABAddIn.Src.Core.Data.Common;

public class OracleDatabaseStrategy : IDatabaseStrategy
{
    private readonly string _connectionString;

    /// <summary>
    /// Constructor con configuración hardcodeada para Oracle
    /// </summary>
    public OracleDatabaseStrategy()
    {
        _connectionString = "User Id=sgo;Password=sgodev01;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=172.19.8.169)(PORT=1521))(CONNECT_DATA=(SID=SITIODEV)));";
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
                System.Diagnostics.Debug.WriteLine($"Estado de Conexión: {context.Database.CanConnect()}");
                return context.Database.CanConnect();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al probar conexiones de base de datos: {ex.Message}\n\n{ex.StackTrace}");
            return false;
        }
    }
}
