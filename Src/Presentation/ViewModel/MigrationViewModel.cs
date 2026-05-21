#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;

using EAABAddIn.Src.Application.UseCases;
using EAABAddIn.Src.Application.UseCases.Acu;
using EAABAddIn.Src.Application.UseCases.Validation;
using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.ViewModel;

internal class MigrationViewModel : BusyViewModelBase
{
    public override string DisplayName => "Asistente de Migración";
    public override string Tooltip => "Iniciar proceso de migración de datos espaciales y tabulares";

    private readonly ValidateDatasetsUseCase _datasetValidatorUseCase = new ValidateDatasetsUseCase();
    private readonly CreateGdbFromXmlUseCase _createGdbFromXmlUseCase = new CreateGdbFromXmlUseCase();
    private readonly MigrateAlcantarilladoUseCase _migrateAlcantarilladoUseCase = new MigrateAlcantarilladoUseCase();
    private readonly MigrateAcuLinesUseCase _migrateAcuLinesUseCase = new MigrateAcuLinesUseCase();
    private readonly MigrateAcuPointsUseCase _migrateAcuPointsUseCase = new MigrateAcuPointsUseCase();
    private readonly AddLayersToMapViewUseCase _addLayersToMapViewUseCase = new AddLayersToMapViewUseCase();
    private readonly ZoomToLayersExtentUseCase _zoomToLayersExtentUseCase = new ZoomToLayersExtentUseCase();

    private bool _migrarConAdvertencias = false;
    private string? _workspace = null;
    private string? _xmlSchemaPath = null;
    private string? _lAcuOrigen = null;
    private string? _pAcuOrigen = null;
    private string? _lAlcOrigen = null;
    private string? _pAlcOrigen = null;
    private string? _lAlcPluvOrigen = null;
    private string? _pAlcPluvOrigen = null;

    public MigrationViewModel()
    {
        StatusMessage = "Esperando configuración. Por favor, seleccione las capas de origen y destino.";
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

    private void BrowseOutputFolder()
    {
        var filter = new BrowseProjectFilter("esri_browseDialogFilters_folders");
        var dlg = new OpenItemDialog
        {
            Title = "Seleccionar directorio de destino",
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
            Title = "Seleccionar archivo XML de esquema",
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
            Title = "Seleccionar clase de entidad de origen",
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
        StatusMessage = "Formulario restablecido. Listo para una nueva configuración.";
    }

    private async Task RunAsync()
    {
        IsBusy = true;
        StatusMessage = "Analizando la integridad estructural de los datos...";

        try
        {
            if (!TryPrepareRun(out var datasetsToValidate))
            {
                return;
            }

            var validation = await _datasetValidatorUseCase.Invoke(new()
            {
                OutputFolder = Workspace,
                Datasets = datasetsToValidate
            });

            if (!HandleValidationResult(validation.TotalWarnings, validation.ReportFolder))
            {
                return;
            }

            var gdbPath = await CreateTargetGdbAsync();
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                return;
            }

            var mensajesMigracion = new List<string>();

            await MigrateAcueductoAsync(gdbPath, mensajesMigracion);
            await MigrateAlcantarilladoAsync(gdbPath, mensajesMigracion);

            ShowMigrationSummary(mensajesMigracion);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Anomalía en el proceso: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private bool TryPrepareRun(out List<DatasetInput> datasetsToValidate)
    {
        datasetsToValidate = CollectDatasetsToValidate();

        if (Workspace is null)
        {
            StatusMessage = "Precaución: El directorio de destino es obligatorio.";
            return false;
        }

        if (XmlSchemaPath is null)
        {
            StatusMessage = "Precaución: Debe especificar un archivo XML de esquema.";
            return false;
        }

        if (datasetsToValidate.Count == 0)
        {
            StatusMessage = "Precaución: Seleccione al menos un conjunto de datos de origen.";
            return false;
        }

        return true;
    }

    private List<DatasetInput> CollectDatasetsToValidate()
    {
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

        return datasetsToValidate;
    }

    private bool HandleValidationResult(int totalWarnings, string reportFolder)
    {
        if (totalWarnings > 0 && !MigrarConAdvertencias)
        {
            StatusMessage = $"Proceso interrumpido: Se hallaron {totalWarnings} advertencia(s) en la validación estructural.";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                messageText: $"La fase de validación identificó {totalWarnings} advertencia(s).\n\n" +
                             $"Los reportes con el detalle completo han sido guardados en:\n{reportFolder}\n\n" +
                             $"Pasos recomendados:\n" +
                             $"  1. Revise los análisis CSV generados.\n" +
                             $"  2. Para continuar omitiendo este bloqueo, active 'Migrar con Advertencias'.\n" +
                             $"  3. Reintente presionar el botón de ejecución.",
                caption: "Criterios de Validación Insuficientes",
                button: System.Windows.MessageBoxButton.OK,
                icon: System.Windows.MessageBoxImage.Warning
            );

            return false;
        }

        if (totalWarnings > 0 && MigrarConAdvertencias)
        {
            StatusMessage = $"Continuando operación (Omitiendo {totalWarnings} advertencia(s) de integridad)...";
        }
        else
        {
            StatusMessage = "Verificación exitosa. Iniciando la arquitectura de migración...";
        }

        return true;
    }

    private async Task<string?> CreateTargetGdbAsync()
    {
        StatusMessage = "Aprovisionando la estructura de la Geodatabase objetivo...";
        var (okGdb, gdbPath, msgGdb) = await _createGdbFromXmlUseCase.Invoke(Workspace!, XmlSchemaPath!);

        if (!okGdb)
        {
            StatusMessage = $"Error de infraestructura: {msgGdb}";
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                messageText: $"Se produjo un fallo inesperado al inicializar la Geodatabase:\n{msgGdb}",
                caption: "Excepción en Geodatabase",
                button: System.Windows.MessageBoxButton.OK,
                icon: System.Windows.MessageBoxImage.Error
            );

            return null;
        }

        StatusMessage = "Geodatabase aprovisionada. Comenzando el traspaso de entidades...";
        return gdbPath;
    }

    private async Task MigrateAcueductoAsync(string gdbPath, List<string> messages)
    {
        if (!string.IsNullOrWhiteSpace(L_Acu_Origen))
        {
            StatusMessage = "Migrando red de acueducto (entidades de tipo línea)...";

            var (ok, msg) = await _migrateAcuLinesUseCase.Invoke(L_Acu_Origen, gdbPath);

            if (ok)
            {
                await AddLayersToMapview(
                    path: gdbPath,
                    layers: ["acd_RedMatriz", "acd_Conduccion", "acd_RedMenor", "acd_LineaLateral"],
                    zoom: false
                );
                messages.Add(msg);
            }
            else
            {
                messages.Add($"⚠ Acueducto Líneas: {msg}");
            }
        }

        if (!string.IsNullOrWhiteSpace(P_Acu_Origen))
        {
            StatusMessage = "Migrando red de acueducto (entidades de tipo punto)...";

            var (ok, msg) = await _migrateAcuPointsUseCase.Invoke(P_Acu_Origen, gdbPath);

            if (ok)
            {
                await AddLayersToMapview(
                    path: gdbPath,
                    layers: ["acd_Accesorio", "acd_ValvulaControl", "acd_ValvulaSistema", "acd_Hidrante", "acd_CamaraAcceso"]
                );
                messages.Add(msg);
            }
            else
            {
                messages.Add($"⚠ Acueducto Puntos: {msg}");
            }
        }
    }

    private async Task MigrateAlcantarilladoAsync(string gdbPath, List<string> mensajesMigracion)
    {
        bool alcantarilladoMigrated = false;

        if (!string.IsNullOrWhiteSpace(L_Alc_Origen))
        {
            StatusMessage = "Migrando alcantarillado sanitario (entidades de tipo línea)...";
            var (okLines, msgLines) = await _migrateAlcantarilladoUseCase.MigrateLines(L_Alc_Origen, gdbPath);
            if (okLines)
            {
                mensajesMigracion.Add(msgLines);
                alcantarilladoMigrated = true;
            }
            else
            {
                mensajesMigracion.Add($"⚠ Alcantarillado Líneas: {msgLines}");
            }
        }

        if (!string.IsNullOrWhiteSpace(P_Alc_Origen))
        {
            StatusMessage = "Migrando alcantarillado sanitario (entidades de tipo punto)...";
            var (okPoints, msgPoints) = await _migrateAlcantarilladoUseCase.MigratePoints(P_Alc_Origen, gdbPath);
            if (okPoints)
            {
                mensajesMigracion.Add(msgPoints);
                alcantarilladoMigrated = true;
            }
            else
            {
                mensajesMigracion.Add($"⚠ Alcantarillado Puntos: {msgPoints}");
            }
        }

        if (!string.IsNullOrWhiteSpace(L_Alc_Pluv_Origen))
        {
            StatusMessage = "Migrando alcantarillado pluvial (entidades de tipo línea)...";
            var (okLinesPluv, msgLinesPluv) = await _migrateAlcantarilladoUseCase.MigrateLines(L_Alc_Pluv_Origen, gdbPath);
            if (okLinesPluv)
            {
                mensajesMigracion.Add(msgLinesPluv);
                alcantarilladoMigrated = true;
            }
            else
            {
                mensajesMigracion.Add($"⚠ Pluvial Líneas: {msgLinesPluv}");
            }
        }

        if (!string.IsNullOrWhiteSpace(P_Alc_Pluv_Origen))
        {
            StatusMessage = "Migrando alcantarillado pluvial (entidades de tipo punto)...";
            var (okPointsPluv, msgPointsPluv) = await _migrateAlcantarilladoUseCase.MigratePoints(P_Alc_Pluv_Origen, gdbPath);
            if (okPointsPluv)
            {
                mensajesMigracion.Add(msgPointsPluv);
                alcantarilladoMigrated = true;
            }
            else
            {
                mensajesMigracion.Add($"⚠ Pluvial Puntos: {msgPointsPluv}");
            }
        }

        if (alcantarilladoMigrated)
        {
            StatusMessage = "Añadiendo resultados de alcantarillado al espacio de trabajo (mapa)...";
            var (okAdd, msgAdd) = await _migrateAlcantarilladoUseCase.AddMigratedLayersToMap(gdbPath);
            if (okAdd)
            {
                mensajesMigracion.Add(msgAdd);
            }
        }
    }

    private async Task AddLayersToMapview(string path, string[] layers, bool zoom = true)
    {
        bool success;

        try
        {
            success = await _addLayersToMapViewUseCase.Invoke(
                path: path,
                layers: layers
            );
        }
        catch (Exception)
        {
            success = false;
        }

        if (zoom && success)
        {
            await _zoomToLayersExtentUseCase.Invoke(layers);
        }
    }

    private void ShowMigrationSummary(List<string> mensajesMigracion)
    {
        var mensajeFinal = mensajesMigracion.Count > 0
            ? string.Join("\n", mensajesMigracion)
            : "La ejecución concluyó sin datos movilizados.";

        StatusMessage = "Transacción finalizada satisfactoriamente.";

        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
            messageText: $"La migración se completó satisfactoriamente.\n\nDetalles del proceso:\n{mensajeFinal}",
            caption: "Migración Exclusiva",
            button: System.Windows.MessageBoxButton.OK,
            icon: System.Windows.MessageBoxImage.Information
        );
    }

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
}
