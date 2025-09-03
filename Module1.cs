using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;


namespace EAABAddIn
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;
        public static Module1 Current =>
            _this ??= (Module1)FrameworkApplication.FindModule("EAABAddIn_Module");

        // 🔹 Instancia única de Settings (carga desde JSON al inicio)
        private static Settings _settings;
        public static Settings Settings => _settings ??= Settings.Load();

        // Si necesitas lógica extra de inicialización/cierre la agregas aquí:
        protected override bool Initialize()
        {
            return base.Initialize();
        }

        protected override bool CanUnload()
        {
            return true;
        }
    }
}