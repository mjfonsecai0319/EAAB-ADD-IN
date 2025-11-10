#nullable enable

using System;
using System.Collections.Generic;
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
    private readonly MigrateAlcantarilladoUseCase _migrateAlcantarilladoUseCase = new MigrateAlcantarilladoUseCase();
    private readonly MigrateAcueductoUseCase _migrateAcueductoUseCase = new MigrateAcueductoUseCase();

    public ICommand WorkspaceCommand { get; private set; }
    public ICommand XmlSchemaCommand { get; private set; }
    public ICommand BrowseLAcuOrigenCommand { get; private set; }
    public ICommand BrowsePAcuOrigenCommand { get; private set; }
    public ICommand BrowseLAlcOrigenCommand { get; private set; }
    public ICommand BrowsePAlcOrigenCommand { get; private set; }
    public ICommand BrowseLAlcPluvOrigenCommand { get; private set; }
    public ICommand BrowsePAlcPluvOrigenCommand { get; private set; }
    public ICommand RunCommand { get; private set; }
    public ICommand ClearFormCommand { get; private set; }
    public ICommand OpenReportsFolderCommand { get; private set; }

    public MigrationViewModel()
    {
        StatusMessage = "Seleccione origen y destino y pulse Migrar.";
        WorkspaceCommand = new RelayCommand(BrowseOutputFolder);
        XmlSchemaCommand = new RelayCommand(BrowseXmlSchema);
        BrowseLAcuOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Acu_Origen = path));
        BrowsePAcuOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Acu_Origen = path));
        BrowseLAlcOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Alc_Origen = path));
        BrowsePAlcOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Alc_Origen = path));
        BrowseLAlcPluvOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => L_Alc_Pluv_Origen = path));
        BrowsePAlcPluvOrigenCommand = new RelayCommand(() => BrowseFeatureClass(path => P_Alc_Pluv_Origen = path));
        RunCommand = new AsyncRelayCommand(RunAsync, CanRun);
        ClearFormCommand = new RelayCommand(ClearForm);
        OpenReportsFolderCommand = new RelayCommand(OpenReportsFolder);
    }

    // ✅ FIX 1: Checkbox notifica cambios correctamente
    private bool _migrarConAdvertencias = false;
    public bool MigrarConAdvertencias
    {
        get => _migrarConAdvertencias;
        set
        {
            if (_migrarConAdvertencias != value)
            {
                _migrarConAdvertencias = value;
                NotifyPropertyChanged(nameof(MigrarConAdvertencias));
                // 🔥 CRÍTICO: Actualizar estado de comandos cuando cambia el checkbox
                RaiseCommandsCanExecute();
            }
        }
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

            // ✅ FIX 2: Actualizar estado ANTES de verificar advertencias
            TotalWarnings = validation.TotalWarnings;
            ReportsFolder = validation.ReportFolder ?? string.Empty;
            ReportFiles = new ObservableCollection<string>(validation.ReportFiles ?? new List<string>());
            HasWarnings = TotalWarnings > 0;
            
            // 🔥 CRÍTICO: Notificar cambios DESPUÉS de actualizar HasWarnings
            RaiseCommandsCanExecute();

            // Gating estilo Python
            if (HasWarnings && !MigrarConAdvertencias)
            {
                StatusMessage = $"Hay {TotalWarnings} advertencias. Activa 'Migrar con advertencias' para continuar.";
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: $"Se detectaron {TotalWarnings} advertencias. Revisa los reportes y activa 'Migrar con advertencias' si deseas continuar.\n\nCarpeta de reportes:\n{ReportsFolder}",
                    caption: "Advertencias detectadas",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Warning
                );
                IsBusy = false;
                return;
            }
            else if (HasWarnings && MigrarConAdvertencias)
            {
                var res = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: $"Continuar a pesar de {TotalWarnings} advertencias?\n\nPuedes abrir los reportes en: {ReportsFolder}",
                    caption: "Confirmar migración con advertencias",
                    button: System.Windows.MessageBoxButton.YesNo,
                    icon: System.Windows.MessageBoxImage.Question
                );
                if (res != System.Windows.MessageBoxResult.Yes)
                {
                    IsBusy = false;
                    return;
                }
            }

            // Crear GDB
            var (okGdb, gdbPath, msgGdb) = await _createGdbFromXmlUseCase.Invoke(Workspace, XmlSchemaPath);

            if (!okGdb)
            {
                StatusMessage = $"Error creando GDB: {msgGdb}";
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: $"Error creando GDB desde XML: {msgGdb}",
                    caption: "Error",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Error
                );
                IsBusy = false;
                return;
            }

            StatusMessage = "GDB creada. Iniciando migración...";

            var mensajesMigracion = new List<string>();
            bool acueductoMigrated = false;
            bool alcantarilladoMigrated = false;

            // ========== ACUEDUCTO ==========
            if (!string.IsNullOrWhiteSpace(L_Acu_Origen))
            {
                StatusMessage = "Migrando líneas de acueducto...";
                var (okLines, msgLines) = await _migrateAcueductoUseCase.MigrateLines(L_Acu_Origen, gdbPath);
                if (okLines)
                {
                    mensajesMigracion.Add(msgLines);
                    acueductoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Líneas ACU: {msgLines}");
                }
            }

            if (!string.IsNullOrWhiteSpace(P_Acu_Origen))
            {
                StatusMessage = "Migrando puntos de acueducto...";
                var (okPoints, msgPoints) = await _migrateAcueductoUseCase.MigratePoints(P_Acu_Origen, gdbPath);
                if (okPoints)
                {
                    mensajesMigracion.Add(msgPoints);
                    acueductoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Puntos ACU: {msgPoints}");
                }
            }

            // ✅ FIX 3: Agregar capas de acueducto SOLO si se migró algo
            if (acueductoMigrated)
            {
                StatusMessage = "Agregando capas de acueducto al mapa...";
                var (okAdd, msgAdd) = await _migrateAcueductoUseCase.AddMigratedLayersToMap(gdbPath);
                if (okAdd)
                {
                    mensajesMigracion.Add("✓ Capas de acueducto agregadas al mapa");
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Error agregando capas ACU: {msgAdd}");
                }
            }

            // ========== ALCANTARILLADO ==========
            if (!string.IsNullOrWhiteSpace(L_Alc_Origen))
            {
                StatusMessage = "Migrando líneas de alcantarillado...";
                var (okLines, msgLines) = await _migrateAlcantarilladoUseCase.MigrateLines(L_Alc_Origen, gdbPath);
                if (okLines)
                {
                    mensajesMigracion.Add(msgLines);
                    alcantarilladoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Líneas ALC: {msgLines}");
                }
            }

            if (!string.IsNullOrWhiteSpace(P_Alc_Origen))
            {
                StatusMessage = "Migrando puntos de alcantarillado...";
                var (okPoints, msgPoints) = await _migrateAlcantarilladoUseCase.MigratePoints(P_Alc_Origen, gdbPath);
                if (okPoints)
                {
                    mensajesMigracion.Add(msgPoints);
                    alcantarilladoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Puntos ALC: {msgPoints}");
                }
            }

            if (!string.IsNullOrWhiteSpace(L_Alc_Pluv_Origen))
            {
                StatusMessage = "Migrando líneas de alcantarillado pluvial...";
                var (okLinesPluv, msgLinesPluv) = await _migrateAlcantarilladoUseCase.MigrateLines(L_Alc_Pluv_Origen, gdbPath);
                if (okLinesPluv)
                {
                    mensajesMigracion.Add(msgLinesPluv);
                    alcantarilladoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Líneas pluvial: {msgLinesPluv}");
                }
            }

            if (!string.IsNullOrWhiteSpace(P_Alc_Pluv_Origen))
            {
                StatusMessage = "Migrando puntos de alcantarillado pluvial...";
                var (okPointsPluv, msgPointsPluv) = await _migrateAlcantarilladoUseCase.MigratePoints(P_Alc_Pluv_Origen, gdbPath);
                if (okPointsPluv)
                {
                    mensajesMigracion.Add(msgPointsPluv);
                    alcantarilladoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Puntos pluvial: {msgPointsPluv}");
                }
            }

            // 🔥 FIX 4: Agregar capas de alcantarillado INDEPENDIENTEMENTE de acueducto
            if (alcantarilladoMigrated)
            {
                StatusMessage = "Agregando capas de alcantarillado al mapa...";
                var (okAdd, msgAdd) = await _migrateAlcantarilladoUseCase.AddMigratedLayersToMap(gdbPath);
                if (okAdd)
                {
                    mensajesMigracion.Add("✓ Capas de alcantarillado agregadas al mapa");
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Error agregando capas ALC: {msgAdd}");
                }
            }

            // Mensaje final
            var mensajeFinal = mensajesMigracion.Count > 0
                ? string.Join("\n\n", mensajesMigracion)
                : "No se especificaron datos para migrar.";

            StatusMessage = "✓ Proceso finalizado.";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                messageText: $"Migración completada:\n\n{mensajeFinal}\n\nGDB: {gdbPath}",
                caption: "Migración Exitosa",
                button: System.Windows.MessageBoxButton.OK,
                icon: System.Windows.MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                messageText: $"Error durante la migración:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                caption: "Error",
                button: System.Windows.MessageBoxButton.OK,
                icon: System.Windows.MessageBoxImage.Error
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearForm()
    {
        if (IsBusy) return;
        Workspace = null;
        XmlSchemaPath = null;
        L_Acu_Origen = null;
        P_Acu_Origen = null;
        L_Alc_Origen = null;
        P_Alc_Origen = null;
        L_Alc_Pluv_Origen = null;
        P_Alc_Pluv_Origen = null;
        MigrarConAdvertencias = false;
        HasWarnings = false;
        TotalWarnings = 0;
        ReportsFolder = string.Empty;
        ReportFiles.Clear();
        StatusMessage = "Formulario limpio. Seleccione origen y destino.";
        RaiseCommandsCanExecute();
    }

    // Reportes / Advertencias
    public int TotalWarnings
    {
        get => _totalWarnings;
        private set
        {
            if (_totalWarnings != value)
            {
                _totalWarnings = value;
                NotifyPropertyChanged(nameof(TotalWarnings));
            }
        }
    }
    private int _totalWarnings = 0;

    public bool HasWarnings
    {
        get => _hasWarnings;
        private set
        {
            if (_hasWarnings != value)
            {
                _hasWarnings = value;
                NotifyPropertyChanged(nameof(HasWarnings));
            }
        }
    }
    private bool _hasWarnings = false;

    public string ReportsFolder
    {
        get => _reportsFolder;
        private set
        {
            if (_reportsFolder != value)
            {
                _reportsFolder = value;
                NotifyPropertyChanged(nameof(ReportsFolder));
            }
        }
    }
    private string _reportsFolder = string.Empty;

    public ObservableCollection<string> ReportFiles { get; private set; } = new();

    private void OpenReportsFolder()
    {
        try
        {
            var folder = string.IsNullOrWhiteSpace(ReportsFolder) ? Workspace : ReportsFolder;
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

    private bool CanRun()
    {
        // ✅ FIX 5: Lógica correcta para habilitar/deshabilitar botón
        if (IsBusy) return false;
        
        // Si hay advertencias y el checkbox NO está marcado, deshabilitar
        if (HasWarnings && !MigrarConAdvertencias)
        {
            return false;
        }
        
        return true;
    }

    private void RaiseCommandsCanExecute()
    {
        (RunCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ClearFormCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenReportsFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}