using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Presentation.Base;
using EAABAddIn.Src.Application.UseCases.Validation;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class MigrationViewModel : BusyViewModelBase
{
    public override string DisplayName => "Migración";
    public override string Tooltip => "Migrar datos entre capas";

    // Entradas del script
    public string OutputFolder { get => _outputFolder; set { if (_outputFolder != value) { _outputFolder = value; NotifyPropertyChanged(nameof(OutputFolder)); } } }
    public string XmlSchemaPath { get => _xmlSchemaPath; set { if (_xmlSchemaPath != value) { _xmlSchemaPath = value; NotifyPropertyChanged(nameof(XmlSchemaPath)); } } }
    public string L_Acu_Origen { get => _lAcuOrigen; set { if (_lAcuOrigen != value) { _lAcuOrigen = value; NotifyPropertyChanged(nameof(L_Acu_Origen)); } } }
    public string P_Acu_Origen { get => _pAcuOrigen; set { if (_pAcuOrigen != value) { _pAcuOrigen = value; NotifyPropertyChanged(nameof(P_Acu_Origen)); } } }
    public string L_Alc_Origen { get => _lAlcOrigen; set { if (_lAlcOrigen != value) { _lAlcOrigen = value; NotifyPropertyChanged(nameof(L_Alc_Origen)); } } }
    public string P_Alc_Origen { get => _pAlcOrigen; set { if (_pAlcOrigen != value) { _pAlcOrigen = value; NotifyPropertyChanged(nameof(P_Alc_Origen)); } } }
    public string L_Alc_Pluv_Origen { get => _lAlcPluvOrigen; set { if (_lAlcPluvOrigen != value) { _lAlcPluvOrigen = value; NotifyPropertyChanged(nameof(L_Alc_Pluv_Origen)); } } }
    public string P_Alc_Pluv_Origen { get => _pAlcPluvOrigen; set { if (_pAlcPluvOrigen != value) { _pAlcPluvOrigen = value; NotifyPropertyChanged(nameof(P_Alc_Pluv_Origen)); } } }
    public bool MigrarConAdvertencias { get => _migrarConAdvertencias; set { if (_migrarConAdvertencias != value) { _migrarConAdvertencias = value; NotifyPropertyChanged(nameof(MigrarConAdvertencias)); } } }

    private string _outputFolder = string.Empty;
    private string _xmlSchemaPath = string.Empty;
    private string _lAcuOrigen = string.Empty;
    private string _pAcuOrigen = string.Empty;
    private string _lAlcOrigen = string.Empty;
    private string _pAlcOrigen = string.Empty;
    private string _lAlcPluvOrigen = string.Empty;
    private string _pAlcPluvOrigen = string.Empty;
    private bool _migrarConAdvertencias = false;

    private string _sourcePath = string.Empty;
    public string SourcePath
    {
        get => _sourcePath;
        set { if (_sourcePath != value) { _sourcePath = value; NotifyPropertyChanged(nameof(SourcePath)); } }
    }

    private string _targetPath = string.Empty;
    public string TargetPath
    {
        get => _targetPath;
        set { if (_targetPath != value) { _targetPath = value; NotifyPropertyChanged(nameof(TargetPath)); } }
    }

    private bool _useSelection = false;
    public bool UseSelection
    {
        get => _useSelection;
        set { if (_useSelection != value) { _useSelection = value; NotifyPropertyChanged(nameof(UseSelection)); } }
    }

    private bool _testSchema = false;
    public bool TestSchema
    {
        get => _testSchema;
        set { if (_testSchema != value) { _testSchema = value; NotifyPropertyChanged(nameof(TestSchema)); } }
    }

    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseTargetCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand BrowseXmlSchemaCommand { get; }
    public ICommand BrowseLAcuOrigenCommand { get; }
    public ICommand BrowsePAcuOrigenCommand { get; }
    public ICommand BrowseLAlcOrigenCommand { get; }
    public ICommand BrowsePAlcOrigenCommand { get; }
    public ICommand BrowseLAlcPluvOrigenCommand { get; }
    public ICommand BrowsePAlcPluvOrigenCommand { get; }
    public ICommand OpenReportsFolderCommand { get; }
    public ICommand RunCommand { get; }

    public MigrationViewModel()
    {
        StatusMessage = "Seleccione origen y destino y pulse Migrar.";
        BrowseSourceCommand = new RelayCommand(BrowseSource);
        BrowseTargetCommand = new RelayCommand(BrowseTarget);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        BrowseXmlSchemaCommand = new RelayCommand(BrowseXmlSchema);
        BrowseLAcuOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Acu_Origen = path));
        BrowsePAcuOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Acu_Origen = path));
        BrowseLAlcOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Alc_Origen = path));
        BrowsePAlcOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Alc_Origen = path));
        BrowseLAlcPluvOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Alc_Pluv_Origen = path));
        BrowsePAlcPluvOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Alc_Pluv_Origen = path));
        OpenReportsFolderCommand = new RelayCommand(OpenReportsFolder);
        RunCommand = new AsyncRelayCommand(RunAsync);
    }

    private void BrowseSource()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses");
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Origen",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };
        if (dlg.ShowDialog() == true && dlg.Items?.Any() == true)
            SourcePath = dlg.Items.First().Path;
    }

    private void BrowseTarget()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses");
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar Destino",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };
        if (dlg.ShowDialog() == true && dlg.Items?.Any() == true)
            TargetPath = dlg.Items.First().Path;
    }

    private async Task RunAsync()
    {
        IsBusy = true;
        StatusMessage = "Validando y migrando...";
        try
        {
            // 1) Validaciones básicas y generación de reportes CSV (scaffold inicial)
            var validator = new ValidateDatasetsUseCase();
            var validation = await validator.Invoke(new()
            {
                OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? Project.Current?.HomeFolderPath ?? System.IO.Path.GetTempPath() : OutputFolder,
                Datasets = new()
                {
                    new DatasetInput("L_ACU_ORIGEN", L_Acu_Origen),
                    new DatasetInput("P_ACU_ORIGEN", P_Acu_Origen),
                    new DatasetInput("L_ALC_ORIGEN", L_Alc_Origen),
                    new DatasetInput("P_ALC_ORIGEN", P_Alc_Origen),
                    new DatasetInput("L_ALC_PLUV_ORIGEN", L_Alc_Pluv_Origen),
                    new DatasetInput("P_ALC_PLUV_ORIGEN", P_Alc_Pluv_Origen),
                }
            });

            // actualizar estado de reportes
            HasWarnings = validation.TotalWarnings > 0;
            ReportsFolder = validation.ReportFolder ?? string.Empty;
            ReportFiles.Clear();
            foreach (var f in validation.ReportFiles ?? Enumerable.Empty<string>())
                ReportFiles.Add(f);
            NotifyPropertyChanged(nameof(HasWarnings));
            NotifyPropertyChanged(nameof(ReportsFolder));

            if (validation.TotalWarnings > 0 && !MigrarConAdvertencias)
            {
                StatusMessage = $"Hay {validation.TotalWarnings} advertencias. Se generaron reportes en: {validation.ReportFolder}. Activa 'Migrar con advertencias' para continuar.";
                return;
            }

            // 2) Crear FGDB desde XML si está definido
            if (!string.IsNullOrWhiteSpace(OutputFolder) && !string.IsNullOrWhiteSpace(XmlSchemaPath))
            {
                var create = new CreateGdbFromXmlUseCase();
                var (okGdb, gdbPath, msgGdb) = await create.Invoke(OutputFolder, "GDB_Cargue.gdb", XmlSchemaPath);
                if (!okGdb)
                {
                    StatusMessage = $"Error creando GDB: {msgGdb}";
                    return;
                }
            }

            // Camino corto: si el usuario quiere hacer una migración simple origen->destino
            if (!string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(TargetPath))
            {
                var options = new AppendOptions
                {
                    SourcePath = SourcePath,
                    TargetPath = TargetPath,
                    UseSelection = UseSelection,
                    SchemaType = TestSchema ? "TEST" : "NO_TEST",
                    FieldMappings = string.Empty
                };
                var append = new AppendFeaturesUseCase();
                var result = await append.Invoke(options);
                if (!result.ok)
                {
                    StatusMessage = $"Append falló: {result.message}";
                    return;
                }
            }

            // TODO: Implementar port 1:1 de validaciones y migración por tipo usando los caminos del script Python
            StatusMessage = "Proceso finalizado. Si definiste FGDB+XML, revisa la GDB creada; si definiste origen/destino, Append terminó.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private void BrowseOutputFolder()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_folders");
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar carpeta de salida",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };
        if (dlg.ShowDialog() == true && dlg.Items?.Any() == true)
            OutputFolder = dlg.Items.First().Path;
    }

    private void BrowseXmlSchema()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_all");
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar XML de esquema",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };
        if (dlg.ShowDialog() == true && dlg.Items?.Any() == true)
            XmlSchemaPath = dlg.Items.First().Path;
    }

    private void BrowseFeatureClass(Action<string> setter)
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_featureClasses");
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar feature class",
            BrowseFilter = filter,
            MultiSelect = false,
            InitialLocation = Project.Current?.HomeFolderPath
        };
        if (dlg.ShowDialog() == true && dlg.Items?.Any() == true)
            setter?.Invoke(dlg.Items.First().Path);
    }

    // Reportes
    public bool HasWarnings { get => _hasWarnings; private set { if (_hasWarnings != value) { _hasWarnings = value; NotifyPropertyChanged(nameof(HasWarnings)); } } }
    public string ReportsFolder { get => _reportsFolder; private set { if (_reportsFolder != value) { _reportsFolder = value; NotifyPropertyChanged(nameof(ReportsFolder)); } } }
    public ObservableCollection<string> ReportFiles { get; } = new();
    private bool _hasWarnings = false;
    private string _reportsFolder = string.Empty;

    private void OpenReportsFolder()
    {
        try
        {
            var folder = string.IsNullOrWhiteSpace(ReportsFolder) ? OutputFolder : ReportsFolder;
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = folder,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
        }
        catch { /* ignorar */ }
    }
}
