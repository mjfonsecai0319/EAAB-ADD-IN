#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

using EAABAddIn.Src.Presentation.Base;
using EAABAddIn.Src.Application.UseCases;

namespace EAABAddIn.Src.Presentation.ViewModel;

public class UnionPolygonsViewModel : BusyViewModelBase
{
    public override string DisplayName => "Unir Polígonos";
    public override string Tooltip => "Crear polígonos unidos a partir de entidades seleccionadas";

    public ICommand WorkspaceCommand { get; private set; }
    public ICommand NeighborhoodCommand { get; private set; }
    public ICommand FeatureClassCommand { get; private set; }
    public ICommand ClientsAffectedCommand { get; private set; }
    public ICommand RunCommand { get; private set; }
    public ICommand ClearFormCommand { get; private set; }
    public ICommand RefreshSelectionCommand { get; private set; }

    private readonly GetSelectedFeatureUseCase _getSelectedFeatureUseCase = new();
    private string? _lastSaveError = null;

    public UnionPolygonsViewModel()
    {
        WorkspaceCommand = new RelayCommand(OnWorkspace);
        NeighborhoodCommand = new RelayCommand(OnNeighborhood);
        FeatureClassCommand = new RelayCommand(OnFeatureClass);
        ClientsAffectedCommand = new RelayCommand(OnClientsAffected);
        RunCommand = new AsyncRelayCommand(OnRunAsync, () => CanBuildPolygons);
        ClearFormCommand = new RelayCommand(OnClearForm);
        RefreshSelectionCommand = new AsyncRelayCommand(OnRefreshSelectionAsync);
        
        MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        // Inicializar después de que la UI esté lista
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
        {
            await Task.Delay(500); // Pequeño delay para asegurar que la UI esté lista
            await OnRefreshSelectionAsync();
        }));
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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
                RaiseRunCanExecuteChanged();
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
                RaiseRunCanExecuteChanged();
                QueuedTask.Run(UpdateSelectedFeatures);
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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
                RaiseRunCanExecuteChanged();
                // Ya no usamos valores desde la selección; el usuario ingresa el identificador manualmente
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
                NotifyPropertyChanged(nameof(CanBuildPolygons));
                RaiseRunCanExecuteChanged();
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
                IsFeatureClassSelected = value != null && value.Count > 0;
                SelectedFeatureClassField = value?.FirstOrDefault();
                NotifyPropertyChanged(nameof(FeatureClassFields));
                NotifyPropertyChanged(nameof(CanBuildPolygons));
                RaiseRunCanExecuteChanged();
            }
        }
    }

    // Eliminado el flujo de identificador desde la selección: usamos entrada manual

    // Permite ingresar manualmente el identificador deseado
    private string? _identifierText = null;
    public string? IdentifierText
    {
        get => _identifierText;
        set
        {
            if (_identifierText != value)
            {
                _identifierText = value;
                NotifyPropertyChanged(nameof(IdentifierText));
                NotifyPropertyChanged(nameof(CanBuildPolygons));
                RaiseRunCanExecuteChanged();
            }
        }
    }

    private readonly ObservableCollection<string> _selectedFeatures = new();
    public ObservableCollection<string> SelectedFeatures => _selectedFeatures;

    public bool CanBuildPolygons =>
        !string.IsNullOrWhiteSpace(Workspace) &&
        !string.IsNullOrWhiteSpace(SelectedFeatureClassField) &&
        !string.IsNullOrWhiteSpace(IdentifierText) &&
        SelectedFeaturesCount >= 2;

    private void OnClearForm()
    {
        FeatureClass = null;
        FeatureClassFields = new List<string>();
        SelectedFeatureClassField = null;
        // Limpiar identificador manual
        IdentifierText = null;
        IsFeatureClassSelected = false;
        Neighborhood = null;
        ClientsAffected = null;
        SelectedFeaturesCount = 0;
        _selectedFeatures.Clear();
        StatusMessage = string.Empty;
        RaiseRunCanExecuteChanged();
    }

    private async Task OnRefreshSelectionAsync()
    {
        await UpdateSelectedFeatures();
        StatusMessage = SelectedFeaturesCount == 0 
            ? "No hay polígonos seleccionados" 
            : $"{SelectedFeaturesCount} polígono(s) seleccionado(s)";
        RaiseRunCanExecuteChanged();
    }

    private void RaiseRunCanExecuteChanged()
    {
        (RunCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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
            Title = "Seleccionar la Feature Class de Barrios",
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
            Title = "Seleccionar la Feature Class de Clientes",
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

    private async Task OnRunAsync()
    {
        if (!CanBuildPolygons)
        {
            MessageBox.Show("Debe seleccionar al menos 2 polígonos y completar todos los campos requeridos.",
                "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validar Workspace (debe ser una geodatabase .gdb)
        if (string.IsNullOrWhiteSpace(Workspace) || !Workspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Workspace))
        {
            MessageBox.Show("Seleccione una geodatabase (.gdb) válida en 'Workspace'.",
                "Workspace inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Procesando unión de polígonos...";

            await QueuedTask.Run(async () =>
            {
                var mv = MapView.Active;
                if (mv?.Map == null)
                {
                    StatusMessage = "No hay mapa activo";
                    return;
                }

                // 1. Obtener polígonos seleccionados
                var selectedPolygons = await GetSelectedPolygonsAsync();
                
                if (selectedPolygons.Count < 2)
                {
                    StatusMessage = "Debe seleccionar al menos 2 polígonos";
                    return;
                }

                // 2. Unir geometrías
                var unionedGeometry = UnionGeometries(selectedPolygons.Select(p => p.Geometry).ToList());
                
                if (unionedGeometry == null)
                {
                    StatusMessage = "Error al unir los polígonos";
                    return;
                }

                // 3. Combinar atributos
                var combinedAttributes = CombineAttributes(selectedPolygons);

                // 4. Guardar en Feature Class de destino
                var (saved, outputName, outGdbPath) = await SaveUnionedPolygon(unionedGeometry, combinedAttributes);

                if (saved)
                {
                    // Agregar la nueva capa al mapa y hacer zoom
                    try
                    {
                        var mvActive = MapView.Active;
                        var map = mvActive?.Map;
                        if (map != null)
                        {
                            // Evitar duplicados
                            var exists = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Any(l => l.Name.Equals(outputName, StringComparison.OrdinalIgnoreCase));
                            if (!exists)
                            {
                                var fcPath = System.IO.Path.Combine(outGdbPath, outputName);
                                var fcUri = new Uri(fcPath);
                                var lyr = LayerFactory.Instance.CreateLayer(fcUri, map) as FeatureLayer;
                                if (lyr != null && !string.IsNullOrEmpty(outputName))
                                    lyr.SetName(outputName);
                            }
                            // Zoom a la geometría unida
                            if (mvActive != null && unionedGeometry?.Extent != null)
                                mvActive.ZoomTo(unionedGeometry.Extent, new TimeSpan(0,0,0,0,400));
                        }
                    }
                    catch (Exception addEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"No se pudo agregar la capa al mapa: {addEx.Message}");
                    }
                    // 5. Si hay capa de barrios, hacer intersección
                    if (unionedGeometry != null && !string.IsNullOrWhiteSpace(Neighborhood))
                    {
                        await ProcessNeighborhoodIntersection(unionedGeometry, combinedAttributes);
                    }

                    // 6. Si hay capa de clientes, contar afectados
                    if (unionedGeometry != null && !string.IsNullOrWhiteSpace(ClientsAffected))
                    {
                        await ProcessClientsAffected(unionedGeometry, combinedAttributes);
                    }

                    var note = !string.IsNullOrWhiteSpace(Workspace) && !Workspace.Equals(outGdbPath, StringComparison.OrdinalIgnoreCase)
                        ? $" (creado en gdb alternativa: {System.IO.Path.GetFileName(outGdbPath)})" : string.Empty;
                    StatusMessage = $"✓ Unión completada: {selectedPolygons.Count} polígonos unidos -> {outputName} (capa agregada){note}";
                }
                else
                {
                    var detail = string.IsNullOrWhiteSpace(_lastSaveError) ? string.Empty : $" Detalle: {_lastSaveError}";
                    StatusMessage = $"✗ Error al guardar el polígono unido.{detail}";
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Error al procesar la unión: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            RaiseRunCanExecuteChanged();
        }
    }

    private async Task<List<PolygonFeature>> GetSelectedPolygonsAsync()
    {
        var result = new List<PolygonFeature>();
        
        try
        {
            var mv = MapView.Active;
            if (mv?.Map == null)
                return result;

            // Obtener todas las capas de polígonos en el mapa
            var polygonLayers = mv.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(layer => layer.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)
                .ToList();

            foreach (var layer in polygonLayers)
            {
                // Verificar si la capa coincide con la Feature Class seleccionada comparando el nombre del dataset
                bool layerMatches = true;
                if (!string.IsNullOrWhiteSpace(FeatureClass))
                {
                    layerMatches = await QueuedTask.Run(() =>
                    {
                        try
                        {
                            using var table = layer.GetTable();
                            var tableDef = table.GetDefinition();
                            var tableDatasetName = tableDef.GetName();
                            var (_, fcDatasetName) = ParseFeatureClassPath(FeatureClass!, Workspace);
                            return tableDatasetName.Equals(fcDatasetName, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }

                if (!layerMatches)
                    continue;

                // Obtener features seleccionadas en esta capa
                var selection = layer.GetSelection();
                if (selection.GetCount() == 0)
                    continue;

                using (var rowCursor = selection.Search())
                {
                    while (rowCursor.MoveNext())
                    {
                        using (var feature = rowCursor.Current as Feature)
                        {
                            if (feature != null)
                            {
                                result.Add(new PolygonFeature
                                {
                                    OID = feature.GetObjectID(),
                                    Geometry = feature.GetShape(),
                                    Attributes = GetFeatureAttributes(feature),
                                    LayerName = layer.Name
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en GetSelectedPolygonsAsync: {ex.Message}");
        }

        return result;
    }

    private Dictionary<string, object> GetFeatureAttributes(Feature feature)
    {
        var attributes = new Dictionary<string, object>();
        var fields = feature.GetTable().GetDefinition().GetFields();

        foreach (var field in fields)
        {
            if (field.FieldType != FieldType.Geometry &&
                field.FieldType != FieldType.OID &&
                field.FieldType != FieldType.GlobalID)
            {
                try
                {
                    var value = feature[field.Name];
                    if (value != null)
                    {
                        attributes[field.Name] = value;
                    }
                }
                catch { /* Ignorar campos que no se pueden leer */ }
            }
        }

        return attributes;
    }

    private Geometry? UnionGeometries(List<Geometry> geometries)
    {
        if (geometries == null || geometries.Count == 0)
            return null;

        Geometry result = geometries[0];

        for (int i = 1; i < geometries.Count; i++)
        {
            result = GeometryEngine.Instance.Union(result, geometries[i]);
        }

        return result;
    }

    private Dictionary<string, object> CombineAttributes(List<PolygonFeature> features)
    {
        var combined = new Dictionary<string, object>();

        if (features.Count == 0) return combined;

        var baseAttributes = features[0].Attributes;

        foreach (var attr in baseAttributes)
        {
            var fieldName = attr.Key;
            var fieldValue = attr.Value;

            // Si es el campo seleccionado, concatenar valores únicos
            if (fieldName.Equals(SelectedFeatureClassField, StringComparison.OrdinalIgnoreCase))
            {
                // Usar el identificador manual si está disponible
                if (!string.IsNullOrWhiteSpace(IdentifierText))
                    combined[fieldName] = IdentifierText!;
                else
                    combined[fieldName] = fieldValue;
            }
            // Para campos numéricos, sumar
            else if (fieldValue is int || fieldValue is long || fieldValue is double || 
                     fieldValue is float || fieldValue is decimal)
            {
                double sum = 0;
                foreach (var f in features)
                {
                    if (f.Attributes.ContainsKey(fieldName) && f.Attributes[fieldName] != null)
                    {
                        try
                        {
                            sum += Convert.ToDouble(f.Attributes[fieldName]);
                        }
                        catch { /* Ignorar valores que no se pueden convertir */ }
                    }
                }
                combined[fieldName] = sum;
            }
            // Para campos de texto, tomar el primer valor no nulo
            else if (fieldValue is string)
            {
                var firstNonEmpty = features
                    .Where(f => f.Attributes.ContainsKey(fieldName))
                    .Select(f => f.Attributes[fieldName]?.ToString())
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

                if (firstNonEmpty != null)
                {
                    combined[fieldName] = firstNonEmpty;
                }
            }
            else
            {
                combined[fieldName] = fieldValue;
            }
        }

        return combined;
    }

    private async Task<(bool Saved, string OutputDatasetName, string OutputGdbPath)> SaveUnionedPolygon(Geometry geometry, Dictionary<string, object> attributes)
    {
        try
        {
            // Siempre usar el Workspace como gdb de salida. El datasetName base se toma de la FC si existe
            string? datasetNameBase = null;
            string gdbPath = Workspace;
            // Info de la FC origen (para replicar SR/campo)
            string? srcGdbPath = null;
            string? srcDatasetName = null;
            if (!string.IsNullOrWhiteSpace(FeatureClass))
            {
                var parsed = ParseFeatureClassPath(FeatureClass!, Workspace);
                datasetNameBase = parsed.datasetName;
                srcGdbPath = parsed.gdbPath;
                srcDatasetName = parsed.datasetName;
            }

            if (string.IsNullOrWhiteSpace(gdbPath))
                return (false, string.Empty, string.Empty);

            // Validar que el workspace sea una geodatabase
            if (string.IsNullOrWhiteSpace(gdbPath) || !gdbPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(gdbPath))
            {
                System.Diagnostics.Debug.WriteLine($"Workspace inválido para salida: '{gdbPath}'");
                // Intentar con la geodatabase por defecto del proyecto
                var projGdb = Project.Current?.DefaultGeodatabasePath;
                if (!string.IsNullOrWhiteSpace(projGdb) && projGdb.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) && Directory.Exists(projGdb))
                {
                    gdbPath = projGdb;
                }
                else
                {
                    return (false, string.Empty, string.Empty);
                }
            }

            var connPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdbInitial = new Geodatabase(connPath);
            // Usar 'activeGdb' para permitir fallback sin reasignar la variable 'using'
            Geodatabase activeGdb = gdbInitial;
            FeatureClassDefinition? srcDef = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(srcGdbPath) && !string.IsNullOrWhiteSpace(srcDatasetName) && Directory.Exists(srcGdbPath))
                {
                    var srcConn = new FileGeodatabaseConnectionPath(new Uri(srcGdbPath));
                    using var srcGdb = new Geodatabase(srcConn);
                    using var trySrc = srcGdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(srcDatasetName);
                    srcDef = trySrc?.GetDefinition();
                }
            }
            catch { /* Puede no existir; tolerar */ }

            // Determinar nombre de salida basado en dataset origen e identificador seleccionado
            var idPreferred = IdentifierText;
            var idPart = SanitizeName(idPreferred ?? "UNION");
            if (string.IsNullOrWhiteSpace(datasetNameBase))
            {
                // Sin FC definida: intenta tomar nombre de la primera capa de polígonos seleccionada
                var mv = MapView.Active;
                var firstPolyLayerName = mv?.Map?.GetLayersAsFlattenedList()?.OfType<FeatureLayer>()
                    .FirstOrDefault(l => l.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)?.Name;
                datasetNameBase = string.IsNullOrWhiteSpace(firstPolyLayerName) ? "UnionOutput" : firstPolyLayerName;
            }
            var baseOutputName = $"{datasetNameBase}_UNION_{idPart}";
            var outputName = GetUniqueDatasetName(activeGdb, baseOutputName);

            // Crear FC de salida si no existe; con fallback a gdb por defecto si hay bloqueo por edición
            if (!DatasetExists(activeGdb, outputName))
            {
                var sr = geometry?.SpatialReference ?? srcDef?.GetSpatialReference() ?? MapView.Active?.Map?.SpatialReference;
                if (sr == null)
                    return (false, string.Empty, gdbPath);
                var shapeDesc = new ArcGIS.Core.Data.DDL.ShapeDescription(GeometryType.Polygon, sr);

                // Crear solo el campo identificador seleccionado, con el mismo tipo que en la FC origen
                var idFieldName = SelectedFeatureClassField;
                Field? srcIdField = null;
                if (!string.IsNullOrWhiteSpace(idFieldName) && srcDef != null)
                {
                    srcIdField = srcDef.GetFields()
                        .FirstOrDefault(f => f.Name.Equals(idFieldName, StringComparison.OrdinalIgnoreCase));
                }

                var fields = new List<ArcGIS.Core.Data.DDL.FieldDescription>();
                if (!string.IsNullOrWhiteSpace(idFieldName))
                {
                    if (srcIdField != null)
                    {
                        var fd = new ArcGIS.Core.Data.DDL.FieldDescription(srcIdField.Name, srcIdField.FieldType);
                        if (srcIdField.FieldType == FieldType.String)
                            fd.Length = srcIdField.Length;
                        if (srcIdField.FieldType == FieldType.Double || srcIdField.FieldType == FieldType.Single || srcIdField.FieldType == FieldType.Integer || srcIdField.FieldType == FieldType.SmallInteger)
                        {
                            fd.Precision = srcIdField.Precision;
                            fd.Scale = srcIdField.Scale;
                        }
                        fields.Add(fd);
                    }
                    else
                    {
                        // Fallback: crear campo string
                        var fd = new ArcGIS.Core.Data.DDL.FieldDescription(idFieldName, FieldType.String)
                        {
                            Length = 255
                        };
                        fields.Add(fd);
                    }
                }

                bool created = false;
                string? fallbackGdb = null;
                try
                {
                    var fcDesc = new FeatureClassDescription(outputName, fields, shapeDesc);
                    var sb = new SchemaBuilder(activeGdb);
                    sb.Create(fcDesc);
                    created = sb.Build();
                }
                catch (Exception ex)
                {
                    _lastSaveError = ex.Message;
                    created = false;
                }

                if (!created)
                {
                    // intentar fallback a la gdb por defecto del proyecto
                    var projGdb = Project.Current?.DefaultGeodatabasePath;
                    if (!string.IsNullOrWhiteSpace(projGdb) && projGdb.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) && Directory.Exists(projGdb))
                    {
                        try
                        {
                            var fbConn = new FileGeodatabaseConnectionPath(new Uri(projGdb));
                            var fbGdb = new Geodatabase(fbConn);
                            fallbackGdb = projGdb;
                            // Recalcular nombre único en gdb fallback
                            outputName = GetUniqueDatasetName(fbGdb, baseOutputName);
                            var fcDesc = new FeatureClassDescription(outputName, fields, shapeDesc);
                            var sb2 = new SchemaBuilder(fbGdb);
                            sb2.Create(fcDesc);
                            created = sb2.Build();
                            if (created)
                            {
                                // Cambiar gdb de trabajo a la fallback
                                gdbPath = projGdb;
                                // Usar fallback como gdb activa para las siguientes operaciones
                                activeGdb = fbGdb;
                            }
                        }
                        catch (Exception ex)
                        {
                            _lastSaveError = string.IsNullOrWhiteSpace(_lastSaveError) ? ex.Message : _lastSaveError + "; " + ex.Message;
                            created = false;
                        }
                    }
                }

                if (!created)
                {
                    return (false, outputName, gdbPath);
                }
            }

            using var outFc = activeGdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(outputName);
            using var outDef = outFc.GetDefinition();

            // 1) Intentar con EditOperation (cuando la edición está habilitada)
            var editOp = new EditOperation
            {
                Name = "Insertar polígono unido",
                ShowModalMessageAfterFailure = false
            };

            editOp.Callback(context =>
            {
                using (var rowBuffer = outFc.CreateRowBuffer())
                {
                    rowBuffer[outDef.GetShapeField()] = geometry;

                    if (!string.IsNullOrWhiteSpace(SelectedFeatureClassField))
                    {
                        try
                        {
                            var outFieldIndex = outDef.FindField(SelectedFeatureClassField!);
                            if (outFieldIndex >= 0)
                            {
                                var outField = outDef.GetFields()[outFieldIndex];
                                var value = IdentifierText;
                                if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(SelectedFeatureClassField) && attributes.TryGetValue(SelectedFeatureClassField!, out var v) && v != null)
                                {
                                    rowBuffer[SelectedFeatureClassField!] = v;
                                }
                                else if (!string.IsNullOrWhiteSpace(value))
                                {
                                    if (TryConvertForField(outField, value!, out var converted))
                                        rowBuffer[SelectedFeatureClassField!] = converted;
                                    else if (outField.FieldType == FieldType.String)
                                        rowBuffer[SelectedFeatureClassField!] = value!;
                                }
                            }
                        }
                        catch { }
                    }

                    using (var newFeature = outFc.CreateRow(rowBuffer))
                    {
                        context.Invalidate(newFeature);
                    }
                }
            }, outFc);

            var ok = await editOp.ExecuteAsync();
            if (!ok)
            {
                var err = editOp.ErrorMessage;
                System.Diagnostics.Debug.WriteLine($"EditOperation failed creating unioned feature in '{outputName}'. Error: {err}");
                _lastSaveError = err;
                // 2) Si falló por edición no habilitada, intentar inserción directa
                if (!string.IsNullOrWhiteSpace(err) && err.IndexOf("not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        if (geometry != null && attributes != null)
                        {
                            var directOk2 = InsertUnionFeatureDirect(outFc, outDef, geometry, attributes);
                            if (directOk2)
                                return (true, outputName, gdbPath);
                        }
                    }
                    catch (Exception dirEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Direct insert (fallback) failed: {dirEx.Message}");
                        _lastSaveError = dirEx.Message;
                    }
                }
            }
            return (ok, outputName, gdbPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al guardar: {ex.Message}");
            _lastSaveError = ex.Message;
            return (false, string.Empty, string.Empty);
        }
    }

    private async Task AddOutputLayerAndZoomAsync(string gdbPath, string datasetName, Geometry unionGeometry)
    {
        await QueuedTask.Run(() =>
        {
            try
            {
                var uri = new Uri(Path.Combine(gdbPath, datasetName));
                var map = MapView.Active?.Map;
                if (map == null) return;
                var lyr = LayerFactory.Instance.CreateLayer(uri, map) as FeatureLayer;
                if (lyr != null)
                {
                    lyr.SetName(datasetName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo agregar la capa de salida: {ex.Message}");
            }
        });

        try
        {
            if (unionGeometry != null)
            {
                await MapView.Active.ZoomToAsync(unionGeometry.Extent, new TimeSpan(0,0,0,0,250));
            }
        }
        catch { }
    }

    private static string SanitizeName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "UNION";
        // Reemplazar caracteres no válidos por guión bajo y limitar longitud
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var cleaned = new string(input.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());
        // Evitar comenzar con número
        if (cleaned.Length > 0 && char.IsDigit(cleaned[0]))
            cleaned = "_" + cleaned;
        // Limitar a 50 caracteres para nombres de FC
        return cleaned.Length > 50 ? cleaned.Substring(0, 50) : cleaned;
    }

    private static bool DatasetExists(Geodatabase gdb, string name)
    {
        try
        {
            using var fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(name);
            return fc != null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetUniqueDatasetName(Geodatabase gdb, string baseName)
    {
        var name = baseName;
        int i = 1;
        while (DatasetExists(gdb, name))
        {
            name = baseName;
            var suffix = $"_{i}";
            if (name.Length + suffix.Length > 63)
                name = name.Substring(0, Math.Max(1, 63 - suffix.Length));
            name += suffix;
            i++;
        }
        return name;
    }

    private static bool TryConvertForField(Field field, string value, out object? converted)
    {
        converted = null;
        try
        {
            switch (field.FieldType)
            {
                case FieldType.String:
                    converted = value;
                    return true;
                case FieldType.Integer:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                    {
                        converted = iv;
                        return true;
                    }
                    break;
                case FieldType.SmallInteger:
                    if (short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sv))
                    {
                        converted = sv;
                        return true;
                    }
                    break;
                case FieldType.Double:
                    if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var dv))
                    {
                        converted = dv;
                        return true;
                    }
                    break;
                case FieldType.Single:
                    if (float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var fv))
                    {
                        converted = fv;
                        return true;
                    }
                    break;
            }
        }
        catch { }
        return false;
    }

    // Inserción directa sin EditOperation: funciona cuando la edición de la aplicación está deshabilitada
    private bool InsertUnionFeatureDirect(ArcGIS.Core.Data.FeatureClass outFc, FeatureClassDefinition outDef, Geometry geometry, Dictionary<string, object> attributes)
    {
        if (geometry == null) return false;
        if (attributes == null) attributes = new Dictionary<string, object>();
        using (var rowBuffer = outFc.CreateRowBuffer())
        {
            rowBuffer[outDef.GetShapeField()] = geometry;

            if (!string.IsNullOrWhiteSpace(SelectedFeatureClassField))
            {
                try
                {
                    var outFieldIndex = outDef.FindField(SelectedFeatureClassField);
                    if (outFieldIndex >= 0)
                    {
                        var outField = outDef.GetFields()[outFieldIndex];
                        var value = IdentifierText;
                        if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(SelectedFeatureClassField) && attributes.TryGetValue(SelectedFeatureClassField!, out var v) && v != null)
                        {
                            rowBuffer[SelectedFeatureClassField!] = v;
                        }
                        else if (!string.IsNullOrWhiteSpace(value))
                        {
                            if (TryConvertForField(outField, value!, out var converted))
                                rowBuffer[SelectedFeatureClassField!] = converted;
                            else if (outField.FieldType == FieldType.String)
                                rowBuffer[SelectedFeatureClassField!] = value!;
                        }
                    }
                }
                catch { }
            }

            using (var newFeature = outFc.CreateRow(rowBuffer))
            {
                // Inserción directa, no se requiere context.Invalidate
            }
        }

        return true;
    }

    private async Task ProcessNeighborhoodIntersection(Geometry unionGeometry, Dictionary<string, object> attributes)
    {
        try
        {
            var (gdbPath, datasetName) = ParseFeatureClassPath(Neighborhood!, Workspace);
            
            if (string.IsNullOrWhiteSpace(gdbPath) || string.IsNullOrWhiteSpace(datasetName))
                return;

            var connPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new Geodatabase(connPath);
            using var fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(datasetName);

            var spatialFilter = new SpatialQueryFilter
            {
                FilterGeometry = unionGeometry,
                SpatialRelationship = SpatialRelationship.Intersects
            };

            using (var cursor = fc.Search(spatialFilter, false))
            {
                var neighborhoods = new List<string>();
                
                while (cursor.MoveNext())
                {
                    using (var feature = cursor.Current as Feature)
                    {
                        if (feature != null)
                        {
                            // Buscar campo de nombre de barrio (ajustar según tu esquema)
                            var nameField = FindNameField(fc.GetDefinition());
                            if (nameField != null)
                            {
                                var name = feature[nameField]?.ToString();
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    neighborhoods.Add(name);
                                }
                            }
                        }
                    }
                }

                if (neighborhoods.Any())
                {
                    StatusMessage += $" | Barrios: {string.Join(", ", neighborhoods)}";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al procesar barrios: {ex.Message}");
        }
    }

    private async Task ProcessClientsAffected(Geometry unionGeometry, Dictionary<string, object> attributes)
    {
        try
        {
            var (gdbPath, datasetName) = ParseFeatureClassPath(ClientsAffected!, Workspace);
            
            if (string.IsNullOrWhiteSpace(gdbPath) || string.IsNullOrWhiteSpace(datasetName))
                return;

            var connPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new Geodatabase(connPath);
            using var fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(datasetName);

            var spatialFilter = new SpatialQueryFilter
            {
                FilterGeometry = unionGeometry,
                SpatialRelationship = SpatialRelationship.Contains
            };

            int clientCount = 0;
            using (var cursor = fc.Search(spatialFilter, false))
            {
                while (cursor.MoveNext())
                {
                    clientCount++;
                }
            }

            StatusMessage += $" | Clientes afectados: {clientCount}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al contar clientes: {ex.Message}");
        }
    }

    private string? FindNameField(FeatureClassDefinition definition)
    {
        var fields = definition.GetFields();
        var possibleNames = new[] { "NOMBRE", "NAME", "BARRIO", "NEIGHBORHOOD", "NOM_BARRIO" };

        foreach (var field in fields)
        {
            if (possibleNames.Any(n => field.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
            {
                return field.Name;
            }
        }

        return fields.FirstOrDefault(f => f.FieldType == FieldType.String)?.Name;
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
                var connPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
                using var gdb = new Geodatabase(connPath);
                using var fc = gdb.OpenDataset<ArcGIS.Core.Data.FeatureClass>(datasetName);
                var def = fc.GetDefinition();
                string[] filter = ["objectid", "shape", "globalid"];

                // Aceptar campos de texto y numéricos como posibles identificadores
                var allowed = new[] { FieldType.String, FieldType.Integer, FieldType.SmallInteger, FieldType.Double, FieldType.Single };
                return def.GetFields()
                    .Where(f => !filter.Contains(f.Name.ToLower()) && allowed.Contains(f.FieldType))
                    .Select(it => it.Name)
                    .OrderBy(it => it)
                    .ToList();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al obtener campos: {ex.Message}");
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
            var datasetName = string.IsNullOrWhiteSpace(remainder) ? Path.GetFileNameWithoutExtension(gdbPath) : Path.GetFileName(remainder);
            return (gdbPath, datasetName);
        }

        var datasetNameNoGdb = Path.GetFileName(featureClass);
        return (workspace, string.IsNullOrWhiteSpace(datasetNameNoGdb) ? featureClass : datasetNameNoGdb);
    }

    private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
    {
        _selectedFeatures.Clear();
        SelectedFeaturesCount = 0;

        QueuedTask.Run(UpdateSelectedFeatures);
    }

    private async Task UpdateSelectedFeatures()
    {
        try
        {
            var selectedPolygons = await GetSelectedPolygonsAlternativeAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _selectedFeatures.Clear();
                foreach (var poly in selectedPolygons)
                {
                    var displayText = $"{poly.LayerName} [OID: {poly.OID}]";
                    _selectedFeatures.Add(displayText);
                }

                SelectedFeaturesCount = selectedPolygons.Count;

                // Actualizar UI en el hilo principal
                NotifyPropertyChanged(nameof(SelectedFeatures));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al actualizar selección: {ex.Message}");
        }
    }
    private async Task<List<PolygonFeature>> GetSelectedPolygonsAlternativeAsync()
    {
        var result = new List<PolygonFeature>();
        
        await QueuedTask.Run(() =>
        {
            var mv = MapView.Active;
            if (mv?.Map == null) return;

            // Obtener la selección actual del mapa
            var selectionSet = mv.Map.GetSelection();
            if (selectionSet.Count == 0) return;

            foreach (var layerSelection in selectionSet.ToDictionary())
            {
                var layer = layerSelection.Key;
                var oids = layerSelection.Value;
                
                if (layer is FeatureLayer featureLayer && 
                    featureLayer.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)
                {
                    // Ya no filtramos por FeatureClass; tomamos todas las capas de polígonos seleccionadas

                    var queryFilter = new QueryFilter
                    {
                        ObjectIDs = oids
                    };

                    using (var featureCursor = featureLayer.Search(queryFilter))
                    {
                        while (featureCursor.MoveNext())
                        {
                            using (var feature = featureCursor.Current as Feature)
                            {
                                if (feature != null)
                                {
                                    result.Add(new PolygonFeature
                                    {
                                        OID = feature.GetObjectID(),
                                        Geometry = feature.GetShape(),
                                        Attributes = GetFeatureAttributes(feature),
                                        LayerName = featureLayer.Name
                                    });
                                }
                            }
                        }
                    }
                }
            }
        });

        return result;
    }
    // Eliminado: ya no se recolectan valores de identificador desde la selección

    #region Helper Classes

    internal class PolygonFeature
    {
        public long OID { get; set; }
        public Geometry Geometry { get; set; } = null!;
        public Dictionary<string, object> Attributes { get; set; } = new();
        public string LayerName { get; set; } = string.Empty;
    }

    #endregion
}