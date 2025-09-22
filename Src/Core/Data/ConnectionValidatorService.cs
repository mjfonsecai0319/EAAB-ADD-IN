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

                    // ✅ Using simplificado según IDE0063
                    using var geodatabase = new Geodatabase(connectionProperties);
                    Debug.WriteLine($"✅ Conexión exitosa a {motorSeleccionado}");
                    return (true, "Conexión exitosa.");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error de conexión: {ex.Message}");
                return (false, $"Error de conexión: {ex.Message}");
            }
        }

        // ✅ Agregar método que está buscando el ViewModel
        public async Task<(bool IsSuccess, string Message)> TestConnectionInstanceAsync(
            DatabaseConnectionProperties connectionProperties,
            string motorSeleccionado)
        {
            return await TestConnectionAsync(connectionProperties, motorSeleccionado);
        }
    }
}