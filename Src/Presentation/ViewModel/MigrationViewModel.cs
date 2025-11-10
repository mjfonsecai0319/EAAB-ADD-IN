#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public ICommand ClearFormCommand { get; private set; }
    public ICommand RunCommand { get; private set; }

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
        ClearFormCommand = new RelayCommand(ClearForm);
        RunCommand = new AsyncRelayCommand(RunAsync);
    }

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

    private void ClearForm()
    {
        Workspace = null;
        XmlSchemaPath = null;
        L_Acu_Origen = null;
        P_Acu_Origen = null;
        L_Alc_Origen = null;
        P_Alc_Origen = null;
        L_Alc_Pluv_Origen = null;
        P_Alc_Pluv_Origen = null;
        MigrarConAdvertencias = false;
        StatusMessage = "Seleccione origen y destino y pulse Migrar.";
    }

    private async Task RunAsync()
    {
        IsBusy = true;
        StatusMessage = "Validando y migrando...";
        System.Diagnostics.Debug.WriteLine($"⚙ Estado inicial del checkbox: {MigrarConAdvertencias}");

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
            StatusMessage = "Validando estructura de los datos...";
            
            var datasetsToValidate = new List<DatasetInput>();
            
            if (!string.IsNullOrWhiteSpace(L_Acu_Origen))
                datasetsToValidate.Add(new DatasetInput("L_ACU_ORIGEN", L_Acu_Origen));
            if (!string.IsNullOrWhiteSpace(P_Acu_Origen))
                datasetsToValidate.Add(new DatasetInput("P_ACU_ORIGEN", P_Acu_Origen));
            if (!string.IsNullOrWhiteSpace(L_Alc_Origen))
                datasetsToValidate.Add(new DatasetInput("L_ALC_ORIGEN", L_Alc_Origen));
            if (!string.IsNullOrWhiteSpace(P_Alc_Origen))
                datasetsToValidate.Add(new DatasetInput("P_ALC_ORIGEN", P_Alc_Origen));
            if (!string.IsNullOrWhiteSpace(L_Alc_Pluv_Origen))
                datasetsToValidate.Add(new DatasetInput("L_ALC_PLUV_ORIGEN", L_Alc_Pluv_Origen));
            if (!string.IsNullOrWhiteSpace(P_Alc_Pluv_Origen))
                datasetsToValidate.Add(new DatasetInput("P_ALC_PLUV_ORIGEN", P_Alc_Pluv_Origen));

            if (datasetsToValidate.Count == 0)
            {
                StatusMessage = "Error: Debe seleccionar al menos un dataset de origen para migrar.";
                IsBusy = false;
                return;
            }

            var validation = await _datasetValidatorUseCase.Invoke(new()
            {
                OutputFolder = Workspace,
                Datasets = datasetsToValidate
            });

            int totalWarnings = validation.TotalWarnings;
            
            System.Diagnostics.Debug.WriteLine($"═══════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine($"📊 RESULTADO VALIDACIÓN:");
            System.Diagnostics.Debug.WriteLine($"   • Total advertencias detectadas: {totalWarnings}");
            System.Diagnostics.Debug.WriteLine($"   • Checkbox 'Migrar con advertencias': {MigrarConAdvertencias}");
            System.Diagnostics.Debug.WriteLine($"   • Datasets validados: {datasetsToValidate.Count}");
            System.Diagnostics.Debug.WriteLine($"   • Reportes generados: {validation.ReportFiles.Count}");
            foreach (var report in validation.ReportFiles)
            {
                System.Diagnostics.Debug.WriteLine($"      - {Path.GetFileName(report)}");
            }
            System.Diagnostics.Debug.WriteLine($"═══════════════════════════════════════════════════════");

            if (totalWarnings > 0 && !MigrarConAdvertencias)
            {
                StatusMessage = $"⚠ MIGRACIÓN BLOQUEADA: {totalWarnings} advertencia(s) detectada(s). Active el checkbox para continuar.";
                
                System.Diagnostics.Debug.WriteLine($"🚫 BLOQUEANDO MIGRACIÓN:");
                System.Diagnostics.Debug.WriteLine($"   ❌ Checkbox desmarcado con {totalWarnings} advertencias");
                System.Diagnostics.Debug.WriteLine($"   📋 Mostrando diálogo de bloqueo al usuario");
                
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: $"🚫 MIGRACIÓN BLOQUEADA\n\n" +
                                 $"Se detectaron {totalWarnings} advertencia(s) en la validación de los datos seleccionados.\n\n" +
                                 $"Datasets validados:\n" + string.Join("\n", datasetsToValidate.Select(d => $"  • {d.Name}")) + "\n\n" +
                                 $"📁 Revise los reportes de validación en:\n{validation.ReportFolder}\n\n" +
                                 $"Archivos generados:\n" + string.Join("\n", validation.ReportFiles.Select(f => $"  • {Path.GetFileName(f)}")) + "\n\n" +
                                 "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                 "✅ Para continuar con la migración:\n" +
                                 "   1. Revise los reportes CSV generados\n" +
                                 "   2. Active el checkbox ☑ 'Migrar con Advertencias'\n" +
                                 "   3. Presione el botón 'Ejecutar' nuevamente\n\n" +
                                 "⚠ IMPORTANTE: La migración NO se ejecutará hasta que\n" +
                                 "   active el checkbox y confirme que desea continuar.",
                    caption: $"⚠ {totalWarnings} Advertencia(s) Detectada(s)",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Warning
                );
                
                System.Diagnostics.Debug.WriteLine($"   ✓ Usuario cerró el diálogo - Migración cancelada");
                IsBusy = false;
                return;
            }
            
            if (totalWarnings > 0 && MigrarConAdvertencias)
            {
                StatusMessage = $"⚠ ADVERTENCIA: Continuando migración con {totalWarnings} problema(s) detectado(s) (checkbox activo).";
                System.Diagnostics.Debug.WriteLine($"⚠ MIGRACIÓN PERMITIDA CON ADVERTENCIAS:");
                System.Diagnostics.Debug.WriteLine($"   ✓ Checkbox marcado - Usuario autorizó continuar");
                System.Diagnostics.Debug.WriteLine($"   ⚠ Se procederá con {totalWarnings} advertencias");
            }
            else if (totalWarnings == 0)
            {
                StatusMessage = "✓ Validación exitosa sin advertencias. Iniciando migración...";
                System.Diagnostics.Debug.WriteLine($"✓ VALIDACIÓN EXITOSA - Sin advertencias detectadas");
            }

            StatusMessage = "Preparando GDB de destino...";
            var (okGdb, gdbPath, msgGdb) = await _createGdbFromXmlUseCase.Invoke(Workspace, XmlSchemaPath);

            if (!okGdb)
            {
                StatusMessage = $"Error preparando GDB de migración: {msgGdb}";
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: $"Error al preparar la GDB de destino: {msgGdb}",
                    caption: "Error",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Error
                );
                IsBusy = false;
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"📂 {msgGdb}");
            StatusMessage = msgGdb.Contains("Reutilizando") 
                ? "✓ GDB existente preparada. Iniciando migración de datos..." 
                : "✓ GDB nueva creada. Iniciando migración de datos...";

            var mensajesMigracion = new List<string>();
            bool acueductoMigrated = false;
            bool alcantarilladoMigrated = false;

            if (!string.IsNullOrWhiteSpace(L_Acu_Origen))
            {
                StatusMessage = "Migrando líneas de acueducto...";
                var (okLines, msgLines, warningsLines) = await _migrateAcueductoUseCase.MigrateLines(L_Acu_Origen, gdbPath);
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
                var (okPoints, msgPoints, warningsPoints) = await _migrateAcueductoUseCase.MigratePoints(P_Acu_Origen, gdbPath);
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

            if (acueductoMigrated)
            {
                StatusMessage = "Agregando capas de acueducto al mapa...";
                var (okAdd, msgAdd) = await _migrateAcueductoUseCase.AddMigratedLayersToMap(gdbPath);
                if (okAdd)
                {
                    mensajesMigracion.Add(msgAdd);
                }
            }


            if (!string.IsNullOrWhiteSpace(L_Alc_Origen))
            {
                StatusMessage = "Migrando líneas de alcantarillado...";
                var (okLines, msgLines, warningsLines) = await _migrateAlcantarilladoUseCase.MigrateLines(L_Alc_Origen, gdbPath);
                if (okLines)
                {
                    mensajesMigracion.Add(msgLines);
                    alcantarilladoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Líneas: {msgLines}");
                }
            }

            if (!string.IsNullOrWhiteSpace(P_Alc_Origen))
            {
                StatusMessage = "Migrando puntos de alcantarillado...";
                var (okPoints, msgPoints, warningsPoints) = await _migrateAlcantarilladoUseCase.MigratePoints(P_Alc_Origen, gdbPath);
                if (okPoints)
                {
                    mensajesMigracion.Add(msgPoints);
                    alcantarilladoMigrated = true;
                }
                else
                {
                    mensajesMigracion.Add($"⚠ Puntos: {msgPoints}");
                }
            }

            if (!string.IsNullOrWhiteSpace(L_Alc_Pluv_Origen))
            {
                StatusMessage = "Migrando líneas de alcantarillado pluvial...";
                var (okLinesPluv, msgLinesPluv, warningsLinesPluv) = await _migrateAlcantarilladoUseCase.MigrateLines(L_Alc_Pluv_Origen, gdbPath);
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
                var (okPointsPluv, msgPointsPluv, warningsPointsPluv) = await _migrateAlcantarilladoUseCase.MigratePoints(P_Alc_Pluv_Origen, gdbPath);
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

            if (alcantarilladoMigrated)
            {
                StatusMessage = "Agregando capas de alcantarillado al mapa...";
                var (okAdd, msgAdd) = await _migrateAlcantarilladoUseCase.AddMigratedLayersToMap(gdbPath);
                if (okAdd)
                {
                    mensajesMigracion.Add(msgAdd);
                }
            }

            var mensajeFinal = mensajesMigracion.Count > 0 
                ? string.Join("\n", mensajesMigracion) 
                : "No se especificaron datos de alcantarillado para migrar.";

            StatusMessage = "✓ Proceso finalizado.";
            
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                messageText: $"Migración completada:\n\n{mensajeFinal}",
                caption: "Migración Exitosa",
                button: System.Windows.MessageBoxButton.OK,
                icon: System.Windows.MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}
