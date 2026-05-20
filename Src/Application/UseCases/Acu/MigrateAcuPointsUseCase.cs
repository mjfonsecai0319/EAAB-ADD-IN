#nullable enable

namespace EAABAddIn.Src.Application.UseCases.Acu;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Application.Services;
using EAABAddIn.Src.Application.Utils;

/// <summary>
/// Migra entidades de puntos de acueducto a la geodatabase de destino.
/// </summary>
public class MigrateAcuPointsUseCase
{
    /// <summary>
    /// Ejecuta la migracion de puntos ACU.
    /// </summary>
    /// <param name="featureclassPath">Ruta de la clase de entidad de origen.</param>
    /// <param name="targetGDBPath">Ruta de la geodatabase de destino.</param>
    /// <returns>Tupla con el estado y el mensaje de resultado.</returns>
    public Task<(bool success, string message)> Invoke(string featureclassPath, string targetGDBPath)
    {
        return QueuedTask.Run(() => Internal(featureclassPath, targetGDBPath));
    }

    private (bool success, string message) Internal(string sourcePath, string targetPath)
    {
        int migrated = 0, total = 0, noClase = 0, noTarget = 0, failed = 0;
        var map = MapView.Active?.Map;
        var messages = new StringBuilder();
        var stats = new Dictionary<string, (int attempts, int migrated, int failed)>(Shared.StringComparer);
        var ensuredLayers = new HashSet<string>(Shared.StringComparer);

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetPath))
        {
            return (false, "Parámetros inválidos");
        }

        if (!Directory.Exists(targetPath))
        {
            return (false, "El directorio de destino no existe");
        }

        try
        {
            using var sourceFC = FeatureClassUtils.TryOpenFeatureClass(sourcePath);
            using var targetGdb = GeodatabaseUtils.OpenGeodatabase(targetPath);

            if (sourceFC is null || targetGdb is null)
            {
                return (false, "No se pudo abrir la clase de entidad de origen");
            }

            using var cursor = sourceFC.Search();

            while (cursor.MoveNext())
            {
                total++;
                if (cursor.Current is not Feature feature)
                {
                    continue;
                }

                using (feature)
                {
                    var @class = feature.GetFieldValue<int?>("CLASE");
                    var subtype = feature.GetFieldValue<int?>("SUBTIPO");
                    string targetName = string.Empty;

                    if (total == 1)
                    {
                        messages.AppendLine("Iniciando migración de puntos ACU...");
                    }

                    if (!@class.HasValue || @class.Value == 0)
                    {
                        noClase++;
                        continue;
                    }

                    targetName = GetTargetClassName(@class.Value);

                    if (string.IsNullOrEmpty(targetName))
                    {
                        noTarget++;
                        continue;
                    }

                    if (!Shared.FeatureClassExists(targetGdb, targetName))
                    {
                        noTarget++;
                        continue;
                    }

                    if (map != null && ensuredLayers.Add(targetName))
                    {
                        Shared.EnsureLayerForTargetClass(map, targetGdb, targetName, isLine: false);
                    }

                    if (MigratePointFeature(feature, targetGdb, targetName, subtype ?? 0, out var migrateError))
                    {
                        migrated++;
                    }
                    else
                    {
                        failed++;
                        if (!string.IsNullOrWhiteSpace(migrateError))
                        {
                            messages.AppendLine($"   Error: {migrateError}");
                        }
                    }

                    if (!string.IsNullOrEmpty(targetName))
                    {
                        if (!stats.TryGetValue(targetName, out var current))
                        {
                            current = (0, 0, 0);
                        }

                        current.attempts++;
                        if (string.IsNullOrWhiteSpace(migrateError))
                        {
                            current.migrated++;
                        }
                        else
                        {
                            current.failed++;
                        }

                        stats[targetName] = current;
                    }
                }
            }

            messages.AppendLine($"Puntos ACU: {migrated} migrados de {total}");

            if (failed > 0)
            {
                messages.AppendLine($"  {failed} con errores");
            }

            try
            {
                var csv = new CsvReportService();
                var folder = csv.EnsureReportsFolder(targetPath);
                var listStats = stats.Select(kv => (kv.Key, kv.Value.attempts, kv.Value.migrated, kv.Value.failed));
                var file = csv.WriteMigrationSummary(folder, "acueducto_puntos", listStats, noClase, noTarget);
                messages.AppendLine($"  Reporte: {Path.GetFileName(file)}");
            }
            catch (Exception exCsv)
            {
                messages.AppendLine($"  Error CSV: {exCsv.Message}");
            }
        }
        catch (Exception ex)
        {
            messages.AppendLine($"Error: {ex.Message}");
        }

        return (true, messages.ToString());
    }

    private string GetTargetClassName(int classname) => classname switch
    {
        1 => "acd_ValvulaSistema",
        2 => "acd_ValvulaControl",
        3 => "acd_Accesorio",
        4 => "acd_Accesorio",
        5 => "acd_Accesorio",
        6 => "acd_Accesorio",
        7 => "acd_Accesorio",
        8 => "acd_Accesorio",
        9 => "acd_Hidrante",
        10 => "acd_MacroMedidor",
        11 => "acd_PuntoAcometida",
        12 => "acd_PilaMuestreo",
        13 => "acd_Captacion",
        14 => "acd_Desarenador",
        15 => "acd_PlantaTratamiento",
        16 => "acd_EstacionBombeo",
        17 => "acd_Tanque",
        18 => "acd_Portal",
        19 => "acd_CamaraAcceso",
        20 => "acd_ValvulaControl",
        21 => "acd_CamaraAcceso",
        _ => string.Empty
    };

    private bool MigratePointFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo, out string? error)
    {
        error = null;
        try
        {
            using var targetFC = FeatureClassUtils.TryOpenFromGeodatabase(targetGdb, targetClassName);
            if (targetFC is null)
            {
                error = $"No se encontró la clase de destino: {targetClassName}";
                return false;
            }

            var geometry = sourceFeature.GetShape();
            if (geometry is null || geometry.IsEmpty)
            {
                error = "Geometría nula o vacía en origen";
                return false;
            }

            using var featureClassDef = targetFC.GetDefinition();

            var attributes = BuildPointAttributes(sourceFeature, subtipo);
            var dict = new Dictionary<string, object?>(Shared.StringComparer);
            string shapeField = featureClassDef.GetShapeField();
            dict[shapeField] = geometry;

            var fieldMap = featureClassDef.GetFields().ToDictionary(f => f.Name, f => f, Shared.StringComparer);

            foreach (var attr in attributes)
            {
                if (!fieldMap.TryGetValue(attr.Key, out var fieldDef))
                {
                    continue;
                }

                dict[attr.Key] = Shared.CoerceToFieldType(attr.Value, fieldDef);
            }

            var (insertOk, insertErr) = Shared.TryInsertRowDirect(targetGdb, targetFC, dict);
            if (!insertOk)
            {
                error = insertErr;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private Dictionary<string, object?> BuildPointAttributes(Feature source, int subtipo)
    {
        var attrs = new Dictionary<string, object?>(Shared.StringComparer)
        {
            ["SUBTIPO"] = subtipo,
            ["IDENTIFIC"] = source.GetFieldValue<string>("IDENTIFIC"),
            ["NORTE"] = source.GetFieldValue<double?>("NORTE"),
            ["ESTE"] = source.GetFieldValue<double?>("ESTE"),
            ["FECHAINST"] = source.GetFieldValue<DateTime?>("FECHAINST"),
            ["ESTADOENRED"] = source.GetFieldValue<string>("ESTADOENRE"),
            ["LOCALIZACIONRELATIVA"] = source.GetFieldValue<string>("LOCALIZACI"),
            ["CALIDADDATO"] = source.GetFieldValue<string>("CALIDADDAT"),
            ["ROTACION"] = source.GetFieldValue<double?>("ROTACION"),
            ["C_RASANTE"] = source.GetFieldValue<double?>("C_RASANTE"),
            ["PROFUN"] = source.GetFieldValue<double?>("PROFUN"),
            ["MATERIAL"] = source.GetFieldValue<string>("MATERIAL"),
            ["VINCULO"] = source.GetFieldValue<string>("VINCULO"),
            ["OBSERVACIONES"] = source.GetFieldValue<string>("OBSERVACIO"),
            ["CONTRATO_ID"] = source.GetFieldValue<string>("CONTRATO_I"),
            ["NDISENO"] = source.GetFieldValue<string>("NDISENO"),
            ["TIPOESPPUB"] = source.GetFieldValue<string>("TIPOESPPUB"),
            ["MATESPPUBL"] = source.GetFieldValue<string>("MATESPPUBL"),
            ["AUTOMATIZA"] = source.GetFieldValue<int?>("AUTOMATIZA"),
            ["DIAMETRO1"] = source.GetFieldValue<string>("DIAMETRO1"),
            ["DIAMETRO2"] = source.GetFieldValue<string>("DIAMETRO2"),
            ["SENTIDOOPERAC"] = source.GetFieldValue<string>("SENTIDOOPE"),
            ["ESTADOOPERAC"] = source.GetFieldValue<string>("ESTADOOPER"),
            ["TIPOOPERAC"] = source.GetFieldValue<string>("TIPOOPERAC"),
            ["ESTADOFIS_VAL"] = source.GetFieldValue<string>("ESTADOFIS_"),
            ["TIPOVALVUL"] = source.GetFieldValue<string>("TIPOVALVUL"),
            ["VUELTASCIE"] = source.GetFieldValue<double?>("VUELTASCIE"),
            ["CLASEACCES"] = source.GetFieldValue<string>("CLASEACCES"),
            ["ESTADOFISICOH"] = source.GetFieldValue<string>("ESTADOFISI"),
            ["MARCA"] = source.GetFieldValue<string>("MARCA"),
            ["FUNCIONPIL"] = source.GetFieldValue<int?>("FUNCIONPIL"),
            ["ESTADOMED"] = source.GetFieldValue<string>("ESTADOMED"),
            ["SECTORENTR"] = source.GetFieldValue<string>("SECTORENTR"),
            ["SECTORSALI"] = source.GetFieldValue<string>("SECTORSALI"),
            ["IDTUBERIAMEDIDA"] = source.GetFieldValue<string>("IDTUBERIAM"),
            ["CAUDAL_PROMEDIO"] = source.GetFieldValue<double?>("CAUDAL_PRO"),
            ["TIPO_M"] = source.GetFieldValue<string>("TIPO_M"),
            ["FECHA_TOMA_C"] = source.GetFieldValue<DateTime?>("FECHA_TOMA"),
            ["UBICACCAJI"] = source.GetFieldValue<string>("UBICACCAJI"),
            ["CENTRO"] = source.GetFieldValue<string>("CENTRO"),
            ["L_ALM"] = source.GetFieldValue<double?>("L_ALM"),
            ["AREARESP"] = source.GetFieldValue<double?>("AREARESP"),
            ["TIPO_MUESTR"] = source.GetFieldValue<string>("TIPO_MUEST"),
            ["FUENTEABAS"] = source.GetFieldValue<string>("FUENTEABAS"),
            ["UBICAC_MUES"] = source.GetFieldValue<string>("UBICAC_MUE"),
            ["PTOANALISI"] = source.GetFieldValue<string>("PTOANALISI"),
            ["LOCPUNTO"] = source.GetFieldValue<string>("LOCPUNTO"),
            ["ESTADO"] = source.GetFieldValue<string>("ESTADO"),
            ["FECHAESTADO"] = source.GetFieldValue<DateTime?>("FECHAESTAD"),
            ["CLASEPUNTO"] = source.GetFieldValue<string>("CLASEPUNTO"),
            ["NROFILTROS"] = source.GetFieldValue<int?>("NROFILTROS"),
            ["NROSEDIMEN"] = source.GetFieldValue<int?>("NROSEDIMEN"),
            ["NROCOMPART"] = source.GetFieldValue<int?>("NROCOMPART"),
            ["NROMEZCLAR"] = source.GetFieldValue<int?>("NROMEZCLAR"),
            ["NROFLOCULA"] = source.GetFieldValue<int?>("NROFLOCULA"),
            ["CAPACINSTA"] = source.GetFieldValue<double?>("CAPACINSTA"),
            ["NROBOMBAS"] = source.GetFieldValue<int?>("NROBOMBAS"),
            ["CAPABOMBEO"] = source.GetFieldValue<double?>("CAPABOMBEO"),
            ["COTABOMBEO"] = source.GetFieldValue<double?>("COTABOMBEO"),
            ["ALTURADINA"] = source.GetFieldValue<double?>("ALTURADINA"),
            ["COTAFONDO"] = source.GetFieldValue<double?>("COTAFONDO"),
            ["COTAREBOSE"] = source.GetFieldValue<double?>("COTAREBOSE"),
            ["CAPACIDAD"] = source.GetFieldValue<double?>("CAPACIDAD"),
            ["NIVELMAXIM"] = source.GetFieldValue<double?>("NIVELMAXIM"),
            ["NIVELMINIM"] = source.GetFieldValue<double?>("NIVELMINIM"),
            ["AREATRANSV"] = source.GetFieldValue<double?>("AREATRANSV"),
            ["TIENEVIGIL"] = source.GetFieldValue<int?>("TIENEVIGIL"),
            ["OPERACTANQ"] = source.GetFieldValue<string>("OPERACTANQ"),
            ["TIPOACCESO"] = source.GetFieldValue<string>("TIPOACCESO"),
            ["DIAMETROAC"] = source.GetFieldValue<string>("DIAMETROAC"),
            ["NOMBRE"] = source.GetFieldValue<string>("NOMBRE"),
            ["DIRECCION"] = source.GetFieldValue<string>("DIRECCION"),
            ["PRESION"] = source.GetFieldValue<double?>("PRESION"),
            ["CODACTIVO_FIJO"] = source.GetFieldValue<string>("CODACTIVO_")
        };

        return attrs;
    }
}
