using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace EAABAddIn.Presentation.View.Buttons
{
    internal class UnionPolygonsButton : Button
    {
        protected override void OnClick()
        {
            var pane = FrameworkApplication.DockPaneManager.Find("EAABAddIn_UnionPolygonsDockpane");
            
            if (pane == null)
            {
                System.Diagnostics.Debug.WriteLine("No se pudo encontrar el DockPane UnionPolygons");
                return;
            }

            pane.Activate();
        }
    }
}