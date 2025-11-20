using ArcGIS.Desktop.Framework.Contracts;
using EAABAddIn.Src.Presentation.ViewModel.DockPanes;

namespace EAABAddIn.Src.Presentation.View.Buttons
{
    /// <summary>
    /// Botón para abrir el DockPane de Generador de Hash
    /// </summary>
    internal class GeneradorHashButton : Button
    {
        protected override void OnClick()
        {
            GeneradorHashDockpaneViewModel.Show();
        }
    }
}
