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

        // Método que estaba buscando el ViewModel (alias del método anterior)
        public async Task<(bool IsSuccess, string Message)> TestConnectionInstanceAsync(
            DatabaseConnectionProperties connectionProperties,
            string motorSeleccionado)
        {
            return await TestConnectionAsync(connectionProperties, motorSeleccionado);
        }

        /// <summary>
        /// Prueba la conexión usando un archivo SDE directamente
        /// </summary>
        /// <param name="sdeFilePath">Ruta al archivo .sde</param>
        /// <returns>Resultado de la prueba de conexión</returns>
        public async Task<(bool IsSuccess, string Message)> TestSdeConnectionAsync(string sdeFilePath)
        {
            try
            {
                return await QueuedTask.Run(() =>
                {
                    Debug.WriteLine("=== Probando conexión SDE ===");
                    Debug.WriteLine($"Archivo SDE: {sdeFilePath}");

                    if (!System.IO.File.Exists(sdeFilePath))
                    {
                        return (false, "El archivo SDE no existe en la ruta especificada.");
                    }

                    // Abrir usando reflexión para compatibilidad de versiones del SDK
                    using var geodatabase = OpenSdeGeodatabase(sdeFilePath);

                    // Intento liviano de acceso: obtener definiciones de tablas (lista read-only)
                    var defs = geodatabase.GetDefinitions<TableDefinition>();
                    var count = defs.Count; // fuerza acceso
                    Debug.WriteLine($"✅ Conexión SDE exitosa - Archivo: {sdeFilePath}");
                    return (true, "Conexión exitosa al archivo SDE.");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error de conexión SDE: {ex.Message}");
                return (false, $"Error al conectar con archivo SDE: {ex.Message}");
            }
        }

        /// <summary>
        /// Intenta abrir una geodatabase a partir de un archivo .sde usando reflexión para soportar versiones del SDK.
        /// </summary>
        private Geodatabase OpenSdeGeodatabase(string sdeFilePath)
        {
            var asm = typeof(Geodatabase).Assembly;

            // 1. Intentar con ArcGIS.Core.Data.DatabaseConnectionFile(Uri)
            var dbConnFileType = asm.GetType("ArcGIS.Core.Data.DatabaseConnectionFile");
            if (dbConnFileType != null)
            {
                var uriCtor = dbConnFileType.GetConstructor(new[] { typeof(Uri) });
                var geoCtor = typeof(Geodatabase).GetConstructor(new[] { dbConnFileType });
                if (uriCtor != null && geoCtor != null)
                {
                    var dbConnFile = uriCtor.Invoke(new object[] { new Uri(sdeFilePath) });
                    return (Geodatabase)geoCtor.Invoke(new[] { dbConnFile });
                }
            }

            // 2. Buscar un constructor interno que acepte string (compatibilidad futura)
            foreach (var ctor in typeof(Geodatabase).GetConstructors())
            {
                var pars = ctor.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                {
                    return (Geodatabase)ctor.Invoke(new object[] { sdeFilePath });
                }
            }

            throw new NotSupportedException("No se pudo abrir la geodatabase SDE con los constructores disponibles.");
        }
    }
}