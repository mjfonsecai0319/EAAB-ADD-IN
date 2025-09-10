using System;

using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class MassiveGeocodeViewModel : PanelViewModelBase
{
    public override string DisplayName => "Geocodificación Masiva";
    public override string Tooltip => "Buscar múltiples direcciones a la vez desde un archivo .xlsx o .xls";
}
