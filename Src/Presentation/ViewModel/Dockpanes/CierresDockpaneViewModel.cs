using System.Collections.Generic;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;

using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel.DockPanes;

internal class CierresDockpaneViewModel : DockPane
{
    private static readonly string _dockPaneID = "EAABAddIn_Dockpane_Cierres";
    private static string DockPaneID => _dockPaneID;

    private DrawPolygonViewModel _paneH1VM;
    private AffectedAreaViewModel _paneH2VM;

    protected CierresDockpaneViewModel()
    {
        _paneH1VM = new DrawPolygonViewModel();
        _paneH2VM = new AffectedAreaViewModel();
        SelectedPanelHeaderIndex = 0;
        PrimaryMenuList.Add(new TabControl()
        {
            Text = _paneH1VM.DisplayName,
            Tooltip = _paneH1VM.Tooltip
        });
        PrimaryMenuList.Add(new TabControl()
        {
            Text = _paneH2VM.DisplayName,
            Tooltip = _paneH2VM.Tooltip
        });
    }

    internal static void Show()
    {
        DockPane dockPane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);

        if (dockPane is not null)
        {
            dockPane.Activate();
        }
        return;
    }

    #region Properties
    private int _selectedPanelHeaderIndex = 0;
    public int SelectedPanelHeaderIndex
    {
        get => _selectedPanelHeaderIndex;
        set
        {
            SetProperty(ref _selectedPanelHeaderIndex, value, () => SelectedPanelHeaderIndex);

            switch (_selectedPanelHeaderIndex)
            {
                case 0:
                    CurrentPage = _paneH1VM;
                    break;
                case 1:
                    CurrentPage = _paneH2VM;
                    break;
                default:
                    CurrentPage = _paneH1VM;
                    break;
            }
        }
    }

    private string _heading = "";
    public string Heading
    {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    private PanelViewModelBase _currentPage;
    public PanelViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value, () => CurrentPage);
    }

    private List<TabControl> _primaryMenuList = new List<TabControl>();
    public List<TabControl> PrimaryMenuList
    {
        get => _primaryMenuList;
        private set => SetProperty(ref _primaryMenuList, value);
    }
    #endregion
}
