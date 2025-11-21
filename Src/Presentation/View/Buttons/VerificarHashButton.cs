using ArcGIS.Desktop.Framework.Contracts;
using EAABAddIn.Src.Presentation.ViewModel.DockPanes;

namespace EAABAddIn.Src.Presentation.View.Buttons
{
    /// <summary>
    /// Botón para abrir el DockPane de Generador de Hash en la pestaña de Verificar
    /// </summary>
    internal class VerificarHashButton : Button
    {
        protected override void OnClick()
        {
            GeneradorHashDockpaneViewModel.ShowVerificarTab();
        }
    }
}