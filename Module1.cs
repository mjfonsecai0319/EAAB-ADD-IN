using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using EAABAddIn.Src.Core.Data;
using EAABAddIn.Src.Core;
using System.Diagnostics;
using System;
using System.Threading.Tasks;

namespace EAABAddIn
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;
        private static Settings _settings;
        private static DatabaseConnectionService _geodatabaseService;

        public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("EAABAddIn_Module");
        public static Settings Settings => _settings ??= Settings.Load();
        public static DatabaseConnectionService DatabaseConnection => _geodatabaseService;

        protected override bool Initialize()
        {
            var result = base.Initialize();
            
            // 🔹 Inicializar el servicio pero NO conectar automáticamente
            _geodatabaseService = new DatabaseConnectionService();
            
            // 🔹 Solo intentar conectar SI hay configuración válida
            var settings = Settings;
            if (IsValidConfiguration(settings))
            {
                var engine = settings.motor.ToDBEngine();
                if (engine != DBEngine.Unknown)
                {
                    // Conectar en background sin bloquear la inicialización
                    _ = QueuedTask.Run(async () => await HandleDatabaseConnectionAsync(engine));
                }
            }
            else
            {
                Debug.WriteLine("⚠️ No hay configuración válida. El usuario debe configurar la conexión primero.");
            }
            
            return result;
        }

        protected override bool CanUnload()
        {
            QueuedTask.Run(() => _geodatabaseService?.DisposeConnectionAsync());
            return base.CanUnload();
        }

        /// <summary>
        /// Verifica si la configuración es válida para realizar una conexión
        /// </summary>
        private bool IsValidConfiguration(Settings settings)
        {
            return !string.IsNullOrWhiteSpace(settings.motor) &&
                   !string.IsNullOrWhiteSpace(settings.host) &&
                   !string.IsNullOrWhiteSpace(settings.usuario) &&
                   !string.IsNullOrWhiteSpace(settings.contraseña) &&
                   !string.IsNullOrWhiteSpace(settings.baseDeDatos);
        }

        /// <summary>
        /// Maneja la conexión a la base de datos de forma asíncrona
        /// </summary>
        private async Task HandleDatabaseConnectionAsync(DBEngine engine)
        {
            try
            {
                var settings = Settings;
                
                if (engine == DBEngine.Oracle)
                {
                    var props = ConnectionPropertiesFactory.CreateOracleConnection(
                        settings.host, settings.usuario, settings.contraseña, 
                        settings.baseDeDatos, settings.puerto ?? "1521"
                    );
                    await _geodatabaseService.CreateConnectionAsync(props);
                    Debug.WriteLine("✅ Conexión Oracle establecida exitosamente");
                }
                else if (engine == DBEngine.PostgreSQL)
                {
                    var props = ConnectionPropertiesFactory.CreatePostgresConnection(
                        settings.host, settings.usuario, settings.contraseña, 
                        settings.baseDeDatos, settings.puerto ?? "5432"
                    );
                    await _geodatabaseService.CreateConnectionAsync(props);
                    Debug.WriteLine("✅ Conexión PostgreSQL establecida exitosamente");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error al establecer conexión automática: {ex.Message}");
                // No lanzar excepción para no bloquear la aplicación
            }
        }

        /// <summary>
        /// Método público para reconectar cuando se cambie la configuración
        /// </summary>
        public static async Task ReconnectDatabaseAsync()
        {
            try
            {
                var instance = Current;
                if (_geodatabaseService != null)
                {
                    // Cerrar conexión existente
                    await _geodatabaseService.DisposeConnectionAsync();
                }

                // Crear nueva conexión
                var settings = Settings;
                if (instance.IsValidConfiguration(settings))
                {
                    var engine = settings.motor.ToDBEngine();
                    if (engine != DBEngine.Unknown)
                    {
                        await instance.HandleDatabaseConnectionAsync(engine);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error en ReconnectDatabaseAsync: {ex.Message}");
                throw; // Aquí sí lanzamos la excepción porque es una operación explícita del usuario
            }
        }
    }
}