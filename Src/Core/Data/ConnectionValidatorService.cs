using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace EAABAddIn.Src.Core.Data
{
    public class ConnectionValidatorService
    {
        public async Task<(bool IsSuccess, string Message)> TestConnectionAsync(
            DatabaseConnectionProperties connectionProperties,
            string motorSeleccionado)
        {
            try
            {
                return await QueuedTask.Run(() =>
                {
                    Debug.WriteLine("=== Probando conexión desde ConnectionValidatorService ===");
                    Debug.WriteLine($"Motor: {motorSeleccionado}");
                    Debug.WriteLine($"Usuario: {connectionProperties.User}");
                    Debug.WriteLine($"Base de Datos: {connectionProperties.Database}");
                    Debug.WriteLine($"Instance: {connectionProperties.Instance}");
                    Debug.WriteLine($"AuthMode: {connectionProperties.AuthenticationMode}");

                    using (var geodatabase = new Geodatabase(connectionProperties))
                    {
                        Debug.WriteLine($"✅ Conexión exitosa a {motorSeleccionado}");
                        return (true, "Conexión exitosa.");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error de conexión: {ex.Message}");
                return (false, $"Error de conexión: {ex.Message}");
            }
        }
    }
}
