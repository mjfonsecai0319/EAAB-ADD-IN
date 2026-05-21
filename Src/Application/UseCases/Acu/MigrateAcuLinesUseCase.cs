#nullable enable

namespace EAABAddIn.Src.Application.UseCases.Acu;

/// <summary>
/// Migrates Acueducto line features into the target geodatabase.

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
/// </summary>
public class MigrateAcuLinesUseCase
{
    /// <summary>
    /// Executes the ACU lines migration.
    /// </summary>
    /// <param name="featureclassPath">Source feature class path.</param>
    /// <param name="targetGDBPath">Target geodatabase folder path.</param>
    /// <returns>Tuple with success flag and a log message.</returns>
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
                        messages.AppendLine("Iniciando migración de líneas ACU...");
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

                    if (MigrateLineFeature(feature, targetGdb, targetName, subtype ?? 0, out var migrateError))
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

            messages.AppendLine($"Lineas ACU: {migrated} migradas de {total}");

            if (failed > 0)
            {
                messages.AppendLine($"  {failed} con errores");
            }

            try
            {
                var csv = new CsvReportService();
                var folder = csv.EnsureReportsFolder(targetPath);
                var listStats = stats.Select(kv => (kv.Key, kv.Value.attempts, kv.Value.migrated, kv.Value.failed));
                var file = csv.WriteMigrationSummary(folder, "acueducto_lineas", listStats, noClase, noTarget);
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
        1 => "acd_RedMatriz",
        2 => "acd_Conduccion",
        3 => "acd_Conduccion",
        4 => "acd_RedMenor",
        5 => "acd_LineaLateral",
        _ => string.Empty
    };

    private bool MigrateLineFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo, out string? error)
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

            var attributes = BuildLineAttributes(sourceFeature, subtipo);
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

    private Dictionary<string, object?> BuildLineAttributes(Feature source, int subtipo)
    {
        var attrs = new Dictionary<string, object?>(Shared.StringComparer)
        {
            ["SUBTIPO"] = subtipo,
            ["N_INICIAL"] = source.GetFieldValue<string>("N_INICIAL"),
            ["N_FINAL"] = source.GetFieldValue<string>("N_FINAL"),
            ["FECHAINSTALACION"] = source.GetFieldValue<DateTime?>("FECHAINST"),
            ["ESTADOENRED"] = source.GetFieldValue<string>("ESTADOENRED"),
            ["DOMDIAMETRONOMINAL"] = source.GetFieldValue<string>("DIAMETRO"),
            ["MATERIAL"] = source.GetFieldValue<string>("MATERIAL"),
            ["CALIDADDEDATO"] = source.GetFieldValue<string>("CALIDADDEDATO"),
            ["ESTADOLEGAL"] = source.GetFieldValue<string>("ESTADOLEGAL"),
            ["OBSERV"] = source.GetFieldValue<string>("OBSERV"),
            ["TIPOINSTALACION"] = source.GetFieldValue<string>("TIPOINSTALACION"),
            ["CONTRATO_ID"] = source.GetFieldValue<string>("CONTRATO_ID"),
            ["PROYECTO_ID"] = source.GetFieldValue<string>("NDISENO"),
            ["NOMBRE"] = source.GetFieldValue<string>("NOMBRE"),
            ["DOMCOSTADO"] = source.GetFieldValue<string>("COSTADO"),
            ["T_SECCION"] = source.GetFieldValue<string>("T_SECCION"),
            ["AREA_TR_M2"] = source.GetFieldValue<double?>("AREA_TR_M2"),
            ["C_RASANTEI"] = source.GetFieldValue<double?>("C_RASANTEI"),
            ["C_RASANTEF"] = source.GetFieldValue<double?>("C_RASANTEF"),
            ["C_CLAVEI"] = source.GetFieldValue<double?>("C_CLAVEI"),
            ["C_CLAVEF"] = source.GetFieldValue<double?>("C_CLAVEF"),
            ["PROFUNDIDAD"] = source.GetFieldValue<double?>("PROFUNDIDAD"),
            ["RUGOSIDAD"] = source.GetFieldValue<double?>("RUGOSIDAD"),
            ["LONGITUD_m"] = source.GetFieldValue<double?>("LONGITUD_m"),
            ["CODACTIVO_FIJO"] = source.GetFieldValue<string>("CODACTIVO_FIJO")
        };

        return attrs;
    }
}
