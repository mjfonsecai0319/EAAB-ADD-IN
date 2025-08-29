using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace EAABAddIn;

internal class Module1 : Module
{
    private static Module1 _this = null;

    public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("EAABAddIn_Module");

    protected override bool CanUnload()
    {
        return true;
    }
}
