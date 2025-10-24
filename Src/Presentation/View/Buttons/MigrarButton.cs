using ArcGIS.Desktop.Framework.Contracts;

using EAABAddIn.Src.Presentation.ViewModel.DockPanes;

namespace EAABAddIn.Src.Presentation.View.Buttons;

internal class MigrarButton : Button
{
    protected override void OnClick()
    {
        // Abre el dockpane de Migración
        MigrationDockpaneViewModel.Show();
    }
}
