#nullable enable

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

    private readonly ValidateDatasetsUseCase _datasetValidatorUseCase = new ValidateDatasetsUseCase();
    private readonly CreateGdbFromXmlUseCase _createGdbFromXmlUseCase = new CreateGdbFromXmlUseCase();

    public ICommand WorkspaceCommand { get; private set; }
    public ICommand XmlSchemaCommand { get; private set; }
    public ICommand BrowseLAcuOrigenCommand { get; private set; }
    public ICommand BrowsePAcuOrigenCommand { get; private set; }
    public ICommand BrowseLAlcPluvOrigenCommand { get; private set; }
    public ICommand BrowsePAlcPluvOrigenCommand { get; private set; }
    public ICommand RunCommand { get; private set; }

    public MigrationViewModel()
    {
        StatusMessage = "Seleccione origen y destino y pulse Migrar.";
        WorkspaceCommand = new RelayCommand(BrowseOutputFolder);
        XmlSchemaCommand = new RelayCommand(BrowseXmlSchema);
        BrowseLAcuOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Acu_Origen = path));
        BrowsePAcuOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Acu_Origen = path));
        BrowseLAlcPluvOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Alc_Pluv_Origen = path));
        BrowsePAlcPluvOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Alc_Pluv_Origen = path));
        RunCommand = new AsyncRelayCommand(RunAsync);
        // OpenReportsFolderCommand = new RelayCommand(OpenReportsFolder);
    }

    private string? _workspace = null;
    public string? Workspace
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

    private string? _xmlSchemaPath = null;
    public string? XmlSchemaPath
    {
        get => _xmlSchemaPath;
        set
        {
            if (_xmlSchemaPath != value)
            {
                _xmlSchemaPath = value;
                NotifyPropertyChanged(nameof(XmlSchemaPath));
            }
        }
    }

    private string? _lAcuOrigen = null;
    public string? L_Acu_Origen
    {
        get => _lAcuOrigen;
        set
        {
            if (_lAcuOrigen != value)
            {
                _lAcuOrigen = value;

                NotifyPropertyChanged(nameof(L_Acu_Origen));
            }
        }
    }

    private string? _pAcuOrigen = null;
    public string? P_Acu_Origen
    {
        get => _pAcuOrigen;
        set
        {
            if (_pAcuOrigen != value)
            {
                _pAcuOrigen = value;
                NotifyPropertyChanged(nameof(P_Acu_Origen));
            }
        }
    }

    private string? _lAlcOrigen = null;
    public string? L_Alc_Origen
    {
        get => _lAlcOrigen;
        set
        {
            if (_lAlcOrigen != value)
            {
                _lAlcOrigen = value;
                NotifyPropertyChanged(nameof(L_Alc_Origen));
            }
        }
    }

    private string? _pAlcOrigen = null;
    public string? P_Alc_Origen
    {
        get => _pAlcOrigen;

        set
        {
            if (_pAlcOrigen != value)
            {
                _pAlcOrigen = value;
                NotifyPropertyChanged(nameof(P_Alc_Origen));
            }
        }
    }

    private string? _lAlcPluvOrigen = null;
    public string? L_Alc_Pluv_Origen
    {
        get => _lAlcPluvOrigen;
        set
        {
            if (_lAlcPluvOrigen != value)
            {
                _lAlcPluvOrigen = value;
                NotifyPropertyChanged(nameof(L_Alc_Pluv_Origen));
            }
        }
    }

    private string? _pAlcPluvOrigen = null;
    public string? P_Alc_Pluv_Origen
    {
        get => _pAlcPluvOrigen;
        set
        {
            if (_pAlcPluvOrigen != value)
            {
                _pAlcPluvOrigen = value;
                NotifyPropertyChanged(nameof(P_Alc_Pluv_Origen));
            }
        }
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
        {
            Workspace = dlg.Items.First().Path;
        }
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
        {
            XmlSchemaPath = dlg.Items.First().Path;
        }
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
        {
            setter?.Invoke(dlg.Items.First().Path);
        }
    }

    private async Task RunAsync()
    {
        IsBusy = true;
        StatusMessage = "Validando y migrando...";

        if (Workspace is null)
        {
            StatusMessage = "Error: Debes seleccionar una carpeta de salida.";
            IsBusy = false;
            return;
        }

        if (XmlSchemaPath is null)
        {
            StatusMessage = "Error: Debes seleccionar un XML de esquema.";
            IsBusy = false;
            return;
        }

        try
        {
            var validation = await _datasetValidatorUseCase.Invoke(new()
            {
                OutputFolder = Workspace,
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
            // HasWarnings = validation.TotalWarnings > 0;
            // ReportsFolder = validation.ReportFolder ?? string.Empty;
            // ReportFiles.Clear();
            // foreach (var f in validation.ReportFiles ?? Enumerable.Empty<string>())
            //     ReportFiles.Add(f);
            // NotifyPropertyChanged(nameof(HasWarnings));
            // NotifyPropertyChanged(nameof(ReportsFolder));

            // if (validation.TotalWarnings > 0 && !MigrarConAdvertencias)
            // {
            //     StatusMessage = $"Hay {validation.TotalWarnings} advertencias. Se generaron reportes en: {validation.ReportFolder}. Activa 'Migrar con advertencias' para continuar.";
            //     return;
            // }


            var (okGdb, gdbPath, msgGdb) = await _createGdbFromXmlUseCase.Invoke(Workspace, "GDB_Cargue.gdb", XmlSchemaPath);

            if (!okGdb)
            {
                StatusMessage = $"Error creando GDB: {msgGdb}";
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: $"Error creando GDB desde XML: {msgGdb}",
                    caption: "Error",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Error
                );
                return;
            }

            // Camino corto: si el usuario quiere hacer una migración simple origen->destino
            // if (!string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(TargetPath))
            // {
            //     var options = new AppendOptions
            //     {
            //         SourcePath = SourcePath,
            //         TargetPath = TargetPath,
            //         UseSelection = UseSelection,
            //         SchemaType = TestSchema ? "TEST" : "NO_TEST",
            //         FieldMappings = string.Empty
            //     };
            //     var append = new AppendFeaturesUseCase();
            //     var result = await append.Invoke(options);
            //     if (!result.ok)
            //     {
            //         StatusMessage = $"Append falló: {result.message}";
            //         return;
            //     }
            // }

            // TODO: Implementar port 1:1 de validaciones y migración por tipo usando los caminos del script Python
            StatusMessage = "Proceso finalizado. Si definiste FGDB+XML, revisa la GDB creada; si definiste origen/destino, Append terminó.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    //     public bool MigrarConAdvertencias { get => _migrarConAdvertencias; set { if (_migrarConAdvertencias != value) { _migrarConAdvertencias = value; NotifyPropertyChanged(nameof(MigrarConAdvertencias)); } } }
    //     private bool _migrarConAdvertencias = false;
    //     // Reportes
    //     public bool HasWarnings { get => _hasWarnings; private set { if (_hasWarnings != value) { _hasWarnings = value; NotifyPropertyChanged(nameof(HasWarnings)); } } }
    //     public string ReportsFolder { get => _reportsFolder; private set { if (_reportsFolder != value) { _reportsFolder = value; NotifyPropertyChanged(nameof(ReportsFolder)); } } }
    //     public ObservableCollection<string> ReportFiles { get; } = new();
    //     private bool _hasWarnings = false;
    //     private string _reportsFolder = string.Empty;

    //     private void OpenReportsFolder()
    //     {
    //         try
    //         {
    //             var folder = string.IsNullOrWhiteSpace(ReportsFolder) ? OutputFolder : ReportsFolder;
    //             if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
    //             {
    //                 var psi = new ProcessStartInfo
    //                 {
    //                     FileName = "explorer.exe",
    //                     Arguments = folder,
    //                     UseShellExecute = true
    //                 };
    //                 Process.Start(psi);
    //             }
    //         }
    //         catch { /* ignorar */
    // }
    //     }
}
