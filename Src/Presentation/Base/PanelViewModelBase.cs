using ArcGIS.Desktop.Framework.Contracts;

namespace EAABAddIn.Src.Presentation.Base;

public abstract class PanelViewModelBase : PropertyChangedBase
{
    public abstract string DisplayName { get; }

    public abstract string Tooltip { get; }
}
