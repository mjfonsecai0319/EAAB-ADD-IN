#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel;

// Clase para representar una Feature Class seleccionable
public class SelectableFeatureClass : INotifyPropertyChanged
{
    public string DatasetName { get; set; } = string.Empty;
    public string FeatureClassName { get; set; } = string.Empty;
    public string DisplayName => $"{DatasetName} → {FeatureClassName}";
    
    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal class ClipFeatureDatasetViewModel : BusyViewModelBase
{
    public override string DisplayName => "Clip Feature Dataset";
    public override string Tooltip => "Realiza un clip de un Feature Dataset sobre un polígono seleccionado.";

    public ICommand OutputGeodatabaseCommand { get; private set; }
    public ICommand FeatureDatasetCommand { get; private set; }
    public ICommand ExecuteClipCommand { get; private set; }
    public ICommand ClearFormCommand { get; private set; }
    public ICommand RefreshSelectionCommand { get; private set; }
    public ICommand SelectAllCommand { get; private set; }
    public ICommand DeselectAllCommand { get; private set; }

    private Polygon? _selectedPolygon = null;
    private string? _sourceGdbPath = null;
    private List<(string datasetName, List<string> featureClasses)>? _featureDatasets = null;

    private ObservableCollection<SelectableFeatureClass> _availableFeatureClasses = new ObservableCollection<SelectableFeatureClass>();
    public ObservableCollection<SelectableFeatureClass> AvailableFeatureClasses
    {
        get => _availableFeatureClasses;
        private set
        {
            if (_availableFeatureClasses != value)
            {
                _availableFeatureClasses = value;
                NotifyPropertyChanged(nameof(AvailableFeatureClasses));
                NotifyPropertyChanged(nameof(HasFeatureClasses));
                NotifyPropertyChanged(nameof(SelectedFeatureClassesCount));
            }
        }
    }

    public bool HasFeatureClasses => AvailableFeatureClasses.Any();
    public int SelectedFeatureClassesCount => AvailableFeatureClasses.Count(fc => fc.IsSelected);

    public ClipFeatureDatasetViewModel()
    {
        OutputGeodatabaseCommand = new RelayCommand(OnOutputGeodatabase);
        FeatureDatasetCommand = new RelayCommand(OnFeatureDataset);
        ExecuteClipCommand = new RelayCommand(async () => await OnExecuteClip(), () => !IsBusy && CanExecuteClip);
        ClearFormCommand = new RelayCommand(OnClearForm);
        RefreshSelectionCommand = new AsyncRelayCommand(OnRefreshSelectionAsync);
        SelectAllCommand = new RelayCommand(OnSelectAll);
        DeselectAllCommand = new RelayCommand(OnDeselectAll);

        MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
        {
            await Task.Delay(500);
            await OnRefreshSelectionAsync();
        }));
    }

    private string _outputGeodatabase = Project.Current.DefaultGeodatabasePath;
    public string OutputGeodatabase
    {
        get => _outputGeodatabase;
        set
        {
            if (_outputGeodatabase != value)
            {
                _outputGeodatabase = value;
                NotifyPropertyChanged(nameof(OutputGeodatabase));
                NotifyPropertyChanged(nameof(CanExecuteClip));
            }
        }
    }

    private string? _featureDataset = null;
    public string? FeatureDataset
    {
        get => _featureDataset;
        set
        {
            if (_featureDataset != value)
            {
                _featureDataset = value;
                NotifyPropertyChanged(nameof(FeatureDataset));
                NotifyPropertyChanged(nameof(CanExecuteClip));
                _ = OnFeatureDatasetSelectedAsync();
            }
        }
    }

    private string _networksStatus = "No se han cargado las redes";
    public string NetworksStatus
    {
        get => _networksStatus;
        private set
        {
            if (_networksStatus != value)
            {
                _networksStatus = value;
                NotifyPropertyChanged(nameof(NetworksStatus));
            }
        }
    }

    private int _networksLoaded = 0;
    public int NetworksLoaded
    {
        get => _networksLoaded;
        private set
        {
            if (_networksLoaded != value)
            {
                _networksLoaded = value;
                NotifyPropertyChanged(nameof(NetworksLoaded));
            }
        }
    }

    private bool _isBufferEnabled = false;
    public bool IsBufferEnabled
    {
        get => _isBufferEnabled;
        set
        {
            if (_isBufferEnabled != value)
            {
                _isBufferEnabled = value;
                NotifyPropertyChanged(nameof(IsBufferEnabled));
                
                if (!_isBufferEnabled)
                {
                    BufferMeters = 0;
                }
            }
        }
    }

    private double _bufferMeters = 0;
    public double BufferMeters
    {
        get => _bufferMeters;
        set
        {
            var newVal = value < 0 ? 0 : value;
            if (Math.Abs(_bufferMeters - newVal) > 0.000001)
            {
                _bufferMeters = newVal;
                NotifyPropertyChanged(nameof(BufferMeters));
            }
        }
    }

    private string _selectionStatus = "No hay polígono seleccionado";
    public string SelectionStatus
    {
        get => _selectionStatus;
        private set
        {
            if (_selectionStatus != value)
            {
                _selectionStatus = value;
                NotifyPropertyChanged(nameof(SelectionStatus));
                NotifyPropertyChanged(nameof(CanExecuteClip));
            }
        }
    }

    public bool CanExecuteClip => (
        !string.IsNullOrWhiteSpace(OutputGeodatabase) &&
        !string.IsNullOrWhiteSpace(FeatureDataset) &&
        BufferMeters >= 0 &&
        _selectedPolygon != null &&
        NetworksLoaded >= 1 &&
        AvailableFeatureClasses.Any(fc => fc.IsSelected)
    );

    private void OnClearForm()
    {
        FeatureDataset = null;
        IsBufferEnabled = false;
        BufferMeters = 0;
        StatusMessage = string.Empty;
        AvailableFeatureClasses.Clear();
    }

    private void OnSelectAll()
    {
        foreach (var fc in AvailableFeatureClasses)
        {
            fc.IsSelected = true;
        }
        NotifyPropertyChanged(nameof(SelectedFeatureClassesCount));
        NotifyPropertyChanged(nameof(CanExecuteClip));
    }

    private void OnDeselectAll()
    {
        foreach (var fc in AvailableFeatureClasses)
        {
            fc.IsSelected = false;
        }
        NotifyPropertyChanged(nameof(SelectedFeatureClassesCount));
        NotifyPropertyChanged(nameof(CanExecuteClip));
    }

    private void OnOutputGeodatabase()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_geodatabases");

        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Geodatabase de Salida",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            OutputGeodatabase = item.Path;
        }
    }

    private void OnFeatureDataset()
    {
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Geodatabase con Redes",
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };

        var ok = dlg.ShowDialog();
        if (ok == true && dlg.Items != null && dlg.Items.Any())
        {
            var item = dlg.Items.First();
            FeatureDataset = item.Path;
        }
    }

    private async Task OnFeatureDatasetSelectedAsync()
    {
        if (string.IsNullOrWhiteSpace(FeatureDataset))
        {
            NetworksStatus = "No se han cargado las redes";
            NetworksLoaded = 0;
            return;
        }

        StatusMessage = "Cargando redes del Feature Dataset...";
        
        try
        {
            await QueuedTask.Run(async () =>
            {
                await LoadNetworksFromFeatureDataset();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando redes: {ex.Message}");
            NetworksStatus = $"❌ Error cargando redes: {ex.Message}";
            NetworksLoaded = 0;
        }
    }

    private async Task LoadNetworksFromFeatureDataset()
    {
        _sourceGdbPath = null;
        _featureDatasets = null;
        NetworksLoaded = 0;

        // Limpiar la lista de Feature Classes disponibles
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            AvailableFeatureClasses.Clear();
        });

        try
        {
            var (gdbPath, datasetName) = ParseFeatureDatasetPath(FeatureDataset!);

            if (string.IsNullOrWhiteSpace(gdbPath) || !Directory.Exists(gdbPath))
            {
                NetworksStatus = "❌ Ruta de Geodatabase inválida";
                return;
            }

            _sourceGdbPath = gdbPath;
            _featureDatasets = new List<(string, List<string>)>();

            var connPath = new ArcGIS.Core.Data.FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new ArcGIS.Core.Data.Geodatabase(connPath);

            // Obtener todos los Feature Datasets
            var datasetDefinitions = gdb.GetDefinitions<FeatureDatasetDefinition>();
            
            int totalFeatureClasses = 0;
            var datasetsSummary = new List<string>();
            var selectableList = new List<SelectableFeatureClass>();

            foreach (var datasetDef in datasetDefinitions)
            {
                var dsName = datasetDef.GetName();
                
                // Abrir el dataset y obtener sus feature classes
                using var featureDataset = gdb.OpenDataset<FeatureDataset>(dsName);
                var featureClassDefinitions = featureDataset.GetDefinitions<FeatureClassDefinition>();
                var fcList = new List<string>();
                
                foreach (var fcDef in featureClassDefinitions)
                {
                    var fcName = fcDef.GetName();
                    fcList.Add(fcName);
                    
                    // Agregar a la lista de seleccionables
                    selectableList.Add(new SelectableFeatureClass
                    {
                        DatasetName = dsName,
                        FeatureClassName = fcName,
                        IsSelected = true // Por defecto todas seleccionadas
                    });
                    
                    fcDef.Dispose();
                }

                if (fcList.Any())
                {
                    _featureDatasets.Add((dsName, fcList));
                    totalFeatureClasses += fcList.Count;
                    datasetsSummary.Add($"✓ {dsName} ({fcList.Count} Feature Classes)");
                }
            }

            // Actualizar la lista observable en el UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableFeatureClasses.Clear();
                foreach (var fc in selectableList)
                {
                    fc.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(SelectableFeatureClass.IsSelected))
                        {
                            NotifyPropertyChanged(nameof(SelectedFeatureClassesCount));
                            NotifyPropertyChanged(nameof(CanExecuteClip));
                        }
                    };
                    AvailableFeatureClasses.Add(fc);
                }
                NotifyPropertyChanged(nameof(HasFeatureClasses));
                NotifyPropertyChanged(nameof(SelectedFeatureClassesCount));
            });

            NetworksLoaded = _featureDatasets.Count;

            if (NetworksLoaded == 0)
            {
                NetworksStatus = "❌ No se encontraron Feature Datasets en la Geodatabase";
            }
            else
            {
                var datasetsText = string.Join("\n  ", datasetsSummary);
                NetworksStatus = $"✓ {NetworksLoaded} Feature Datasets encontrados ({totalFeatureClasses} Feature Classes total):\n  {datasetsText}";
            }

            StatusMessage = $"✓ {NetworksLoaded} Feature Datasets cargados correctamente";
            NotifyPropertyChanged(nameof(CanExecuteClip));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en LoadNetworksFromFeatureDataset: {ex.Message}");
            NetworksStatus = $"❌ Error: {ex.Message}";
            NetworksLoaded = 0;
        }
    }

    private (string gdbPath, string datasetName) ParseFeatureDatasetPath(string featureDatasetPath)
    {
        if (featureDatasetPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
        {
            return (featureDatasetPath, "FeatureDataset");
        }

        var idx = featureDatasetPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);

        if (idx >= 0)
        {
            var gdbEnd = idx + 4;
            var gdbPath = featureDatasetPath.Substring(0, gdbEnd);
            var remainder = featureDatasetPath.Length > gdbEnd 
                ? featureDatasetPath.Substring(gdbEnd).TrimStart('\\', '/') 
                : string.Empty;
            var datasetName = string.IsNullOrWhiteSpace(remainder) 
                ? "FeatureDataset" 
                : Path.GetFileName(remainder);
            return (gdbPath, datasetName);
        }

        var datasetNameNoGdb = Path.GetFileName(featureDatasetPath);
        return (Path.GetDirectoryName(featureDatasetPath) ?? string.Empty, 
                string.IsNullOrWhiteSpace(datasetNameNoGdb) ? featureDatasetPath : datasetNameNoGdb);
    }

    private async Task OnExecuteClip()
    {
        if (_selectedPolygon == null)
        {
            StatusMessage = "❌ No hay polígono seleccionado";
            return;
        }

        var selectedCount = AvailableFeatureClasses.Count(fc => fc.IsSelected);
        if (selectedCount == 0)
        {
            StatusMessage = "❌ Debe seleccionar al menos una Feature Class";
            return;
        }

        StatusMessage = $"Ejecutando clip de {selectedCount} Feature Classes...";
        IsBusy = true;

        try
        {
            var parentFolder = Path.GetDirectoryName(OutputGeodatabase);
            
            if (string.IsNullOrWhiteSpace(parentFolder) || !Directory.Exists(parentFolder))
            {
                parentFolder = Path.GetDirectoryName(Project.Current.DefaultGeodatabasePath) 
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var gdbName = $"Clip_{timestamp}.gdb";
            var outputGdbPath = Path.Combine(parentFolder, gdbName);

            var bufferInMapUnits = await ConvertMetersToMapUnitsAsync(_selectedPolygon, BufferMeters);

            // Filtrar solo las Feature Classes seleccionadas
            var selectedFeatureDatasets = new List<(string datasetName, List<string> featureClasses)>();
            
            if (_featureDatasets != null)
            {
                foreach (var (datasetName, featureClasses) in _featureDatasets)
                {
                    var selectedFCs = featureClasses
                        .Where(fc => AvailableFeatureClasses.Any(afc => 
                            afc.DatasetName == datasetName && 
                            afc.FeatureClassName == fc && 
                            afc.IsSelected))
                        .ToList();

                    if (selectedFCs.Any())
                    {
                        selectedFeatureDatasets.Add((datasetName, selectedFCs));
                    }
                }
            }

            var useCase = new EAABAddIn.Src.Application.UseCases.ClipFeatureDatasetUseCase();
            var (success, message) = await useCase.ExecuteAsync(
                outputGdbPath,
                _sourceGdbPath,
                selectedFeatureDatasets,
                _selectedPolygon,
                bufferInMapUnits);

            StatusMessage = success ? $"✓ {message}" : $"❌ {message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.StackTrace}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<double> ConvertMetersToMapUnitsAsync(Polygon polygon, double meters)
    {
        return await QueuedTask.Run(() =>
        {
            var spatialRef = polygon.SpatialReference;
            
            if (spatialRef == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ SpatialReference es null, usando metros directamente: {meters}");
                return meters;
            }

            // Verificar si es un sistema de coordenadas geográfico (lat/lon)
            if (spatialRef.IsGeographic)
            {
                // Para coordenadas geográficas, aproximadamente 1 grado = 111,000 metros en el ecuador
                // Convertir metros a grados: grados = metros / 111,000
                var bufferInDegrees = meters / 111000.0;
                System.Diagnostics.Debug.WriteLine($"✓ Sistema geográfico detectado (WKID: {spatialRef.Wkid})");
                System.Diagnostics.Debug.WriteLine($"  Conversión: {meters} metros = {bufferInDegrees:F8} grados");
                return bufferInDegrees;
            }

            var unit = spatialRef.Unit;
            
            if (unit == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Unit es null, usando metros directamente: {meters}");
                return meters;
            }

            // ConversionFactor es el factor para convertir DE la unidad A metros
            var conversionFactor = unit.ConversionFactor;
            
            System.Diagnostics.Debug.WriteLine($"ℹ️ Sistema proyectado detectado (WKID: {spatialRef.Wkid})");
            System.Diagnostics.Debug.WriteLine($"  Unidad: {unit.Name}, Factor de conversión: {conversionFactor}");
            
            if (Math.Abs(conversionFactor) < 0.0001)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Factor de conversión demasiado pequeño, usando metros: {meters}");
                return meters;
            }

            // Si el factor es 1.0, la unidad ya es metros
            if (Math.Abs(conversionFactor - 1.0) < 0.0001)
            {
                System.Diagnostics.Debug.WriteLine($"✓ Unidad ya está en metros: {meters}");
                return meters;
            }

            // Convertir metros a unidades del mapa
            // Ejemplo: si la unidad es pies (factor = 0.3048), entonces 10 metros = 10 / 0.3048 = 32.8 pies
            var bufferInMapUnits = meters / conversionFactor;
            
            System.Diagnostics.Debug.WriteLine($"✓ Conversión: {meters} metros = {bufferInMapUnits:F2} {unit.Name}");
            
            return bufferInMapUnits;
        });
    }

    private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
    {
        QueuedTask.Run(OnRefreshSelectionAsync);
    }

    private async Task OnRefreshSelectionAsync()
    {
        try
        {
            _selectedPolygon = await GetSelectedPolygonAsync();

            if (_selectedPolygon != null)
            {
                var area = _selectedPolygon.Area;
                var areaText = area >= 1 ? $"{area:N0} m²" : $"{area:N2} m²";
                SelectionStatus = $"✓ Polígono seleccionado (Área: {areaText})";
            }
            else
            {
                SelectionStatus = "❌ No hay polígono seleccionado";
            }

            NotifyPropertyChanged(nameof(CanExecuteClip));
            (ExecuteClipCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al actualizar selección: {ex.Message}");
            SelectionStatus = "⚠️ Error al detectar selección";
        }
    }

    private async Task<Polygon?> GetSelectedPolygonAsync()
    {
        return await QueuedTask.Run(() =>
        {
            var mv = MapView.Active;
            if (mv?.Map == null)
                return null;

            var selectionSet = mv.Map.GetSelection();
            if (selectionSet.Count == 0)
                return null;

            foreach (var layerSelection in selectionSet.ToDictionary())
            {
                var layer = layerSelection.Key;
                var oids = layerSelection.Value;

                if (layer is FeatureLayer featureLayer &&
                    featureLayer.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)
                {
                    if (oids.Count != 1)
                        continue;

                    var queryFilter = new QueryFilter
                    {
                        ObjectIDs = oids
                    };

                    using (var featureCursor = featureLayer.Search(queryFilter))
                    {
                        if (featureCursor.MoveNext())
                        {
                            using (var feature = featureCursor.Current as Feature)
                            {
                                if (feature?.GetShape() is Polygon polygon)
                                {
                                    return polygon;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        });
    }
}
