#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;

namespace EAABAddIn.Src.Application.UseCases
{
    public class MigrateAlcantarilladoUseCase
    {
        public async Task<(bool ok, string message)> MigrateLines(string sourceLineasPath, string targetGdbPath, bool addLayersToMap = true, bool zoomToData = true)
        {
            var result = await QueuedTask.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                var perClassStats = new Dictionary<string, (int attempts, int migrated, int failed)>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (string.IsNullOrWhiteSpace(sourceLineasPath) || string.IsNullOrWhiteSpace(targetGdbPath))
                        return (false, "Parámetros inválidos");

                    if (!Directory.Exists(targetGdbPath))
                        return (false, "La GDB de destino no existe");

                    using var sourceFC = OpenFeatureClass(sourceLineasPath);
                    if (sourceFC == null)
                        return (false, $"No se pudo abrir: {sourceLineasPath}");

                    using var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath)));

                    var map = MapView.Active?.Map;
                    SpatialReference? mapSpatialReference = null;
                    if (map != null)
                    {
                        try
                        {
                            mapSpatialReference = map.SpatialReference;
                            System.Diagnostics.Debug.WriteLine($"🌍 SR del Mapa (Líneas ALC): WKID={mapSpatialReference?.Wkid}");
                        }
                        catch (Exception exSR)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ Error obteniendo SR del mapa: {exSR.Message}");
                        }
                    }

                    var ensuredLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    int migrated = 0, total = 0, noClase = 0, noTarget = 0, failed = 0;

                    using var cursor = sourceFC.Search();
                    bool editingDisabledDetected = false;
                    while (cursor.MoveNext())
                    {
                        total++;
                        using var feature = cursor.Current as Feature;
                        if (feature == null) continue;

                        var clase = GetFieldValue<int?>(feature, "CLASE");
                        var subtipo = GetFieldValue<int?>(feature, "SUBTIPO");
                        var tipoSistema = GetFieldValue<int?>(feature, "SISTEMA");

                        if (total == 1)
                        {
                            log.AppendLine($"📋 Primera feature (línea ALC): CLASE={clase}, SUBTIPO={subtipo}, SISTEMA={tipoSistema}");
                            log.AppendLine($"   Campos: {string.Join(", ", GetFieldNames(feature).Take(10))}...");
                        }

                        if (!clase.HasValue || clase.Value == 0)
                        {
                            noClase++;
                            continue;
                        }

                        string targetClassName = GetTargetLineClassName(clase.Value, tipoSistema?.ToString());
                        if (string.IsNullOrEmpty(targetClassName))
                        {
                            noTarget++;
                            if (total <= 5)
                                System.Diagnostics.Debug.WriteLine($"⚠ Feature {total}: CLASE={clase}, SISTEMA={tipoSistema} -> Sin clase destino");
                            continue;
                        }

                        if (!FeatureClassExists(targetGdb, targetClassName))
                        {
                            noTarget++;
                            if (total <= 5)
                                System.Diagnostics.Debug.WriteLine($"⚠ Clase destino no existe: {targetClassName}");
                            continue;
                        }

                        if (map != null && !ensuredLayers.Contains(targetClassName))
                        {
                            EnsureLayerForTargetClass(map, targetGdb, targetClassName, isLine: true);
                            ensuredLayers.Add(targetClassName);
                        }

                        if (total <= 5)
                            log.AppendLine($"→ Feature {total}: {targetClassName}");

                        if (MigrateLineFeature(feature, targetGdb, targetClassName, subtipo ?? 0, out var migrateErr, mapSpatialReference))
                            migrated++;
                        else
                        {
                            failed++;
                            if (!string.IsNullOrWhiteSpace(migrateErr))
                            {
                                if (!editingDisabledDetected)
                                {
                                    log.AppendLine($"   ✖ Error: {migrateErr}");
                                }
                                if (migrateErr?.IndexOf("Editing in the application is not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    editingDisabledDetected = true;
                                    log.AppendLine("   👉 Habilita edición en ArcGIS Pro: Proyecto > Opciones > Edición");
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            if (!perClassStats.TryGetValue(targetClassName, out var stats))
                                stats = (0, 0, 0);
                            stats.attempts++;
                            if (string.IsNullOrWhiteSpace(migrateErr))
                                stats.migrated++;
                            else
                                stats.failed++;
                            perClassStats[targetClassName] = stats;
                        }
                    }

                    log.AppendLine($"\n📊 Resumen líneas ALC:");
                    log.AppendLine($"   Total: {total}");
                    log.AppendLine($"   Migradas: {migrated}");
                    log.AppendLine($"   Sin CLASE: {noClase}");
                    log.AppendLine($"   Sin clase destino: {noTarget}");
                    log.AppendLine($"   Fallos: {failed}");

                    // Escribir CSV resumen
                    try
                    {
                        var csv = new Services.CsvReportService();
                        var folder = csv.EnsureReportsFolder(targetGdbPath);
                        var listStats = perClassStats.Select(kv => (kv.Key, kv.Value.attempts, kv.Value.migrated, kv.Value.failed));
                        var file = csv.WriteMigrationSummary(folder, "alcantarillado_lineas", listStats, noClase, noTarget);
                        log.AppendLine($"   📁 CSV: {file}");
                    }
                    catch (Exception exCsv)
                    {
                        log.AppendLine($"   ⚠ No se pudo escribir CSV: {exCsv.Message}");
                    }

                    return (true, log.ToString());
                }
                catch (Exception ex)
                {
                    log.AppendLine($"\n❌ Error: {ex.Message}");
                    return (false, log.ToString());
                }
            });

            if (result.Item1 && addLayersToMap)
            {
                var (ok2, msg2) = await AddMigratedLayersToMap(targetGdbPath, zoomToData);
                var merged = result.Item2 + "\n\n" + msg2;
                return (ok2, merged);
            }

            return result;
        }

        public async Task<(bool ok, string message)> MigratePoints(string sourcePuntosPath, string targetGdbPath, bool addLayersToMap = true, bool zoomToData = true)
        {
            var result = await QueuedTask.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                var perClassStats = new Dictionary<string, (int attempts, int migrated, int failed)>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (string.IsNullOrWhiteSpace(sourcePuntosPath) || string.IsNullOrWhiteSpace(targetGdbPath))
                        return (false, "Parámetros inválidos");

                    if (!Directory.Exists(targetGdbPath))
                        return (false, "La GDB de destino no existe");

                    using var sourceFC = OpenFeatureClass(sourcePuntosPath);
                    if (sourceFC == null)
                        return (false, $"No se pudo abrir: {sourcePuntosPath}");

                    using var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath)));

                    var map = MapView.Active?.Map;
                    SpatialReference? mapSpatialReference = null;
                    if (map != null)
                    {
                        try
                        {
                            mapSpatialReference = map.SpatialReference;
                            System.Diagnostics.Debug.WriteLine($"🌍 SR del Mapa (Puntos ALC): WKID={mapSpatialReference?.Wkid}");
                        }
                        catch (Exception exSR)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ Error obteniendo SR del mapa: {exSR.Message}");
                        }
                    }

                    var ensuredLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    int migrated = 0, total = 0, noClase = 0, noTarget = 0, failed = 0;

                    using var cursor = sourceFC.Search();
                    bool editingDisabledDetected = false;
                    while (cursor.MoveNext())
                    {
                        total++;
                        using var feature = cursor.Current as Feature;
                        if (feature == null) continue;

                        var clase = GetFieldValue<int?>(feature, "CLASE");
                        var subtipo = GetFieldValue<int?>(feature, "SUBTIPO");
                        var tipoSistema = GetFieldValue<int?>(feature, "SISTEMA");

                        if (total == 1)
                        {
                            log.AppendLine($"📋 Primera feature (punto ALC): CLASE={clase}, SUBTIPO={subtipo}, SISTEMA={tipoSistema}");
                            log.AppendLine($"   Campos: {string.Join(", ", GetFieldNames(feature).Take(10))}...");
                        }

                        if (!clase.HasValue || clase.Value == 0)
                        {
                            noClase++;
                            continue;
                        }

                        string targetClassName = GetTargetPointClassName(clase.Value, tipoSistema?.ToString());
                        if (string.IsNullOrEmpty(targetClassName))
                        {
                            noTarget++;
                            if (total <= 5)
                                System.Diagnostics.Debug.WriteLine($"⚠ Feature {total}: CLASE={clase}, SISTEMA={tipoSistema} -> Sin clase destino");
                            continue;
                        }

                        if (!FeatureClassExists(targetGdb, targetClassName))
                        {
                            noTarget++;
                            if (total <= 5)
                                System.Diagnostics.Debug.WriteLine($"⚠ Clase destino no existe: {targetClassName}");
                            continue;
                        }

                        if (map != null && !ensuredLayers.Contains(targetClassName))
                        {
                            EnsureLayerForTargetClass(map, targetGdb, targetClassName, isLine: false);
                            ensuredLayers.Add(targetClassName);
                        }

                        if (total <= 5)
                            log.AppendLine($"→ Feature {total}: {targetClassName}");

                        if (MigratePointFeature(feature, targetGdb, targetClassName, subtipo ?? 0, out var migrateErr, mapSpatialReference))
                            migrated++;
                        else
                        {
                            failed++;
                            if (!string.IsNullOrWhiteSpace(migrateErr))
                            {
                                if (!editingDisabledDetected)
                                {
                                    log.AppendLine($"   ✖ Error: {migrateErr}");
                                }
                                if (migrateErr?.IndexOf("Editing in the application is not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    editingDisabledDetected = true;
                                    log.AppendLine("   👉 Habilita edición en ArcGIS Pro");
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            if (!perClassStats.TryGetValue(targetClassName, out var stats))
                                stats = (0, 0, 0);
                            stats.attempts++;
                            if (string.IsNullOrWhiteSpace(migrateErr))
                                stats.migrated++;
                            else
                                stats.failed++;
                            perClassStats[targetClassName] = stats;
                        }
                    }

                    log.AppendLine($"\n📊 Resumen puntos ALC:");
                    log.AppendLine($"   Total: {total}");
                    log.AppendLine($"   Migradas: {migrated}");
                    log.AppendLine($"   Sin CLASE: {noClase}");
                    log.AppendLine($"   Sin destino: {noTarget}");
                    log.AppendLine($"   Fallos: {failed}");

                    try
                    {
                        var csv = new Services.CsvReportService();
                        var folder = csv.EnsureReportsFolder(targetGdbPath);
                        var listStats = perClassStats.Select(kv => (kv.Key, kv.Value.attempts, kv.Value.migrated, kv.Value.failed));
                        var file = csv.WriteMigrationSummary(folder, "alcantarillado_puntos", listStats, noClase, noTarget);
                        log.AppendLine($"   📁 CSV: {file}");
                    }
                    catch (Exception exCsv)
                    {
                        log.AppendLine($"   ⚠ No se pudo escribir CSV: {exCsv.Message}");
                    }

                    return (true, log.ToString());
                }
                catch (Exception ex)
                {
                    log.AppendLine($"\n❌ Error: {ex.Message}");
                    return (false, log.ToString());
                }
            });

            if (result.Item1 && addLayersToMap)
            {
                var (ok2, msg2) = await AddMigratedLayersToMap(targetGdbPath, zoomToData);
                var merged = result.Item2 + "\n\n" + msg2;
                return (ok2, merged);
            }

            return result;
        }

        #region Helpers

        private FeatureClass? OpenFeatureClass(string path)
        {
            try
            {
                path = Path.GetFullPath(path);

                if (path.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = Path.GetDirectoryName(path);
                    if (string.IsNullOrEmpty(folder))
                        return null;
                    var name = Path.GetFileNameWithoutExtension(path);
                    var conn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                    var datastore = new FileSystemDatastore(conn);
                    return datastore.OpenDataset<FeatureClass>(name);
                }

                var gdbIndex = path.LastIndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                if (gdbIndex >= 0)
                {
                    var gdbPath = path.Substring(0, gdbIndex + 4);
                    var fcName = path.Length > gdbIndex + 4
                        ? path.Substring(gdbIndex + 5).TrimStart('\\', '/')
                        : string.Empty;

                    if (string.IsNullOrEmpty(fcName))
                        return null;

                    var conn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
                    var gdb = new Geodatabase(conn);

                    try
                    {
                        var fc = gdb.OpenDataset<FeatureClass>(fcName);
                        if (fc != null) return fc;
                    }
                    catch { }

                    var parts = fcName.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    var last = parts.Length > 0 ? parts[parts.Length - 1] : fcName;

                    if (parts.Length >= 2)
                    {
                        var fdName = parts[0];
                        try
                        {
                            var fd = gdb.OpenDataset<FeatureDataset>(fdName);
                            var fcFromFd = fd.OpenDataset<FeatureClass>(last);
                            if (fcFromFd != null) return fcFromFd;
                        }
                        catch { }
                    }

                    try
                    {
                        var fdDefs = gdb.GetDefinitions<FeatureDatasetDefinition>();
                        foreach (var fdDef in fdDefs)
                        {
                            try
                            {
                                var fd = gdb.OpenDataset<FeatureDataset>(fdDef.GetName());
                                var fcFromAnyFd = fd.OpenDataset<FeatureClass>(last);
                                if (fcFromAnyFd != null) return fcFromAnyFd;
                            }
                            catch { }
                        }
                    }
                    catch { }

                    return null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private List<string> GetFieldNames(Feature feature)
        {
            var names = new List<string>();
            try
            {
                var def = feature.GetTable().GetDefinition();
                foreach (var f in def.GetFields())
                    names.Add(f.Name);
            }
            catch { }
            return names;
        }

        private T? GetFieldValue<T>(Feature feature, string field)
        {
            try
            {
                var idx = feature.FindField(field);
                if (idx < 0)
                    return default;
                var val = feature[idx];
                if (val == null || val is DBNull)
                    return default;

                if (typeof(T) == typeof(string))
                {
                    var s = val?.ToString() ?? string.Empty;
                    return (T)(object)s;
                }

                if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    var underlyingType = Nullable.GetUnderlyingType(typeof(T))!;
                    return (T)Convert.ChangeType(val, underlyingType);
                }
                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        private string GetTargetLineClassName(int clase, string? tipoSistema)
        {
            string prefix = tipoSistema switch
            {
                "1" => "alp_",
                "0" or "2" or null or "" => "als_",
                _ => "als_"
            };
            return clase switch
            {
                1 => prefix + "RedLocal",
                2 => prefix + "RedTroncal",
                3 => prefix + "LineaLateral",
                _ => string.Empty
            };
        }

        private string GetTargetPointClassName(int clase, string? tipoSistema)
        {
            string prefix = tipoSistema switch
            {
                "1" => "alp_",
                "0" or "2" or null or "" => "als_",
                _ => "als_"
            };
            return clase switch
            {
                1 => prefix + "EstructuraRed",
                2 => prefix + "Pozo",
                3 => prefix + "Sumidero",
                4 => prefix + "CajaDomiciliaria",
                5 => prefix + "SeccionTransversal",
                _ => string.Empty
            };
        }

        private bool MigrateLineFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo, out string? error, SpatialReference? mapSpatialReference = null)
        {
            error = null;
            try
            {
                using var targetFC = OpenTargetFeatureClass(targetGdb, targetClassName);
                if (targetFC == null)
                {
                    error = $"No se encontró: {targetClassName}";
                    return false;
                }

                var geometry = sourceFeature.GetShape();
                if (geometry == null || geometry.IsEmpty)
                {
                    error = "Geometría vacía";
                    return false;
                }

                using var featureClassDef = targetFC.GetDefinition();

                var attributes = BuildLineAttributes(sourceFeature, featureClassDef, subtipo);
                var dict = new Dictionary<string, object?>();
                string shapeField = featureClassDef.GetShapeField();
                dict[shapeField] = geometry;

                var fieldMap = featureClassDef.GetFields().ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
                foreach (var attr in attributes)
                {
                    if (!fieldMap.TryGetValue(attr.Key, out var fieldDef))
                        continue;
                    dict[attr.Key] = CoerceToFieldType(attr.Value, fieldDef);
                }

                var (insertOk, insertErr) = TryInsertRowDirect(targetGdb, targetFC, dict, mapSpatialReference);
                if (insertOk)
                    return true;

                error = insertErr;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool MigratePointFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo, out string? error, SpatialReference? mapSpatialReference = null)
        {
            error = null;
            try
            {
                using var targetFC = OpenTargetFeatureClass(targetGdb, targetClassName);
                if (targetFC == null)
                {
                    error = $"No se encontró: {targetClassName}";
                    return false;
                }

                var geometry = sourceFeature.GetShape();
                if (geometry == null || geometry.IsEmpty)
                {
                    error = "Geometría vacía";
                    return false;
                }

                using var featureClassDef = targetFC.GetDefinition();

                var attributes = BuildPointAttributes(sourceFeature, featureClassDef, subtipo);
                var dict = new Dictionary<string, object?>();
                string shapeField = featureClassDef.GetShapeField();
                dict[shapeField] = geometry;

                var fieldMap = featureClassDef.GetFields().ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
                foreach (var attr in attributes)
                {
                    if (!fieldMap.TryGetValue(attr.Key, out var fieldDef))
                        continue;
                    dict[attr.Key] = CoerceToFieldType(attr.Value, fieldDef);
                }

                var (insertOk, insertErr) = TryInsertRowDirect(targetGdb, targetFC, dict, mapSpatialReference);
                if (insertOk)
                    return true;

                error = insertErr;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private (bool ok, string? error) TryInsertRowDirect(Geodatabase targetGdb, FeatureClass targetFC, Dictionary<string, object?> dict, SpatialReference? mapSpatialReference = null)
        {
            try
            {
                using var def = targetFC.GetDefinition();
                var fieldMap = def.GetFields().ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
                var targetSR = def.GetSpatialReference();
                string shapeField = def.GetShapeField();

                // 1) Validar geometría presente
                if (!dict.TryGetValue(shapeField, out var geomVal) || geomVal == null)
                    return (false, "Geometría nula");

                if (geomVal is not Geometry sourceGeom || sourceGeom.IsEmpty)
                    return (false, "Geometría vacía");

                // 2) Reproyectar si es necesario
                try
                {
                    var sourceSR = sourceGeom.SpatialReference;
                    if (targetSR != null && sourceSR != null && sourceSR.Wkid != targetSR.Wkid)
                    {
                        var projectedGeom = GeometryEngine.Instance.Project(sourceGeom, targetSR);
                        sourceGeom = projectedGeom;
                        dict[shapeField] = projectedGeom;
                    }
                }
                catch (Exception exProj)
                {
                    System.Diagnostics.Debug.WriteLine($"[ALC] Error proyectando: {exProj.Message}");
                }

                // 3) Ajuste Z/M si el destino no soporta
                try
                {
                    bool targetHasZ = def.HasZ();
                    bool targetHasM = def.HasM();
                    if ((sourceGeom.HasZ && !targetHasZ) || (sourceGeom.HasM && !targetHasM))
                    {
                        Geometry adjusted = sourceGeom;
                        if (sourceGeom is MapPoint mp)
                        {
                            adjusted = MapPointBuilderEx.CreateMapPoint(mp.X, mp.Y, mp.SpatialReference);
                        }
                        else if (sourceGeom is Polyline line)
                        {
                            var builder = new PolylineBuilderEx(sourceGeom.SpatialReference);
                            foreach (var part in line.Parts)
                            {
                                var pts = new List<MapPoint>();
                                MapPoint? lastEnd = null;
                                foreach (var seg in part)
                                {
                                    var sp = seg.StartPoint;
                                    pts.Add(MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, sourceGeom.SpatialReference));
                                    lastEnd = seg.EndPoint;
                                }
                                if (lastEnd != null)
                                    pts.Add(MapPointBuilderEx.CreateMapPoint(lastEnd.X, lastEnd.Y, sourceGeom.SpatialReference));
                                if (pts.Count > 0)
                                    builder.AddPart(pts);
                            }
                            adjusted = builder.ToGeometry();
                        }
                        else if (sourceGeom is Polygon poly)
                        {
                            var builder = new PolygonBuilderEx(sourceGeom.SpatialReference);
                            foreach (var part in poly.Parts)
                            {
                                var pts = new List<MapPoint>();
                                MapPoint? lastEnd = null;
                                foreach (var seg in part)
                                {
                                    var sp = seg.StartPoint;
                                    pts.Add(MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, sourceGeom.SpatialReference));
                                    lastEnd = seg.EndPoint;
                                }
                                if (pts.Count > 0)
                                    builder.AddPart(pts);
                            }
                            adjusted = builder.ToGeometry();
                        }
                        dict[shapeField] = adjusted;
                    }
                }
                catch (Exception exZM)
                {
                    return (false, $"Error ajustando Z/M: {exZM.Message}");
                }

                // 4) Inserción directa
                try
                {
                    using var rowBuffer = targetFC.CreateRowBuffer();
                    foreach (var kv in dict)
                    {
                        if (!fieldMap.TryGetValue(kv.Key, out var fd)) continue;
                        if (fd.FieldType == FieldType.OID || fd.FieldType == FieldType.GlobalID) continue;
                        rowBuffer[kv.Key] = kv.Value ?? DBNull.Value;
                    }
                    using var row = targetFC.CreateRow(rowBuffer);
                    return (true, null);
                }
                catch (Exception directEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ALC] Inserción directa falló: {directEx.Message} -> fallback a EditOperation");
                }

                // 5) Fallback EditOperation (solo si directa falla)
                var editOp = new EditOperation { Name = "Insertar feature ALC (fallback)", SelectNewFeatures = false };
                editOp.Callback(context =>
                {
                    using var rowBuffer2 = targetFC.CreateRowBuffer();
                    foreach (var kv in dict)
                    {
                        if (!fieldMap.TryGetValue(kv.Key, out var fd)) continue;
                        if (fd.FieldType == FieldType.OID || fd.FieldType == FieldType.GlobalID) continue;
                        rowBuffer2[kv.Key] = kv.Value ?? DBNull.Value;
                    }
                    using var row2 = targetFC.CreateRow(rowBuffer2);
                    context.Invalidate(row2);
                }, targetFC);

                if (!editOp.Execute())
                {
                    var msg = string.IsNullOrWhiteSpace(editOp.ErrorMessage) ? "Falló EditOperation" : editOp.ErrorMessage;
                    return (false, msg);
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private Dictionary<string, object?> BuildLineAttributes(Feature source, FeatureClassDefinition def, int subtipo)
        {
            var a = new Dictionary<string, object?>();
            try
            {
                a["SUBTIPO"] = subtipo;
                var sistema = GetFieldValue<int?>(source, "SISTEMA");
                a["DOMTIPOSISTEMA"] = sistema?.ToString();

                var ubicTec = GetFieldValue<string>(source, "UBICACIONTECNICA")
                              ?? GetFieldValue<string>(source, "UBICACION_TECNICA")
                              ?? GetFieldValue<string>(source, "UBIC_TECNICA")
                              ?? GetFieldValue<string>(source, "UBIC_TECN");
                if (!string.IsNullOrWhiteSpace(ubicTec))
                {
                    a["UBICACIONTECNICA"] = ubicTec;
                    a["UBICACION_TECNICA"] = ubicTec;
                }

                a["ZONA"] = GetFieldValue<string>(source, "ZONA");
                a["UGA"] = GetFieldValue<string>(source, "UGA") ?? GetFieldValue<string>(source, "UGA_ID");
                a["IDSIG"] = GetFieldValue<string>(source, "IDSIG") ?? GetFieldValue<string>(source, "ID_SIG");
                a["FECHAINSTALACION"] = GetFieldValue<DateTime?>(source, "FECHAINST");
                a["LONGITUD_M"] = GetFieldValue<double?>(source, "LONGITUD_M");
                a["DOMMATERIAL"] = GetFieldValue<string>(source, "MATERIAL");
                a["DOMMATERIAL2"] = GetFieldValue<string>(source, "MATERIAL2");
                a["DOMDIAMETRONOMINAL"] = GetFieldValue<string>(source, "DIAMETRO");
                a["DOMESTADOENRED"] = GetFieldValue<string>(source, "ESTADOENRED") ?? GetFieldValue<string>(source, "ESTADOENRE");
                a["DOMCALIDADDATO"] = GetFieldValue<string>(source, "CALIDADDATO") ?? GetFieldValue<string>(source, "CALIDADDAT");
                a["DOMESTADOLEGAL"] = GetFieldValue<string>(source, "ESTADOLEGAL") ?? GetFieldValue<string>(source, "ESTADOLEGA");
                a["DOMTIPOSECCION"] = GetFieldValue<string>(source, "T_SECCION");
                a["DOMCAMARACAIDA"] = GetFieldValue<string>(source, "CAM_CAIDA");
                a["DOMMETODOINSTALACION"] = GetFieldValue<string>(source, "INSTALACI");
                a["DOMMATERIALESPPUBLICO"] = GetFieldValue<string>(source, "MATESPPUBL");
                a["DOMTIPOINSPECCION"] = GetFieldValue<string>(source, "TIPOINSPEC");
                a["DOMGRADOESTRUCTURAL"] = GetFieldValue<string>(source, "GRADOEST");
                a["DOMGRADOOPERACIONAL"] = GetFieldValue<string>(source, "GRADOOPER");
                a["RUGOSIDAD"] = GetFieldValue<double?>(source, "RUGOSIDAD");
                a["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERV") ?? GetFieldValue<string>(source, "OBSERVACIO");
                a["PENDIENTE"] = GetFieldValue<double?>(source, "PENDIENTE");
                a["PROFUNDIDADMEDIA"] = GetFieldValue<double?>(source, "PROFUNDIDA");
                a["NUMEROCONDUCTOS"] = GetFieldValue<int?>(source, "NROCONDUCT");
                a["BASE"] = GetFieldValue<double?>(source, "BASE");
                a["ALTURA1"] = GetFieldValue<double?>(source, "ALTURA1");
                a["ALTURA2"] = GetFieldValue<double?>(source, "ALTURA2");
                a["TALUD1"] = GetFieldValue<double?>(source, "TALUD1");
                a["TALUD2"] = GetFieldValue<double?>(source, "TALUD2");
                a["ANCHOBERMA"] = GetFieldValue<double?>(source, "ANCHOBERMA");
                a["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
                a["COTARASANTEINICIAL"] = GetFieldValue<double?>(source, "C_RASATEI");
                a["COTARASANTEFINAL"] = GetFieldValue<double?>(source, "C_RASANTEF");
                a["COTACLAVEINICIAL"] = GetFieldValue<double?>(source, "C_CLAVEI");
                a["COTACLAVEFINAL"] = GetFieldValue<double?>(source, "C_CLAVEF");
                a["COTABATEAINICIAL"] = GetFieldValue<double?>(source, "C_BATEAI");
                a["COTABATEAFINAL"] = GetFieldValue<double?>(source, "C_BATEAF");
                a["N_INICIAL"] = GetFieldValue<string>(source, "N_INICIAL");
                a["N_FINAL"] = GetFieldValue<string>(source, "N_FINAL");
                a["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_I");
                a["DISENO_ID"] = GetFieldValue<string>(source, "NDISENO");
                a["CODACTIVOS_FIJOS"] = GetFieldValue<string>(source, "CODACTIVOS") ?? GetFieldValue<string>(source, "CODACTIVO_");
            }
            catch { }
            return a;
        }

        private Dictionary<string, object?> BuildPointAttributes(Feature source, FeatureClassDefinition def, int subtipo)
        {
            var a = new Dictionary<string, object?>();
            try
            {
                a["SUBTIPO"] = subtipo;
                var sistema = GetFieldValue<int?>(source, "SISTEMA");
                a["DOMTIPOSISTEMA"] = sistema?.ToString();

                var ubicTec = GetFieldValue<string>(source, "UBICACIONTECNICA")
                              ?? GetFieldValue<string>(source, "UBICACION_TECNICA")
                              ?? GetFieldValue<string>(source, "UBIC_TECNICA")
                              ?? GetFieldValue<string>(source, "UBIC_TECN");
                if (!string.IsNullOrWhiteSpace(ubicTec))
                {
                    a["UBICACIONTECNICA"] = ubicTec;
                    a["UBICACION_TECNICA"] = ubicTec;
                }

                a["ZONA"] = GetFieldValue<string>(source, "ZONA");
                a["UGA"] = GetFieldValue<string>(source, "UGA") ?? GetFieldValue<string>(source, "UGA_ID");
                a["IDSIG"] = GetFieldValue<string>(source, "IDSIG") ?? GetFieldValue<string>(source, "ID_SIG");
                a["FECHAINSTALACION"] = GetFieldValue<DateTime?>(source, "FECHADATO") ?? GetFieldValue<DateTime?>(source, "FECHAINST");
                a["DISENO_ID"] = GetFieldValue<string>(source, "NDISENO");
                a["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_ID") ?? GetFieldValue<string>(source, "CONTRATO_I");
                a["DOMESTADOENRED"] = GetFieldValue<string>(source, "ESTADOENRED") ?? GetFieldValue<string>(source, "ESTADOENRE");
                a["DOMCALIDADDATO"] = GetFieldValue<string>(source, "CALIDADDATO") ?? GetFieldValue<string>(source, "CALIDADDAT");
                a["DOMMATERIAL"] = GetFieldValue<string>(source, "MATERIAL");
                a["LOCALIZACIONRELATIVA"] = GetFieldValue<string>(source, "LOCALIZACI");
                a["ROTACIONSIMBOLO"] = GetFieldValue<double?>(source, "ROTACION");
                a["DIRECCION"] = GetFieldValue<string>(source, "DIRECCION");
                a["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
                a["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERV");
                a["COTARASANTE"] = GetFieldValue<double?>(source, "C_RASANTE");
                a["COTATERRENO"] = GetFieldValue<double?>(source, "C_TERRENO");
                a["COTAFONDO"] = GetFieldValue<double?>(source, "C_FONDO");
                a["PROFUNDIDAD"] = GetFieldValue<double?>(source, "PROFUNDIDA");
                a["COTACRESTA"] = GetFieldValue<double?>(source, "COTACRESTA");
                a["COTATECHOVERTEDERO"] = GetFieldValue<double?>(source, "C_TECHO_VE");
                a["ALTURABOMBEO"] = GetFieldValue<double?>(source, "HBOMBEO");
                a["COTABOMBEO"] = GetFieldValue<double?>(source, "COTABOMBE");
                a["VOLUMENBOMBEO"] = GetFieldValue<double?>(source, "VOLBOMBEO");
                a["CAUDALBOMBEO"] = GetFieldValue<double?>(source, "Q_BOMBEO");
                a["LONGVERTEDERO"] = GetFieldValue<double?>(source, "LONGVERT");
                a["LARGOESTRUCTURA"] = GetFieldValue<double?>(source, "LARGO");
                a["ANCHOESTRUCTURA"] = GetFieldValue<double?>(source, "ANCHO");
                a["ALTOESTRUCTURA"] = GetFieldValue<double?>(source, "ALTO");
                a["UNIDADESBOMBEO"] = GetFieldValue<string>(source, "UNIDBOMBEO");
                a["DOMTIPOBOMBEO"] = GetFieldValue<string>(source, "TIPOBOMB");
                a["DOMINICIALVARIASCUENCAS"] = GetFieldValue<string>(source, "INICIAL_CU");
                a["DOMCAMARASIFON"] = GetFieldValue<string>(source, "CAMARASIF");
                a["DOMESTADOPOZO"] = GetFieldValue<string>(source, "EST_POZO");
                a["DOMESTADOOPERACION"] = GetFieldValue<string>(source, "ESTOPERA");
                a["DOMTIPOALMACENAMIENTO"] = GetFieldValue<string>(source, "TIPOALMAC");
                a["DOMESTADOFISICO"] = GetFieldValue<string>(source, "EST_FISICO");
                a["DOMTIPOVALVULAANTIRREFLUJO"] = GetFieldValue<string>(source, "TIPO_VALV_");
                a["DOMTIPOALIVIO"] = GetFieldValue<string>(source, "TIPO_ALIVI");
                a["DOMTIENECABEZAL"] = GetFieldValue<string>(source, "CABEZAL");
                a["DOMESTADOTAPA"] = GetFieldValue<string>(source, "EST_TAPA");
                a["DOMMATERIALESCALONES"] = GetFieldValue<string>(source, "MATESCALO");
                a["DOMESTADOESCALON"] = GetFieldValue<string>(source, "ESTESCALON");
                a["DOMESTADOCARGUE"] = GetFieldValue<string>(source, "ESTCARGUE");
                a["DOMESTADOCILINDRO"] = GetFieldValue<string>(source, "ESTCILIND");
                a["DOMESTADOCANUELA"] = GetFieldValue<string>(source, "ESTCANUE");
                a["CONTINSPE"] = GetFieldValue<string>(source, "CONTINSPE");
                a["FECHA_INSP"] = GetFieldValue<DateTime?>(source, "FECHA_INSP");
                a["DOMTIPOINSPECCION"] = GetFieldValue<string>(source, "TIPOINSPEC");
                a["DOMCONOREDUCCION"] = GetFieldValue<string>(source, "CONOREDUCC");
                a["DOMMATERCONO"] = GetFieldValue<string>(source, "MATERCONO");
                a["DOMTIPOCONO"] = GetFieldValue<string>(source, "TIPO_CONO");
                a["DOMESTADOCONO"] = GetFieldValue<string>(source, "EST_CONO");
                a["ESTREJILLA"] = GetFieldValue<string>(source, "ESTREJILLA");
                a["MATREJILLA"] = GetFieldValue<string>(source, "MATREJILLA");
                a["TAMREJILLA"] = GetFieldValue<string>(source, "TAMREJILLA");
                a["DOMORIGENSECCION"] = GetFieldValue<string>(source, "ORIGENSEC");
                a["DISTANCIADESDEORIGEN"] = GetFieldValue<double?>(source, "DISTORIGEN");
                a["ABSCISA"] = GetFieldValue<string>(source, "ABSCISA");
                a["IDENTIFIC"] = GetFieldValue<string>(source, "IDENTIFIC");
                a["NORTE"] = GetFieldValue<double?>(source, "NORTE");
                a["ESTE"] = GetFieldValue<double?>(source, "ESTE");
                a["CODACTIVO_FIJO"] = GetFieldValue<string>(source, "CODACTIVO_F") ?? GetFieldValue<string>(source, "CODACTIVO_");
            }
            catch { }
            return a;
        }

        private object? CoerceToFieldType(object? value, Field fieldDef)
        {
            if (value == null || value is DBNull)
                return DBNull.Value;

            try
            {
                switch (fieldDef.FieldType)
                {
                    case FieldType.String:
                        {
                            var s = value.ToString() ?? string.Empty;
                            s = s.Trim();
                            try
                            {
                                int maxLen = fieldDef.Length;
                                if (maxLen > 0 && s.Length > maxLen)
                                    s = s.Substring(0, maxLen);
                            }
                            catch { }
                            return s;
                        }
                    case FieldType.Integer:
                        return Convert.ChangeType(value, typeof(int));
                    case FieldType.SmallInteger:
                        return Convert.ChangeType(value, typeof(short));
                    case FieldType.Double:
                        return Convert.ChangeType(value, typeof(double));
                    case FieldType.Single:
                        return Convert.ChangeType(value, typeof(float));
                    case FieldType.Date:
                        return Convert.ChangeType(value, typeof(DateTime));
                    case FieldType.GUID:
                    case FieldType.GlobalID:
                        return value.ToString();
                    case FieldType.OID:
                        return DBNull.Value;
                    default:
                        return value;
                }
            }
            catch
            {
                return DBNull.Value;
            }
        }

        private bool FeatureClassExists(Geodatabase gdb, string className)
        {
            try
            {
                using var fc = OpenTargetFeatureClass(gdb, className);
                return fc != null;
            }
            catch
            {
                return false;
            }
        }

        private FeatureClass? OpenTargetFeatureClass(Geodatabase gdb, string className)
        {
            try
            {
                return gdb.OpenDataset<FeatureClass>(className);
            }
            catch { }

            try
            {
                var fdDefs = gdb.GetDefinitions<FeatureDatasetDefinition>();
                foreach (var fdDef in fdDefs)
                {
                    try
                    {
                        using var fd = gdb.OpenDataset<FeatureDataset>(fdDef.GetName());
                        var fc = fd.OpenDataset<FeatureClass>(className);
                        if (fc != null)
                            return fc;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 🔥 MÉTODO PRINCIPAL: Agrega capas de alcantarillado al mapa (INDEPENDIENTE de acueducto)
        /// </summary>
        public async Task<(bool ok, string message)> AddMigratedLayersToMap(string targetGdbPath, bool zoomToData = true)
        {
            return await QueuedTask.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        return (false, "No hay un mapa activo. Abre un mapa primero.");
                    }

                    log.AppendLine($"🗺️ Mapa activo: {map.Name}");
                    try
                    {
                        var mapSR = map.SpatialReference;
                        if (mapSR != null)
                            log.AppendLine($"   SR del mapa: WKID={mapSR.Wkid}, Name={mapSR.Name}");
                    }
                    catch { }

                    if (!Directory.Exists(targetGdbPath))
                    {
                        return (false, $"La GDB no existe: {targetGdbPath}");
                    }

                    using var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath)));

                    log.AppendLine("📂 Agregando capas de alcantarillado...");

                    var layersAdded = new List<string>();
                    Envelope? combinedExtent = null;

                    // Clases de líneas
                    var lineClasses = new[] { "als_RedLocal", "als_RedTroncal", "als_LineaLateral", "alp_RedLocal", "alp_RedTroncal", "alp_LineaLateral" };
                    foreach (var className in lineClasses)
                    {
                        var (added, extent) = AddFeatureLayerToMap(map, targetGdb, className, isLine: true);
                        if (added)
                        {
                            layersAdded.Add(className);
                            if (extent != null)
                                combinedExtent = combinedExtent == null ? extent : combinedExtent.Union(extent);
                        }
                    }

                    // Clases de puntos
                    var pointClasses = new[] { "als_EstructuraRed", "als_Pozo", "als_Sumidero", "als_CajaDomiciliaria", "als_SeccionTransversal",
                                              "alp_EstructuraRed", "alp_Pozo", "alp_Sumidero", "alp_CajaDomiciliaria", "alp_SeccionTransversal" };
                    foreach (var className in pointClasses)
                    {
                        var (added, extent) = AddFeatureLayerToMap(map, targetGdb, className, isLine: false);
                        if (added)
                        {
                            layersAdded.Add(className);
                            if (extent != null)
                                combinedExtent = combinedExtent == null ? extent : combinedExtent.Union(extent);
                        }
                    }

                    if (layersAdded.Count > 0)
                    {
                        log.AppendLine($"✓ Capas agregadas: {string.Join(", ", layersAdded)}");

                        // Hacer zoom
                        if (zoomToData && combinedExtent != null && MapView.Active != null)
                        {
                            if (!double.IsNaN(combinedExtent.XMin) && !double.IsNaN(combinedExtent.YMin))
                            {
                                var width = combinedExtent.Width;
                                var height = combinedExtent.Height;
                                var expandedExtent = new EnvelopeBuilderEx(
                                    combinedExtent.XMin - width * 0.1,
                                    combinedExtent.YMin - height * 0.1,
                                    combinedExtent.XMax + width * 0.1,
                                    combinedExtent.YMax + height * 0.1,
                                    combinedExtent.SpatialReference
                                ).ToGeometry();

                                MapView.Active.ZoomTo(expandedExtent, TimeSpan.FromSeconds(1.5));
                                MapView.Active.Redraw(true);
                                log.AppendLine($"✓ Zoom aplicado");
                            }
                        }

                        return (true, log.ToString());
                    }
                    else
                    {
                        log.AppendLine("⚠ No se agregó ninguna capa (todas vacías o no existen)");
                        return (false, log.ToString());
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine($"❌ Error: {ex.Message}");
                    return (false, log.ToString());
                }
            });
        }

        private (bool added, Envelope? extent) AddFeatureLayerToMap(Map map, Geodatabase gdb, string className, bool isLine)
        {
            try
            {
                using var fc = OpenTargetFeatureClass(gdb, className);
                if (fc == null)
                    return (false, null);

                var count = fc.GetCount();
                if (count == 0)
                    return (false, null);

                // Calcular extent
                Envelope? calculatedExtent = null;
                try
                {
                    using var cursor = fc.Search();
                    int geomCount = 0;
                    while (cursor.MoveNext() && geomCount < 100)
                    {
                        using var feature = cursor.Current as Feature;
                        if (feature != null)
                        {
                            var geom = feature.GetShape();
                            if (geom != null && !geom.IsEmpty)
                            {
                                var geomExtent = geom.Extent;
                                if (geomExtent != null && !double.IsNaN(geomExtent.XMin))
                                {
                                    calculatedExtent = calculatedExtent == null
                                        ? geomExtent
                                        : calculatedExtent.Union(geomExtent);
                                    geomCount++;
                                }
                            }
                        }
                    }
                }
                catch { }

                // Verificar si ya existe
                var existingLayer = map.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .FirstOrDefault(l => l.Name.Equals(className, StringComparison.OrdinalIgnoreCase));

                if (existingLayer != null)
                {
                    bool sameDatasource = false;
                    try
                    {
                        using var layerFc = existingLayer.GetFeatureClass();
                        var layerGdb = layerFc?.GetDatastore() as Geodatabase;
                        var layerGdbPath = layerGdb?.GetPath().LocalPath ?? string.Empty;
                        var targetGdbPath = gdb.GetPath().LocalPath;
                        sameDatasource = !string.IsNullOrEmpty(layerGdbPath) &&
                                        layerGdbPath.Equals(targetGdbPath, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { }

                    if (sameDatasource)
                    {
                        ApplySymbology(existingLayer, isLine);
                        EnsureLayerIsVisibleAndSelectable(existingLayer, fc);
                        return (true, calculatedExtent);
                    }
                    else
                    {
                        try { map.RemoveLayer(existingLayer); } catch { }
                    }
                }

                // Crear capa
                var flParams = new FeatureLayerCreationParams(fc)
                {
                    Name = className,
                    MapMemberPosition = MapMemberPosition.AddToTop
                };
                var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flParams, map);

                if (layer != null)
                {
                    ApplySymbology(layer, isLine);
                    EnsureLayerIsVisibleAndSelectable(layer, fc);
                    return (true, calculatedExtent);
                }

                return (false, null);
            }
            catch
            {
                return (false, null);
            }
        }

        private void ApplySymbology(FeatureLayer layer, bool isLine)
        {
            try
            {
                if (isLine)
                {
                    var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                        ColorFactory.Instance.CreateRGBColor(34, 139, 34), // Verde
                        3.0,
                        SimpleLineStyle.Solid
                    );

                    var rendererDef = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = lineSymbol.MakeSymbolReference()
                    };

                    var renderer = layer.CreateRenderer(rendererDef);
                    layer.SetRenderer(renderer);
                }
                else
                {
                    var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                        ColorFactory.Instance.CreateRGBColor(255, 140, 0), // Naranja
                        12,
                        SimpleMarkerStyle.Circle
                    );

                    var rendererDef = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = pointSymbol.MakeSymbolReference()
                    };

                    var renderer = layer.CreateRenderer(rendererDef);
                    layer.SetRenderer(renderer);
                }
            }
            catch { }
        }

        private void EnsureLayerIsVisibleAndSelectable(FeatureLayer layer, FeatureClass fc)
        {
            try
            {
                try { layer.SetVisibility(true); } catch { }
                try { layer.SetDefinitionQuery(""); } catch { }

                try
                {
                    var cim = layer.GetDefinition() as CIMFeatureLayer;
                    if (cim != null)
                    {
                        cim.MinScale = 0;
                        cim.MaxScale = 0;
                        layer.SetDefinition(cim);
                    }
                }
                catch { }

                try
                {
                    var oids = new List<long>();
                    using (var featureCursor = fc.Search(null, false))
                    {
                        int cnt = 0;
                        while (featureCursor.MoveNext() && cnt < 5)
                        {
                            using var row = featureCursor.Current as Row;
                            if (row != null)
                            {
                                oids.Add(row.GetObjectID());
                                cnt++;
                            }
                        }
                    }
                    if (oids.Count > 0)
                    {
                        layer.Select(new QueryFilter { ObjectIDs = oids });
                    }
                }
                catch { }
            }
            catch { }
        }

        private void EnsureLayerForTargetClass(Map map, Geodatabase gdb, string targetClassName, bool isLine)
        {
            try
            {
                using var fc = OpenTargetFeatureClass(gdb, targetClassName);
                if (fc == null)
                    return;

                // A diferencia de antes, agregamos la capa aun si está vacía.
                // Esto ayuda a inicializar la sesión de edición y a que las
                // futuras inserciones se reflejen inmediatamente en el mapa.

                var existingLayer = map.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .FirstOrDefault(l => l.Name.Equals(targetClassName, StringComparison.OrdinalIgnoreCase));

                if (existingLayer != null)
                {
                    ApplySymbology(existingLayer, isLine);
                    EnsureLayerIsVisibleAndSelectable(existingLayer, fc);
                    return;
                }

                var flParams = new FeatureLayerCreationParams(fc)
                {
                    Name = targetClassName,
                    MapMemberPosition = MapMemberPosition.AddToTop
                };
                var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flParams, map);

                if (layer != null)
                {
                    ApplySymbology(layer, isLine);
                    EnsureLayerIsVisibleAndSelectable(layer, fc);
                }
            }
            catch { }
        }

        #endregion
    }
}