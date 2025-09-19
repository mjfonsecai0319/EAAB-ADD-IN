using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using EAABAddIn.Src.Core.Data;
using EAABAddIn.Src.Core;

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
            var engine = Settings.motor.ToDBEngine();

            QueuedTask.Run(() => HandleDatabaseConnection(engine));
            return result;
        }

        protected override bool CanUnload()
        {
            QueuedTask.Run(() => _geodatabaseService?.DisposeConnectionAsync());
            return base.CanUnload();
        }

        private async void HandleDatabaseConnection(DBEngine engine)
        {
            _geodatabaseService = new DatabaseConnectionService();

            if (engine == DBEngine.Oracle)
            {
                var props = ConnectionPropertiesFactory.CreateOracleConnection(
                    Settings.host, Settings.usuario, Settings.contraseña, Settings.baseDeDatos, Settings.puerto
                );
                await _geodatabaseService.CreateConnectionAsync(props);
            }
            else if (engine == DBEngine.PostgreSQL)
            {
                var props = ConnectionPropertiesFactory.CreatePostgresConnection(
                    Settings.host, Settings.usuario, Settings.contraseña, Settings.baseDeDatos, Settings.puerto
                );
                await _geodatabaseService.CreateConnectionAsync(props);
            }
        }
    }
}
