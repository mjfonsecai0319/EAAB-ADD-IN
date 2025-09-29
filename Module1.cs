using System;
using System.Threading.Tasks;

using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Core;
using EAABAddIn.Src.Core.Data;

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

            _geodatabaseService = new DatabaseConnectionService();

            var settings = Settings;

            ProjectOpenedEvent.Subscribe(this.OnProjectOpened);

            if (IsValidConfiguration(settings))
            {
                var engine = settings.motor.ToDBEngine();
                if (engine != DBEngine.Unknown)
                {
                    _ = QueuedTask.Run(async () => await HandleDatabaseConnectionAsync(engine));
                }
            }
            else
            {
                // Configuración no válida; la conexión se intentará una vez el usuario provea datos.
            }

            return result;
        }

        protected override bool CanUnload()
        {
            QueuedTask.Run(() => _geodatabaseService?.DisposeConnectionAsync());
            return base.CanUnload();
        }

        private bool IsValidConfiguration(Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.motor)) return false;

            var engine = settings.motor.ToDBEngine();
            if (engine == DBEngine.OracleSDE || engine == DBEngine.PostgreSQLSDE)
            {
                return !string.IsNullOrWhiteSpace(settings.rutaArchivoCredenciales) &&
                    System.IO.File.Exists(settings.rutaArchivoCredenciales);
            }

            return !string.IsNullOrWhiteSpace(settings.host) &&
                !string.IsNullOrWhiteSpace(settings.usuario) &&
                !string.IsNullOrWhiteSpace(settings.contraseña) &&
                !string.IsNullOrWhiteSpace(settings.baseDeDatos);
        }

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
                }
                else if (engine == DBEngine.PostgreSQL)
                {
                    var props = ConnectionPropertiesFactory.CreatePostgresConnection(
                        settings.host, settings.usuario, settings.contraseña,
                        settings.baseDeDatos, settings.puerto ?? "5432"
                    );
                    await _geodatabaseService.CreateConnectionAsync(props);
                }
                else if (engine == DBEngine.OracleSDE)
                {
                    var sdePath = settings.rutaArchivoCredenciales;
                    await _geodatabaseService.CreateConnectionAsync(sdePath);
                }
                else if (engine == DBEngine.PostgreSQLSDE)
                {
                    var sdePath = settings.rutaArchivoCredenciales;
                    await _geodatabaseService.CreateConnectionAsync(sdePath);
                }
            }
            catch (Exception)
            {
                // Error al intentar conexión automática silenciado intencionalmente
            }
        }

        public static async Task ReconnectDatabaseAsync()
        {
            try
            {
                var instance = Current;
                if (_geodatabaseService != null)
                {
                    await _geodatabaseService.DisposeConnectionAsync();
                }

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
            catch (Exception)
            {
                throw;
            }
        }

        public void OnProjectOpened(ProjectEventArgs args)
        {
            if (ValidateDomain.Invoke())
            {
                FrameworkApplication.State.Activate("EAABAddIn_InCompanyDomain");
                return;
            }
            DisableAddIn();
        }

        private void DisableAddIn()
        {
            FrameworkApplication.State.Deactivate("EAABAddIn_InCompanyDomain");

            try
            {
                var dockPane = FrameworkApplication.DockPaneManager.Find("EAABAddIn_Src_Presentation_View_GeocoderDockpane");
                dockPane?.Hide();
            }
            catch
            {
                // Ignorar errores al ocultar el DockPane
            }

            if (_geodatabaseService != null)
            {
                _ = QueuedTask.Run(_geodatabaseService.DisposeConnectionAsync);
            }
        }
    }
}