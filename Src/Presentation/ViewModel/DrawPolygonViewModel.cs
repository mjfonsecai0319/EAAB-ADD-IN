#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Mapping;
using EAABAddIn.Src.Core.Map;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class DrawPolygonViewModel : BusyViewModelBase
{
    public override string DisplayName => "Nuevo Cierre";
    public override string Tooltip => "Herramienta para crear cierres en el mapa.";

    public ICommand WorkspaceCommand { get; private set; }
    public ICommand FeatureClassCommand { get; private set; }
    public ICommand NeighborhoodCommand { get; private set; }
    public ICommand ClientsAffectedCommand { get; private set; }
    public ICommand BuildPolygonsCommand { get; private set; }
    public ICommand ClearFormCommand { get; private set; }

    public DrawPolygonViewModel()
    {
        WorkspaceCommand = new RelayCommand(OnWorkspace);
        FeatureClassCommand = new RelayCommand(OnFeatureClass);
        NeighborhoodCommand = new RelayCommand(OnNeighborhood);
        ClientsAffectedCommand = new RelayCommand(OnClientsAffected);
        BuildPolygonsCommand = new RelayCommand(async () => await OnBuildPolygons(), () => !IsBusy && CanBuildPolygons);
        ClearFormCommand = new RelayCommand(OnClearForm);
    }

    private async Task OnBuildPolygons()
    {
        StatusMessage = "Construyendo polígonos...";
        IsBusy = true;

        try
        {
            var identifiers = await GetUniqueIdentifiersAsync();
            
            if (identifiers == null || identifiers.Count == 0)
            {
                StatusMessage = "No se encontraron identificadores en la columna seleccionada.";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DrawPolygon] Generando polígonos para {identifiers.Count} identificadores usando campo: {SelectedFeatureClassField}");

            string? neighborhoodsPath = Neighborhood;
            string? clientsPath = ClientsAffected;
            string neighborhoodNameField = "NEIGHBORHOOD_DESC"; 
            var result = await GeocodedPolygonsLayerService.GenerateAsync(
                identifiers,
                Workspace,
                SelectedFeatureClassField,
                neighborhoodsPath,
                clientsPath,
                neighborhoodNameField
            );
            bool allow3 = Module1.Settings?.permitirTresPuntos == true;
            int minPoints = allow3 ? 3 : 4;

            if (result.Count == 0)
            {
                StatusMessage = $"No se generaron polígonos, se necesitan más de {minPoints} puntos por identificador.";
                return;
            }

            if (result.ContainsKey("__DIAGNOSTICO__") && result.Count == 1)
            {
                StatusMessage = $"No se generaron polígonos. Ver consola (Debug) para causas (<{minPoints}).";
                return;
            }

            var resumen = string.Join(", ", result.Keys.Where(k => k != "__DIAGNOSTICO__").Take(10));
            var total = result.Keys.Count(k => k != "__DIAGNOSTICO__");

            if (total > 10) resumen += $" (+{total - 10} más)";

            StatusMessage = $"Generados {total} polígonos (mínimo {minPoints}). IDs: {resumen}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Error construyendo polígonos";
            MessageBox.Show($"Error al construir polígonos: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[DrawPolygon] Error: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnClearForm()
    {
        FeatureClass = null;
        FeatureClassFields = new List<string>();
        SelectedFeatureClassField = null;
        IsFeatureClassSelected = false;
        Neighborhood = null;
        ClientsAffected = null;
        ClientsAffectedCount = 0;
        StatusMessage = string.Empty;
    }


    private async Task<List<string>?> GetUniqueIdentifiersAsync()
    {
        if (string.IsNullOrWhiteSpace(FeatureClass) || string.IsNullOrWhiteSpace(SelectedFeatureClassField))
            return null;

        try
        {
            return await QueuedTask.Run(() =>
            {
                var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                var (gdbPath, datasetName) = ParseFeatureClassPath(FeatureClass, Workspace);

                if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath) || string.IsNullOrWhiteSpace(datasetName))
                    return null;

                var connPath = new ArcGIS.Core.Data.FileGeodatabaseConnectionPath(new Uri(gdbPath));
                using (var gdb = new ArcGIS.Core.Data.Geodatabase(connPath))
                using (var fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(datasetName))
                {
                    var def = fc.GetDefinition();
                    var idField = def.GetFields().FirstOrDefault(f => 
                        f.Name.Equals(SelectedFeatureClassField, StringComparison.OrdinalIgnoreCase))?.Name;

                    if (idField == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GetUniqueIdentifiers] Campo '{SelectedFeatureClassField}' no encontrado");
                        return null;
                    }

                    System.Diagnostics.Debug.WriteLine($"[GetUniqueIdentifiers] Buscando identificadores únicos en campo: {idField}");

                    using (var cursor = fc.Search(new ArcGIS.Core.Data.QueryFilter 
                    { 
                        SubFields = idField 
                    }, true))
                    {
                        while (cursor.MoveNext())
                        {
                            using var row = cursor.Current;
                            var val = row[idField]?.ToString();
                            if (!string.IsNullOrWhiteSpace(val))
                                identifiers.Add(val.Trim());
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[GetUniqueIdentifiers] Encontrados {identifiers.Count} identificadores únicos");
                return identifiers.OrderBy(s => s).ToList();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GetUniqueIdentifiers] Error: {ex.Message}");
            return null;
        }
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

  

    private bool CanBuildPolygons => 
        !string.IsNullOrWhiteSpace(Workspace) && 
        !string.IsNullOrWhiteSpace(FeatureClass) && 
        !string.IsNullOrWhiteSpace(SelectedFeatureClassField);

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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
                _ = GetFeatureClassFieldNamesAsync();
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
            }
        }
    }

    private int _clientsAffectedCount = 0;
    public int ClientsAffectedCount
    {
        get => _clientsAffectedCount;
        set
        {
            if (_clientsAffectedCount != value)
            {
                _clientsAffectedCount = value;
                NotifyPropertyChanged(nameof(ClientsAffectedCount));
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

    private List<string> _featureClassFields = [];

    public List<string> FeatureClassFields
    {
        get => _featureClassFields;
        private set {
            if (_featureClassFields != value)
            {
                _featureClassFields = value;
                IsFeatureClassSelected = !value.IsNullOrEmpty();
                SelectedFeatureClassField = value.FirstOrDefault();
                NotifyPropertyChanged(nameof(FeatureClassFields));
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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
            }
        }
    }   
}