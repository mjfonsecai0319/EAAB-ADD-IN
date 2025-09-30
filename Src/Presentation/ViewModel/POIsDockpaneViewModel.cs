using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace EAABAddIn.Src.Presentation.ViewModel;

public class POIsDockpaneViewModel : DockPane
{
    private const string _dockPaneID = "EAABAddIn_Src_Presentation_View_POIsDockpane";

    internal static void Show()
    {
        DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
        if (pane is not null)
        {
            pane.Activate();
        }
        return;
    }

    private string _searchInput = string.Empty;
    public string SearchInput
    {
        get => _searchInput;
        set
        {
            if (_searchInput != value)
            {
                _searchInput = value;
                NotifyPropertyChanged(nameof(SearchInput));
            }
        }
    }

    public ICommand SearchCommand { get; }

    public POIsDockpaneViewModel()
    {
        this.SearchCommand = new RelayCommand(OnSearchAsync);
    }

    private async Task OnSearchAsync()
    {
        this.SearchInput = string.Empty;
    }
}
