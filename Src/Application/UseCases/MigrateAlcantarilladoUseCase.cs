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
        public async Task<(bool ok, string message)> MigrateLines(string sourceLineasPath, string targetGdbPath)
        {
            return await QueuedTask.Run(() =>
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
                            System.Diagnostics.Debug.WriteLine($"🌍 SR del Mapa (Líneas): WKID={mapSpatialReference?.Wkid}, Name={mapSpatialReference?.Name}");
                        }
                        catch (Exception exSR)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ No se pudo obtener SR del mapa: {exSR.Message}");
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
                            log.AppendLine($"📋 Primera feature: CLASE={clase}, SUBTIPO={subtipo}, SISTEMA={tipoSistema}");
                            log.AppendLine($"   Campos disponibles: {string.Join(", ", GetFieldNames(feature))}");
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
                            System.Diagnostics.Debug.WriteLine($"⚠ CLASE={clase}, SISTEMA={tipoSistema} -> Sin clase destino");
                            continue;
                        }

                        if (!FeatureClassExists(targetGdb, targetClassName))
                        {
                            noTarget++;
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
                                if (editingDisabledDetected == false)
                                {
                                    log.AppendLine($"   ✖ Error de edición: {migrateErr}");
                                }
                                if (migrateErr != null && migrateErr.IndexOf("Editing in the application is not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    editingDisabledDetected = true;
                                    log.AppendLine("   👉 Habilita la edición en ArcGIS Pro: Proyecto > Opciones > Edición > Habilitar edición.");
                                    log.AppendLine("   Se detuvo la migración para evitar mensajes repetidos.");
                                    break;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            if (!perClassStats.TryGetValue(targetClassName, out var stats))
                                stats = (0, 0, 0);
                            stats.attempts++;
                            if (string.IsNullOrWhiteSpace(migrateErr)) stats.migrated++; else stats.failed++;
                            perClassStats[targetClassName] = stats;
                        }
                    }

                    log.AppendLine($"\n📊 Resumen:");
                    log.AppendLine($"   Total features: {total}");
                    log.AppendLine($"   Migradas: {migrated}");
                    log.AppendLine($"   Sin CLASE: {noClase}");
                    log.AppendLine($"   Sin clase destino: {noTarget}");
                    log.AppendLine($"   Fallos: {failed}");

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
                        log.AppendLine($"   ⚠ No se pudo escribir CSV de migración líneas: {exCsv.Message}");
                    }
                    return (true, log.ToString());
                }
                catch (Exception ex)
                {
                    log.AppendLine($"\n❌ Error: {ex.Message}");
                    return (false, log.ToString());
                }
            });
        }

        public async Task<(bool ok, string message)> MigratePoints(string sourcePuntosPath, string targetGdbPath)
        {
            return await QueuedTask.Run(() =>
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
                            System.Diagnostics.Debug.WriteLine($"🌍 SR del Mapa (Puntos): WKID={mapSpatialReference?.Wkid}, Name={mapSpatialReference?.Name}");
                        }
                        catch (Exception exSR)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ No se pudo obtener SR del mapa: {exSR.Message}");
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
                            log.AppendLine($"📋 Primera feature: CLASE={clase}, SUBTIPO={subtipo}, SISTEMA={tipoSistema}");
                            log.AppendLine($"   Campos: {string.Join(", ", GetFieldNames(feature))}");
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
                            System.Diagnostics.Debug.WriteLine($"⚠ CLASE={clase}, SISTEMA={tipoSistema} -> Sin clase destino");
                            continue;
                        }

                        if (!FeatureClassExists(targetGdb, targetClassName))
                        {
                            noTarget++;
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
                                if (editingDisabledDetected == false)
                                {
                                    log.AppendLine($"   ✖ Error de edición: {migrateErr}");
                                }
                                if (migrateErr != null && migrateErr.IndexOf("Editing in the application is not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    editingDisabledDetected = true;
                                    log.AppendLine("   👉 Habilita la edición en ArcGIS Pro: Proyecto > Opciones > Edición > Habilitar edición.");
                                    log.AppendLine("   Se detuvo la migración para evitar mensajes repetidos.");
                                    break;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            if (!perClassStats.TryGetValue(targetClassName, out var stats))
                                stats = (0, 0, 0);
                            stats.attempts++;
                            if (string.IsNullOrWhiteSpace(migrateErr)) stats.migrated++; else stats.failed++;
                            perClassStats[targetClassName] = stats;
                        }
                    }

                    log.AppendLine($"\n📊 Resumen:");
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
                        log.AppendLine($"   ⚠ No se pudo escribir CSV de migración puntos: {exCsv.Message}");
                    }
                    return (true, log.ToString());
                }
                catch (Exception ex)
                {
                    log.AppendLine($"\n❌ Error: {ex.Message}");
                    return (false, log.ToString());
                }
            });
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
                {
                    if (field == "SISTEMA")
                        System.Diagnostics.Debug.WriteLine("⚠ Campo SISTEMA no encontrado en feature");
                    return default;
                }
                var val = feature[idx];
                if (val == null || val is DBNull)
                {
                    if (field == "SISTEMA")
                        System.Diagnostics.Debug.WriteLine("⚠ Campo SISTEMA es nulo");
                    return default;
                }

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Error en GetFieldValue({field}): {ex.Message}");
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
                4 => prefix + "RedLocal",
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
                6 => prefix + "EstructuraRed", 
                7 => prefix + "Sumidero",      
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
                    error = $"No se encontró la clase de destino: {targetClassName}";
                    return false;
                }

                var geometry = sourceFeature.GetShape();
                if (geometry == null || geometry.IsEmpty)
                {
                    error = "Geometría nula o vacía en origen";
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
                {
                    return true;
                }

                var editOp = new EditOperation
                {
                    Name = $"Migrar línea -> {targetClassName}",
                    SelectNewFeatures = false
                };

                editOp.Callback(context =>
                {
                    using (var rowBuffer = targetFC.CreateRowBuffer())
                    {
                        foreach (var kv in dict)
                            rowBuffer[kv.Key] = kv.Value;

                        using (var row = targetFC.CreateRow(rowBuffer))
                        {
                            context.Invalidate(row);
                        }
                    }
                }, targetFC);

                bool ok = editOp.Execute();

                if (!ok)
                {
                    var msg = string.IsNullOrWhiteSpace(editOp.ErrorMessage) ? "Edit operation failed." : editOp.ErrorMessage;
                    error = $"Inserción directa falló: {insertErr} | EditOperation falló: {msg}";
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


        private bool MigratePointFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo, out string? error, SpatialReference? mapSpatialReference = null)
        {
            error = null;
            try
            {
                using var targetFC = OpenTargetFeatureClass(targetGdb, targetClassName);
                if (targetFC == null)
                {
                    error = $"No se encontró la clase de destino: {targetClassName}";
                    return false;
                }

                var geometry = sourceFeature.GetShape();
                if (geometry == null || geometry.IsEmpty)
                {
                    error = "Geometría nula o vacía en origen";
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
                {
                    return true;
                }

                var editOp = new EditOperation
                {
                    Name = $"Migrar punto -> {targetClassName}",
                    SelectNewFeatures = false
                };

                editOp.Callback(context =>
                {
                    using (var rowBuffer = targetFC.CreateRowBuffer())
                    {
                        foreach (var kv in dict)
                            rowBuffer[kv.Key] = kv.Value;

                        using (var row = targetFC.CreateRow(rowBuffer))
                        {
                            context.Invalidate(row);
                        }
                    }
                }, targetFC);

                bool ok = editOp.Execute();

                if (!ok)
                {
                    var msg = string.IsNullOrWhiteSpace(editOp.ErrorMessage) ? "Edit operation failed." : editOp.ErrorMessage;
                    error = $"Inserción directa falló: {insertErr} | EditOperation falló: {msg}";
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

        private (bool ok, string? error) TryInsertRowDirect(Geodatabase targetGdb, FeatureClass targetFC, Dictionary<string, object?> dict, SpatialReference? mapSpatialReference = null)
        {
            try
            {
                using var def = targetFC.GetDefinition();
                var fieldMap = def.GetFields().ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
                
                var targetSR = def.GetSpatialReference();
                System.Diagnostics.Debug.WriteLine($"🎯 Target FC SR: WKID={targetSR?.Wkid}, Name={targetSR?.Name}");
                
                string shapeField = def.GetShapeField();
                if (dict.TryGetValue(shapeField, out var geomVal))
                {
                    if (geomVal == null || (geomVal is Geometry g && g.IsEmpty))
                    {
                        return (false, "Geometría nula o vacía al insertar");
                    }
                    
                    if (geomVal is Geometry sourceGeom)
                    {
                        var sourceSR = sourceGeom.SpatialReference;
                        System.Diagnostics.Debug.WriteLine($"📍 Source Geom SR: WKID={sourceSR?.Wkid}");
                        
                        if (targetSR != null && sourceSR != null && sourceSR.Wkid != targetSR.Wkid)
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"🌍 Proyectando geometría: {sourceSR.Wkid} → {targetSR.Wkid}");
                                var projectedGeom = GeometryEngine.Instance.Project(sourceGeom, targetSR);
                                dict[shapeField] = projectedGeom;
                                geomVal = projectedGeom;
                                sourceGeom = projectedGeom;
                                System.Diagnostics.Debug.WriteLine($"✓ Geometría proyectada exitosamente");
                            }
                            catch (Exception exProj)
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ Error proyectando geometría: {exProj.Message}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"ℹ No se requiere proyección (mismo SR o SR nulo)");
                        }
                        
                        bool targetHasZ = def.HasZ();
                        bool targetHasM = def.HasM();
                        bool sourceHasZ = sourceGeom.HasZ;
                        bool sourceHasM = sourceGeom.HasM;
                        
                        if ((sourceHasZ && !targetHasZ) || (sourceHasM && !targetHasM))
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"🔧 Ajustando geometría: Source(Z={sourceHasZ},M={sourceHasM}) → Target(Z={targetHasZ},M={targetHasM})");
                                
                                Geometry adjustedGeom = sourceGeom;
                                
                                if (sourceGeom is MapPoint point)
                                {
                                    adjustedGeom = MapPointBuilderEx.CreateMapPoint(point.X, point.Y, sourceGeom.SpatialReference);
                                }
                                else if (sourceGeom is Polyline line)
                                {
                                    var builder = new PolylineBuilderEx(sourceGeom.SpatialReference);
                                    foreach (var part in line.Parts)
                                    {
                                        var points = new List<MapPoint>();
                                        foreach (var segment in part)
                                        {
                                            var startPt = segment.StartPoint;
                                            points.Add(MapPointBuilderEx.CreateMapPoint(startPt.X, startPt.Y, sourceGeom.SpatialReference));
                                        }
                                        if (part.Count > 0)
                                        {
                                            var lastSegment = part[part.Count - 1];
                                            var endPt = lastSegment.EndPoint;
                                            points.Add(MapPointBuilderEx.CreateMapPoint(endPt.X, endPt.Y, sourceGeom.SpatialReference));
                                        }
                                        if (points.Count > 0)
                                            builder.AddPart(points);
                                    }
                                    adjustedGeom = builder.ToGeometry();
                                }
                                else if (sourceGeom is Polygon poly)
                                {
                                    var builder = new PolygonBuilderEx(sourceGeom.SpatialReference);
                                    foreach (var part in poly.Parts)
                                    {
                                        var points = new List<MapPoint>();
                                        foreach (var segment in part)
                                        {
                                            var startPt = segment.StartPoint;
                                            points.Add(MapPointBuilderEx.CreateMapPoint(startPt.X, startPt.Y, sourceGeom.SpatialReference));
                                        }
       
                                        if (points.Count > 0)
                                            builder.AddPart(points);
                                    }
                                    adjustedGeom = builder.ToGeometry();
                                }
                                
                                dict[shapeField] = adjustedGeom;
                                System.Diagnostics.Debug.WriteLine($"✓ Geometría ajustada correctamente (nuevo HasZ={adjustedGeom.HasZ}, HasM={adjustedGeom.HasM})");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ Error ajustando geometría: {ex.Message}");
                                return (false, $"Error ajustando geometría Z/M: {ex.Message}");
                            }
                        }
                    }
                }

                var editOp = new EditOperation
                {
                    Name = "Insertar feature migrado",
                    SelectNewFeatures = false
                };

                editOp.Callback(context =>
                {
                    using var rowBuffer = targetFC.CreateRowBuffer();
                    foreach (var kv in dict)
                    {
                        if (!fieldMap.TryGetValue(kv.Key, out var fieldDef))
                            continue;
                        if (fieldDef.FieldType == FieldType.OID || fieldDef.FieldType == FieldType.GlobalID)
                            continue;
                        
                        rowBuffer[kv.Key] = kv.Value ?? DBNull.Value;
                    }

                    using var row = targetFC.CreateRow(rowBuffer);
                    context.Invalidate(row);
                }, targetFC);

                bool success = editOp.Execute();
                if (!success)
                {
                    string errMsg = string.IsNullOrWhiteSpace(editOp.ErrorMessage) 
                        ? "EditOperation falló sin mensaje de error" 
                        : editOp.ErrorMessage;
                    System.Diagnostics.Debug.WriteLine($"❌ Inserción con EditOperation falló: {errMsg}");
                    return (false, errMsg);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Inserción directa falló: {ex.Message}");
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
                a["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERV") ?? GetFieldValue<string>(source, "OBSERVACIO") ?? GetFieldValue<string>(source, "OBSERVACIONES");
                a["LONGITUD_M"] = GetFieldValue<double?>(source, "LONGITUD_M");
                a["PENDIENTE"] = GetFieldValue<double?>(source, "PENDIENTE");
                a["PROFUNDIDADMEDIA"] = GetFieldValue<double?>(source, "PROFUNDIDAD");
                a["NUMEROCONDUCTOS"] = GetFieldValue<double?>(source, "NROCONDUCTOS");
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
                a["CODACTIVOS_FIJOS"] = GetFieldValue<string>(source, "CODACTIVOS_FIJOS") ?? GetFieldValue<string>(source, "CODACTIVO_FIJO");
                System.Diagnostics.Debug.WriteLine($"📋 Atributos línea construidos: SUBTIPO={subtipo}, SISTEMA={sistema}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Error construyendo atributos (línea): {ex.Message}");
            }
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
                a["FECHAINSTALACION"] = GetFieldValue<DateTime?>(source, "FECHADATO") ?? GetFieldValue<DateTime?>(source, "FECHAINST");
                a["DISENO_ID"] = GetFieldValue<string>(source, "NDISENO");
                a["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_ID") ?? GetFieldValue<string>(source, "CONTRATO_I");
                a["DOMESTADOENRED"] = GetFieldValue<string>(source, "ESTADOENRED") ?? GetFieldValue<string>(source, "ESTADOENRE");
                a["DOMCALIDADDATO"] = GetFieldValue<string>(source, "CALIDADDATO") ?? GetFieldValue<string>(source, "CALIDADDAT");
                a["DOMMATERIAL"] = GetFieldValue<string>(source, "MATERIAL");
                a["LOCALIZACIONRELATIVA"] = GetFieldValue<string>(source, "LOCALIZACIONRELATIVA") ?? GetFieldValue<string>(source, "LOCALIZACI");
                a["ROTACIONSIMBOLO"] = GetFieldValue<double?>(source, "ROTACION");
                a["DIRECCION"] = GetFieldValue<string>(source, "DIRECCION");
                a["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
                a["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERV") ?? GetFieldValue<string>(source, "OBSERVACIO");
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
                a["DOMINICIALVARIASCUENCAS"] = GetFieldValue<string>(source, "INICIAL_CUENCAS");
                a["DOMCAMARASIFON"] = GetFieldValue<string>(source, "CAMARASIF");
                a["DOMESTADOPOZO"] = GetFieldValue<string>(source, "EST_POZO");
                a["DOMESTADOOPERACION"] = GetFieldValue<string>(source, "ESTOPERA");
                a["DOMTIPOALMACENAMIENTO"] = GetFieldValue<string>(source, "TIPOALMAC");
                a["DOMESTADOFISICO"] = GetFieldValue<string>(source, "EST_FISICO");
                a["DOMTIPOVALVULAANTIRREFLUJO"] = GetFieldValue<string>(source, "TIPO_VALV_ANT");
                a["DOMTIPOALIVIO"] = GetFieldValue<string>(source, "TIPO_ALIVI") ?? GetFieldValue<string>(source, "TIPO_ALIVIO");
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
                a["CODACTIVO_FIJO"] = GetFieldValue<string>(source, "CODACTIVO_FIJO");
                System.Diagnostics.Debug.WriteLine($"📋 Atributos punto construidos: SUBTIPO={subtipo}, SISTEMA={sistema}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Error construyendo atributos (punto): {ex.Message}");
            }
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
                                {
                                    System.Diagnostics.Debug.WriteLine($"⚠ Valor truncado para {fieldDef.Name}: '{s}' -> '{s.Substring(0, maxLen)}' (max {maxLen})");
                                    s = s.Substring(0, maxLen);
                                }
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
                System.Diagnostics.Debug.WriteLine($"⚠ No se pudo convertir valor '{value}' al tipo de campo {fieldDef.Name}:{fieldDef.FieldType}");
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

       public async Task<(bool ok, string message)> AddMigratedLayersToMap(string targetGdbPath)
        {
            return await QueuedTask.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        return (false, "No hay un mapa activo");
                    }

                    log.AppendLine($"🗺️ Mapa activo: {map.Name}");
                    try
                    {
                        var mapSR = map.SpatialReference;
                        if (mapSR != null)
                            log.AppendLine($"   SR del mapa: WKID={mapSR.Wkid}, Name={mapSR.Name}");
                    }
                    catch { }
                    log.AppendLine($"📂 GDB: {targetGdbPath}");

                    if (!Directory.Exists(targetGdbPath))
                    {
                        return (false, $"La GDB no existe: {targetGdbPath}");
                    }

                    using var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath)));
                    
                    log.AppendLine("🗺️ Agregando capas de alcantarillado al mapa...");

                    var layersAdded = new List<string>();
                    Envelope? combinedExtent = null;

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
                        
                        if (combinedExtent != null && MapView.Active != null)
                        {
                            log.AppendLine($"� Extent: XMin={combinedExtent.XMin:F2}, YMin={combinedExtent.YMin:F2}, XMax={combinedExtent.XMax:F2}, YMax={combinedExtent.YMax:F2}");
                            
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
                                log.AppendLine($"✓ Zoom aplicado (expandido 10%)");
                                
                                MapView.Active.Redraw(true);
                            }
                            else
                            {
                                log.AppendLine($"⚠ Extent inválido (contiene NaN), no se puede hacer zoom");
                            }
                        }
                        
                        return (true, log.ToString());
                    }
                    else
                    {
                        log.AppendLine("⚠ No se agregó ninguna capa. Revisa la ventana Output > Debug para más detalles.");
                        return (false, log.ToString());
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine($"❌ Error: {ex.Message}");
                    log.AppendLine($"Stack: {ex.StackTrace}");
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
                {
                    System.Diagnostics.Debug.WriteLine($"❌ {className}: No se pudo abrir la FeatureClass");
                    return (false, null);
                }

                var count = fc.GetCount();
                if (count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ {className}: Capa vacía (0 features)");
                    return (false, null);
                }

                System.Diagnostics.Debug.WriteLine($"📊 {className}: {count} features encontradas");

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
                    
                    if (calculatedExtent != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Extent calculado desde geometrías - X[{calculatedExtent.XMin:F2}, {calculatedExtent.XMax:F2}] Y[{calculatedExtent.YMin:F2}, {calculatedExtent.YMax:F2}]");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error calculando extent - {ex.Message}");
                }

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
                        var layerFcName = layerFc?.GetName() ?? string.Empty;
                        var targetFcName = fc.GetName();
                        sameDatasource =
                            !string.IsNullOrEmpty(layerGdbPath) &&
                            layerGdbPath.Equals(targetGdbPath, StringComparison.OrdinalIgnoreCase) &&
                            layerFcName.Equals(targetFcName, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { }

                    if (sameDatasource)
                    {
                        System.Diagnostics.Debug.WriteLine($"Capa ya existe y apunta a la misma fuente, aplicando simbología");
                        ApplySymbology(existingLayer, isLine);
                        EnsureLayerIsVisibleAndSelectable(existingLayer, fc);
                        return (true, calculatedExtent);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Existe una capa con el mismo nombre pero distinta fuente. Se reemplazará.");
                        try { map.RemoveLayer(existingLayer); } catch { }
                    }
                }

                using var fcDef = fc.GetDefinition();
                var gdbPath = gdb.GetPath().LocalPath;
                
                string fullPath = Path.Combine(gdbPath, className);
                
                try
                {
                    var fdDefs = gdb.GetDefinitions<FeatureDatasetDefinition>();
                    foreach (var fdDef in fdDefs)
                    {
                        try
                        {
                            using var fd = gdb.OpenDataset<FeatureDataset>(fdDef.GetName());
                            using var testFc = fd.OpenDataset<FeatureClass>(className);
                            if (testFc != null)
                            {
                                fullPath = Path.Combine(gdbPath, fdDef.GetName(), className);
                                System.Diagnostics.Debug.WriteLine($"Encontrada en dataset {fdDef.GetName()}");
                                break;
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                System.Diagnostics.Debug.WriteLine($"Creando capa {className} desde FeatureClass...");
                var flParams = new FeatureLayerCreationParams(fc)
                {
                    Name = className,
                    MapMemberPosition = MapMemberPosition.AddToTop
                };
                var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flParams, map);
                
                if (layer != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Capa creada exitosamente");
                    ApplySymbology(layer, isLine);
                    EnsureLayerIsVisibleAndSelectable(layer, fc);
                    
                    try
                    {
                        layer.SetDefinitionQuery("1=1");
                        layer.SetDefinitionQuery("");
                    }
                    catch { }
                    
                    return (true, calculatedExtent);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"LayerFactory devolvió null");
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {ex.StackTrace}");
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
                        ColorFactory.Instance.CreateRGBColor(34, 139, 34), 
                        1.2,
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
                        ColorFactory.Instance.CreateRGBColor(255, 140, 0), 
                        4,
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
            catch
            {
            }
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

                var count = fc.GetCount();
                if (count == 0)
                    return;

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
            catch
            {
            }
        }

        #endregion
    }
}
