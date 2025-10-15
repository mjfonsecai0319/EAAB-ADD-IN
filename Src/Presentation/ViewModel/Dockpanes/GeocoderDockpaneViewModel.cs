using System.Collections.Generic;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;

using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel.DockPanes;

internal class GeocoderDockpaneViewModel : DockPane
{
    private const string _dockPaneID = "EAABAddIn_Dockpane_AddressGeocoder";

    private AddressSearchViewModel _paneH1VM;
    private MassiveGeocodeViewModel _paneH2VM;
    private POIsDockpaneViewModel _paneH3VM;

    protected GeocoderDockpaneViewModel()
    {
        _paneH1VM = new AddressSearchViewModel();
        _paneH2VM = new MassiveGeocodeViewModel();
        _paneH3VM = new POIsDockpaneViewModel();
        _selectedPanelHeaderIndex = 0;
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
        PrimaryMenuList.Add(new TabControl()
        {
            Text = _paneH3VM.DisplayName,
            Tooltip = _paneH3VM.Tooltip
        });
        CurrentPage = _paneH1VM;
    }

    internal static void Show()
    {
        DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
        if (pane is not null)
        {
            pane.Activate();
        }
        return;
    }

    #region Properties
    private string _heading = "";
    public string Heading
    {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    private List<TabControl> _primaryMenuList = new List<TabControl>();
    public List<TabControl> PrimaryMenuList { get => _primaryMenuList; }

    private PanelViewModelBase _currentPage;
    public PanelViewModelBase CurrentPage
    {
        get { return _currentPage; }
        set
        {
            SetProperty(ref _currentPage, value, () => CurrentPage);
        }
    }

    private int _selectedPanelHeaderIndex = 0;
    public int SelectedPanelHeaderIndex
    {
        get { return _selectedPanelHeaderIndex; }
        set
        {
            SetProperty(ref _selectedPanelHeaderIndex, value, () => SelectedPanelHeaderIndex);
            if (_selectedPanelHeaderIndex == 0)
                CurrentPage = _paneH1VM;
            if (_selectedPanelHeaderIndex == 1)
                CurrentPage = _paneH2VM;
            if (_selectedPanelHeaderIndex == 2)
                CurrentPage = _paneH3VM;
        }
    }
    #endregion
}

internal class GeocoderDockpane_ShowButton : Button
{
    protected override void OnClick()
    {
        GeocoderDockpaneViewModel.Show();
    }
}