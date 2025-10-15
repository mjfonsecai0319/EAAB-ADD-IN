#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Mapping;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Core.Map;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class AffectedAreaViewModel : BusyViewModelBase
{
    public override string DisplayName => "Area Afectada";
    public override string Tooltip => "Calcular área afectada a partir de entidades o capas seleccionadas";

    private readonly GetSelectedFeatureUseCase _getSelectedFeatureUseCase = new();

    public ICommand WorkspaceCommand { get; private set; }
    public ICommand NeighborhoodCommand { get; private set; }
    public ICommand FeatureClassCommand { get; private set; }
    public ICommand ClientsAffectedCommand { get; private set; }
    public ICommand ClearFormCommand { get; private set; }
    public ICommand BuildPolygonsCommand { get; private set; }

    public AffectedAreaViewModel()
    {
        WorkspaceCommand = new RelayCommand(OnWorkspace);
        NeighborhoodCommand = new RelayCommand(OnNeighborhood);
        FeatureClassCommand = new RelayCommand(OnFeatureClass);
        ClientsAffectedCommand = new RelayCommand(OnClientsAffected);
        ClearFormCommand = new RelayCommand(OnClearForm);
        BuildPolygonsCommand = new RelayCommand(async () => await OnBuildPolygonsAsync(), () => !IsBusy && CanBuildPolygons);
        MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);

        // Refrescar CanExecute cuando cambie IsBusy
        this.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IsBusy))
                RaiseCanExecuteForBuild();
        };
    }

    private string _workspace = Project.Current.DefaultGeodatabasePath;
    public string Workspace
    {
        get => _workspace;
        set
        {
            if (_workspace != value)
            {
                _workspace = value;
                NotifyPropertyChanged(nameof(Workspace));
            }
        }
    }

    private string? _featureClass = null;
    public string? FeatureClass
    {
        get => _featureClass;
        set
        {
            if (_featureClass != value)
            {
                _featureClass = value;
                NotifyPropertyChanged(nameof(FeatureClass));
                _ = GetFeatureClassFieldNamesAsync();
                RaiseCanExecuteForBuild();
            }
        }
    }

    private string? _neighborhood = null;
    public string? Neighborhood
    {
        get => _neighborhood;
        set
        {
            if (_neighborhood != value)
            {
                _neighborhood = value;
                NotifyPropertyChanged(nameof(Neighborhood));
                RaiseCanExecuteForBuild();
            }
        }
    }

    private string? _clientsAffected = null;
    public string? ClientsAffected
    {
        get => _clientsAffected;
        set
        {
            if (_clientsAffected != value)
            {
                _clientsAffected = value;
                NotifyPropertyChanged(nameof(ClientsAffected));
                RaiseCanExecuteForBuild();
            }
        }
    }

    private bool _isFeatureClassSelected = false;
    public bool IsFeatureClassSelected
    {
        get => _isFeatureClassSelected;
        set
        {
            if (_isFeatureClassSelected != value)
            {
                _isFeatureClassSelected = value;
                NotifyPropertyChanged(nameof(IsFeatureClassSelected));
            }
        }
    }

    private string? _selectedFeatureClassField = null;
    public string? SelectedFeatureClassField
    {
        get => _selectedFeatureClassField;
        set
        {
            if (_selectedFeatureClassField != value)
            {
                _selectedFeatureClassField = value;
                NotifyPropertyChanged(nameof(SelectedFeatureClassField));
                RaiseCanExecuteForBuild();
            }
        }
    }

    private int _selectedFeaturesCount = 0;

    public int SelectedFeaturesCount
    {
        get => _selectedFeaturesCount;
        private set
        {
            if (_selectedFeaturesCount != value)
            {
                _selectedFeaturesCount = value;
                NotifyPropertyChanged(nameof(SelectedFeaturesCount));
                RaiseCanExecuteForBuild();
            }
        }
    }

    private List<string> _featureClassFields = [];

    public List<string> FeatureClassFields
    {
        get => _featureClassFields;
        private set
        {
            if (_featureClassFields != value)
            {
                _featureClassFields = value;
                IsFeatureClassSelected = !value.IsNullOrEmpty();
                SelectedFeatureClassField = value.FirstOrDefault();
                NotifyPropertyChanged(nameof(FeatureClassFields));
                RaiseCanExecuteForBuild();
            }
        }
    }

    private readonly ObservableCollection<string> _selectedFeatures = new();

    public ObservableCollection<string> SelectedFeatures
    {
        get => _selectedFeatures;
    }

    public bool CanBuildPolygons =>
        !string.IsNullOrWhiteSpace(Workspace) &&
        !string.IsNullOrWhiteSpace(FeatureClass) &&
        !string.IsNullOrWhiteSpace(SelectedFeatureClassField) &&
        (!string.IsNullOrWhiteSpace(Neighborhood) || !string.IsNullOrWhiteSpace(ClientsAffected));

    private void OnClearForm()
    {
        FeatureClass = null;
        FeatureClassFields = new List<string>();
        SelectedFeatureClassField = null;
        IsFeatureClassSelected = false;
        Neighborhood = null;
        ClientsAffected = null;
        SelectedFeaturesCount = 0;
        _selectedFeatures.Clear();
        StatusMessage = string.Empty;
        RaiseCanExecuteForBuild();
    }

    private async Task OnBuildPolygonsAsync()
    {

        await Task.CompletedTask;
        StatusMessage = "Construyendo polígonos de área afectada...";
    }

    private void RaiseCanExecuteForBuild()
    {
        if (BuildPolygonsCommand is RelayCommand rc)
            rc.RaiseCanExecuteChanged();
    }


    private void OnWorkspace()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Geodatabase",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            Workspace = item.Path;
        }
    }

    private void OnFeatureClass()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses_polygon");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar la Feature Class",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            FeatureClass = item.Path;
        }
    }

    private void OnNeighborhood()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses_polygon");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar la Feature Class",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            Neighborhood = item.Path;
        }
    }

    private void OnClientsAffected()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses_point");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar la Feature Class",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            ClientsAffected = item.Path;
        }
    }

    private async Task GetFeatureClassFieldNamesAsync()
    {
        var fields = new List<string>();

        if (string.IsNullOrWhiteSpace(FeatureClass))
        {
            return;
        }

        try
        {
            var (gdbPath, datasetName) = ParseFeatureClassPath(FeatureClass, Workspace);

            if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath) || string.IsNullOrWhiteSpace(datasetName))
            {
                return;
            }

            FeatureClassFields = await QueuedTask.Run(() =>
            {
                var connPath = new ArcGIS.Core.Data.FileGeodatabaseConnectionPath(new Uri(gdbPath));
                using var gdb = new ArcGIS.Core.Data.Geodatabase(connPath);
                using var fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(datasetName);
                var def = fc.GetDefinition();
                string[] filter = ["objectid", "shape"];

                return def.GetFields().Select(it => it.Name).Where(
                    it => !filter.Contains(it.ToLower())
                ).OrderBy(
                    it => it
                ).ToList();
            });
        }
        catch (Exception)
        {
            return;
        }
    }

    private (string gdbPath, string datasetName) ParseFeatureClassPath(string featureClass, string workspace)
    {
        var idx = featureClass.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);

        if (idx >= 0)
        {
            var gdbEnd = idx + 4;
            var gdbPath = featureClass.Substring(0, gdbEnd);
            var remainder = featureClass.Length > gdbEnd ? featureClass.Substring(gdbEnd).TrimStart('\\', '/') : string.Empty;
            var datasetName = string.IsNullOrWhiteSpace(remainder) ? Path.GetFileNameWithoutExtension(gdbPath) : Path.GetFileName(remainder.Contains('\\') || remainder.Contains('/') ? remainder[..^0] : remainder);
            return (gdbPath, datasetName);
        }

        var datasetNameNoGdb = Path.GetFileName(featureClass);
        return (workspace, string.IsNullOrWhiteSpace(datasetNameNoGdb) ? featureClass : datasetNameNoGdb);
    }

    private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
    {
        QueuedTask.Run(async () =>
        {
            var selectedFeatures = await _getSelectedFeatureUseCase.Invoke(MapView.Active, "BARRIOS_MUNICIPIO");
    
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                _selectedFeatures.Clear();
                SelectedFeaturesCount = selectedFeatures.Count;


                foreach (var e in selectedFeatures)
                    _selectedFeatures.Add(e.GetTable().GetDefinition().GetName() + " - " + e.GetObjectID().ToString());
            });
        });
    }
}