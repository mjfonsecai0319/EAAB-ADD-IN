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
    /// <summary>
    /// Migra líneas y puntos de Acueducto desde shapefiles/GDB origen a la GDB de cargue.
    /// Basado en Cargue_Acueducto.py con mapeos completos de atributos.
    /// </summary>
    public class MigrateAcueductoUseCase
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

                    // Asegurar que las capas de destino estén presentes en el mapa (ayuda a iniciar sesión de edición del workspace)
                    var map = MapView.Active?.Map;
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

                        if (total == 1)
                        {
                            log.AppendLine($"📋 Primera feature (línea ACU): CLASE={clase}, SUBTIPO={subtipo}");
                            log.AppendLine($"   Campos disponibles: {string.Join(", ", GetFieldNames(feature))}");
                            
                            // Verificar geometría de primera feature
                            var geom = feature.GetShape();
                            if (geom != null && !geom.IsEmpty)
                            {
                                var sr = geom.SpatialReference;
                                string srInfo = sr != null ? $"WKID={sr.Wkid}, Name={sr.Name}" : "SIN SR";
                                log.AppendLine($"   Geometría origen: Tipo={geom.GeometryType}, HasZ={geom.HasZ}, HasM={geom.HasM}");
                                log.AppendLine($"   SR Origen: {srInfo}");
                                
                                if (geom is Polyline polyline)
                                {
                                    log.AppendLine($"   Longitud: {polyline.Length:F2} m, Puntos: {polyline.PointCount}");
                                    var extent = polyline.Extent;
                                    log.AppendLine($"   Extent: XMin={extent.XMin:F2}, YMin={extent.YMin:F2}, XMax={extent.XMax:F2}, YMax={extent.YMax:F2}");
                                }
                                
                                // Verificar SR del destino
                                if (clase.HasValue)
                                {
                                    using var targetFC = OpenTargetFeatureClass(targetGdb, GetTargetLineClassName(clase.Value));
                                    if (targetFC != null)
                                    {
                                        using var targetDef = targetFC.GetDefinition();
                                        var targetSR = targetDef.GetSpatialReference();
                                        string targetSRInfo = targetSR != null ? $"WKID={targetSR.Wkid}, Name={targetSR.Name}" : "SIN SR";
                                        log.AppendLine($"   SR Destino: {targetSRInfo}");
                                        
                                        if (sr != null && targetSR != null && !sr.IsEqual(targetSR))
                                        {
                                            bool isMagnaSource = sr.Wkid == 102233 || sr.Wkid == 6247;
                                            bool isMagnaTarget = targetSR.Wkid == 102233 || targetSR.Wkid == 6247;
                                            if (isMagnaSource && isMagnaTarget)
                                            {
                                                log.AppendLine($"   ✓ Ambos son MAGNA Bogotá - Se mantendrán coordenadas exactas");
                                            }
                                            else
                                            {
                                                log.AppendLine($"   ⚠ Sistemas diferentes - Se reproyectará");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                log.AppendLine($"   ⚠ Geometría nula o vacía en primera feature");
                            }
                        }

                        if (!clase.HasValue || clase.Value == 0)
                        {
                            noClase++;
                            continue;
                        }

                        string targetClassName = GetTargetLineClassName(clase.Value);
                        if (string.IsNullOrEmpty(targetClassName))
                        {
                            noTarget++;
                            System.Diagnostics.Debug.WriteLine($"⚠ CLASE={clase} -> Sin clase destino (ACU)");
                            continue;
                        }

                        if (!FeatureClassExists(targetGdb, targetClassName))
                        {
                            noTarget++;
                            System.Diagnostics.Debug.WriteLine($"⚠ Clase destino no existe: {targetClassName}");
                            continue;
                        }

                        // Garantizar capa en mapa (solo una vez por clase)
                        if (map != null && !ensuredLayers.Contains(targetClassName))
                        {
                            EnsureLayerForTargetClass(map, targetGdb, targetClassName, isLine: true);
                            ensuredLayers.Add(targetClassName);
                        }

                        if (total <= 5)
                            log.AppendLine($"→ Feature {total}: {targetClassName}");

                        if (MigrateLineFeature(feature, targetGdb, targetClassName, subtipo ?? 0, out var migrateErr))
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

                        // Actualizar stats por clase
                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            if (!perClassStats.TryGetValue(targetClassName, out var stats))
                                stats = (0, 0, 0);
                            stats.attempts++;
                            if (string.IsNullOrWhiteSpace(migrateErr)) stats.migrated++; else stats.failed++;
                            perClassStats[targetClassName] = stats;
                        }
                    }

                    log.AppendLine($"\n📊 Resumen líneas ACU:");
                    log.AppendLine($"   Total: {total}");
                    log.AppendLine($"   Migradas: {migrated}");
                    log.AppendLine($"   Sin CLASE: {noClase}");
                    log.AppendLine($"   Sin clase destino: {noTarget}");
                    log.AppendLine($"   Fallos: {failed}");
                    
                    // Verificar que las features se insertaron
                    if (migrated > 0)
                    {
                        log.AppendLine($"\n🔍 Verificando features insertadas...");
                        var targetClassName = GetTargetLineClassName(4); // Usar una clase común para verificar
                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            using var targetFC = OpenTargetFeatureClass(targetGdb, targetClassName);
                            if (targetFC != null)
                            {
                                var count = targetFC.GetCount();
                                log.AppendLine($"   Features en {targetClassName}: {count}");
                                
                                // Verificar las geometrías insertadas
                                if (count > 0)
                                {
                                    using var def = targetFC.GetDefinition();
                                    var sr = def.GetSpatialReference();
                                    log.AppendLine($"   SR de la clase: WKID={sr?.Wkid}, Name={sr?.Name}");
                                    
                                    using var verifyCursor = targetFC.Search(null, false);
                                    int checkedCount = 0;
                                    int validGeoms = 0;
                                    int emptyGeoms = 0;
                                    while (verifyCursor.MoveNext() && checkedCount < 5)
                                    {
                                        using var feat = verifyCursor.Current as Feature;
                                        if (feat != null)
                                        {
                                            var geom = feat.GetShape();
                                            checkedCount++;
                                            if (geom != null && !geom.IsEmpty)
                                            {
                                                validGeoms++;
                                                if (checkedCount == 1 && geom is Polyline pl)
                                                {
                                                    var ext = pl.Extent;
                                                    var geomSR = geom.SpatialReference;
                                                    log.AppendLine($"   Primera feature insertada:");
                                                    log.AppendLine($"     Extent: XMin={ext.XMin:F2}, YMin={ext.YMin:F2}, XMax={ext.XMax:F2}, YMax={ext.YMax:F2}");
                                                    log.AppendLine($"     SR: WKID={geomSR?.Wkid}");
                                                    log.AppendLine($"     Longitud: {pl.Length:F2}, Puntos: {pl.PointCount}");
                                                }
                                            }
                                            else
                                            {
                                                emptyGeoms++;
                                            }
                                        }
                                    }
                                    log.AppendLine($"   ✓ Geometrías válidas: {validGeoms}/{checkedCount}");
                                    if (emptyGeoms > 0)
                                        log.AppendLine($"   ⚠ Geometrías vacías: {emptyGeoms}");
                                }
                            }
                        }
                    }

                    // Escribir CSV resumen
                    try
                    {
                        var csv = new Services.CsvReportService();
                        var folder = csv.EnsureReportsFolder(targetGdbPath);
                        var listStats = perClassStats.Select(kv => (kv.Key, kv.Value.attempts, kv.Value.migrated, kv.Value.failed));
                        var file = csv.WriteMigrationSummary(folder, "acueducto_lineas", listStats, noClase, noTarget);
                        log.AppendLine($"   📁 CSV: {file}");
                    }
                    catch (Exception exCsv)
                    {
                        log.AppendLine($"   ⚠ No se pudo escribir CSV de migración líneas ACU: {exCsv.Message}");
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

                    // Asegurar que las capas de destino estén presentes en el mapa (ayuda a iniciar sesión de edición del workspace)
                    var map = MapView.Active?.Map;
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

                        if (total == 1)
                        {
                            log.AppendLine($"📋 Primera feature (punto ACU): CLASE={clase}, SUBTIPO={subtipo}");
                            log.AppendLine($"   Campos: {string.Join(", ", GetFieldNames(feature))}");
                            
                            // Verificar geometría de primera feature
                            var geom = feature.GetShape();
                            if (geom != null && !geom.IsEmpty)
                            {
                                var sr = geom.SpatialReference;
                                string srInfo = sr != null ? $"WKID={sr.Wkid}, Name={sr.Name}" : "SIN SR";
                                log.AppendLine($"   Geometría origen: Tipo={geom.GeometryType}, HasZ={geom.HasZ}, HasM={geom.HasM}");
                                log.AppendLine($"   SR Origen: {srInfo}");
                                
                                if (geom is MapPoint point)
                                {
                                    log.AppendLine($"   Coordenadas: X={point.X:F2}, Y={point.Y:F2}");
                                }
                                
                                // Verificar SR del destino
                                using var targetFC = OpenTargetFeatureClass(targetGdb, GetTargetPointClassName(clase.Value));
                                if (targetFC != null)
                                {
                                    using var targetDef = targetFC.GetDefinition();
                                    var targetSR = targetDef.GetSpatialReference();
                                    string targetSRInfo = targetSR != null ? $"WKID={targetSR.Wkid}, Name={targetSR.Name}" : "SIN SR";
                                    log.AppendLine($"   SR Destino: {targetSRInfo}");
                                    
                                    if (sr != null && targetSR != null && !sr.IsEqual(targetSR))
                                    {
                                        bool isMagnaSource = sr.Wkid == 102233 || sr.Wkid == 6247;
                                        bool isMagnaTarget = targetSR.Wkid == 102233 || targetSR.Wkid == 6247;
                                        if (isMagnaSource && isMagnaTarget)
                                        {
                                            log.AppendLine($"   ✓ Ambos son MAGNA Bogotá - Se mantendrán coordenadas exactas");
                                        }
                                        else
                                        {
                                            log.AppendLine($"   ⚠ Sistemas diferentes - Se reproyectará");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                log.AppendLine($"   ⚠ Geometría nula o vacía en primera feature");
                            }
                        }

                        if (!clase.HasValue || clase.Value == 0)
                        {
                            noClase++;
                            continue;
                        }

                        string targetClassName = GetTargetPointClassName(clase.Value);
                        if (string.IsNullOrEmpty(targetClassName))
                        {
                            noTarget++;
                            System.Diagnostics.Debug.WriteLine($"⚠ CLASE={clase} -> Sin clase destino (punto ACU)");
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

                        if (MigratePointFeature(feature, targetGdb, targetClassName, subtipo ?? 0, out var migrateErr))
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
                                    log.AppendLine("   👉 Habilita la edición en ArcGIS Pro.");
                                    log.AppendLine("   Se detuvo la migración para evitar mensajes repetidos.");
                                    break;
                                }
                            }
                        }

                        // Actualizar stats por clase
                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            if (!perClassStats.TryGetValue(targetClassName, out var stats))
                                stats = (0, 0, 0);
                            stats.attempts++;
                            if (string.IsNullOrWhiteSpace(migrateErr)) stats.migrated++; else stats.failed++;
                            perClassStats[targetClassName] = stats;
                        }
                    }

                    log.AppendLine($"\n📊 Resumen puntos ACU:");
                    log.AppendLine($"   Total: {total}");
                    log.AppendLine($"   Migradas: {migrated}");
                    log.AppendLine($"   Sin CLASE: {noClase}");
                    log.AppendLine($"   Sin destino: {noTarget}");
                    log.AppendLine($"   Fallos: {failed}");
                    
                    // Verificar que las features se insertaron
                    if (migrated > 0)
                    {
                        log.AppendLine($"\n🔍 Verificando features insertadas...");
                        var targetClassName = GetTargetPointClassName(7); // Usar una clase común para verificar
                        if (!string.IsNullOrEmpty(targetClassName))
                        {
                            using var targetFC = OpenTargetFeatureClass(targetGdb, targetClassName);
                            if (targetFC != null)
                            {
                                var count = targetFC.GetCount();
                                log.AppendLine($"   Features en {targetClassName}: {count}");
                                
                                // Verificar las geometrías insertadas
                                if (count > 0)
                                {
                                    using var def = targetFC.GetDefinition();
                                    var sr = def.GetSpatialReference();
                                    log.AppendLine($"   SR de la clase: WKID={sr?.Wkid}, Name={sr?.Name}");
                                    
                                    using var verifyCursor = targetFC.Search(null, false);
                                    int checkedCount = 0;
                                    int validGeoms = 0;
                                    int emptyGeoms = 0;
                                    while (verifyCursor.MoveNext() && checkedCount < 5)
                                    {
                                        using var feat = verifyCursor.Current as Feature;
                                        if (feat != null)
                                        {
                                            var geom = feat.GetShape();
                                            checkedCount++;
                                            if (geom != null && !geom.IsEmpty)
                                            {
                                                validGeoms++;
                                                if (checkedCount == 1 && geom is MapPoint mp)
                                                {
                                                    var geomSR = geom.SpatialReference;
                                                    log.AppendLine($"   Primera feature insertada:");
                                                    log.AppendLine($"     Coordenadas: X={mp.X:F2}, Y={mp.Y:F2}");
                                                    log.AppendLine($"     SR: WKID={geomSR?.Wkid}");
                                                }
                                            }
                                            else
                                            {
                                                emptyGeoms++;
                                            }
                                        }
                                    }
                                    log.AppendLine($"   ✓ Geometrías válidas: {validGeoms}/{checkedCount}");
                                    if (emptyGeoms > 0)
                                        log.AppendLine($"   ⚠ Geometrías vacías: {emptyGeoms}");
                                }
                            }
                        }
                    }

                    // Escribir CSV resumen
                    try
                    {
                        var csv = new Services.CsvReportService();
                        var folder = csv.EnsureReportsFolder(targetGdbPath);
                        var listStats = perClassStats.Select(kv => (kv.Key, kv.Value.attempts, kv.Value.migrated, kv.Value.failed));
                        var file = csv.WriteMigrationSummary(folder, "acueducto_puntos", listStats, noClase, noTarget);
                        log.AppendLine($"   📁 CSV: {file}");
                    }
                    catch (Exception exCsv)
                    {
                        log.AppendLine($"   ⚠ No se pudo escribir CSV de migración puntos ACU: {exCsv.Message}");
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

                    // Intentar abrir directamente (raíz)
                    try
                    {
                        var fc = gdb.OpenDataset<FeatureClass>(fcName);
                        if (fc != null) return fc;
                    }
                    catch { }

                    // Si viene con FD\FC, separar y abrir desde FD
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

                    // Explorar todos los FeatureDatasets por si el nombre viene sin FD correcto
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

        // Mapeo de CLASE a nombre de FeatureClass (acueducto líneas)
        // 1=RedMatriz, 2=Conduccion, 3=Conduccion, 4=RedMenor, 5=LineaLateral
        private string GetTargetLineClassName(int clase)
        {
            return clase switch
            {
                1 => "acd_RedMatriz",
                2 => "acd_Conduccion",
                3 => "acd_Conduccion",
                4 => "acd_RedMenor",
                5 => "acd_LineaLateral",
                _ => string.Empty
            };
        }

        // Mapeo de CLASE a nombre de FeatureClass (acueducto puntos)
        // Según script Python: 1-21 diferentes tipos
        // Nota: CLASE 3 puede ir a acd_Accesorio o acd_CodosPasivos según atributo,
        // aquí simplificamos a acd_Accesorio
        private string GetTargetPointClassName(int clase)
        {
            return clase switch
            {
                1 => "acd_ValvulaSistema",
                2 => "acd_ValvulaControl",
                3 => "acd_Accesorio",       // ACCESORIO_CODO (o CodosPasivos si atributo[29]!='1')
                4 => "acd_Accesorio",       // ACCESORIO_REDUCCION
                5 => "acd_Accesorio",       // ACCESORIO_TAPON
                6 => "acd_Accesorio",       // ACCESORIO_TEE
                7 => "acd_Accesorio",       // ACCESORIO_UNION
                8 => "acd_Accesorio",       // ACCESORIO_OTROS
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
                20 => "acd_ValvulaControl", // ESTRUCTURA_CONTROL -> ValvulaControl según Python
                21 => "acd_CamaraAcceso",   // INSTRUMENTOS_MEDICION -> CamaraAcceso según Python
                _ => string.Empty
            };
        }

        private bool MigrateLineFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo, out string? error)
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

                // ✅ PRIMERO: Intentar inserción directa (más confiable y no depende de sesión de edición)
                var (insertOk, insertErr) = TryInsertRowDirect(targetFC, dict);
                if (insertOk)
                {
                    // (Sin auto refresco de mapa por requerimiento del usuario)
                    return true;
                }

                // ⚠️ FALLBACK: Si falla inserción directa, intentar con EditOperation
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

                // (Sin auto refresco de mapa por requerimiento del usuario)

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }


        private bool MigratePointFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo, out string? error)
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

                // Atributos
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

                // ✅ PRIMERO: Intentar inserción directa (más confiable y no depende de sesión de edición)
                var (insertOk, insertErr) = TryInsertRowDirect(targetFC, dict);
                if (insertOk)
                {
                    // (Sin auto refresco de mapa por requerimiento del usuario)
                    return true;
                }

                // ⚠️ FALLBACK: Si falla inserción directa, intentar con EditOperation
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

                // (Sin auto refresco de mapa por requerimiento del usuario)

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }


        // Mapeo de atributos líneas acueducto (según Python: atrib_l_ecu_shp -> atrib_l_ecu_gdb)
        private Dictionary<string, object?> BuildLineAttributes(Feature source, FeatureClassDefinition def, int subtipo)
        {
            var attrs = new Dictionary<string, object?>();
            try
            {
                attrs["SUBTIPO"] = subtipo;
                // Ubicación / Zona / UGA / IDSIG (agregado para paridad con alcantarillado)
                var ubicTec = GetFieldValue<string>(source, "UBICACIONTECNICA")
                               ?? GetFieldValue<string>(source, "UBICACION_TECNICA")
                               ?? GetFieldValue<string>(source, "UBIC_TECNICA")
                               ?? GetFieldValue<string>(source, "UBIC_TECN");
                if (!string.IsNullOrWhiteSpace(ubicTec))
                {
                    attrs["UBICACIONTECNICA"] = ubicTec;
                    attrs["UBICACION_TECNICA"] = ubicTec; // alias posible en esquema destino
                }
                attrs["ZONA"] = GetFieldValue<string>(source, "ZONA");
                attrs["UGA"] = GetFieldValue<string>(source, "UGA") ?? GetFieldValue<string>(source, "UGA_ID");
                attrs["IDSIG"] = GetFieldValue<string>(source, "IDSIG") ?? GetFieldValue<string>(source, "ID_SIG");
                attrs["N_INICIAL"] = GetFieldValue<string>(source, "N_INICIAL");
                attrs["N_FINAL"] = GetFieldValue<string>(source, "N_FINAL");
                attrs["FECHAINST"] = GetFieldValue<DateTime?>(source, "FECHAINST");
                attrs["ESTADOENRED"] = GetFieldValue<string>(source, "ESTADOENRE");
                attrs["DIAMETRO"] = GetFieldValue<string>(source, "DIAMETRO");
                attrs["MATERIAL"] = GetFieldValue<string>(source, "MATERIAL");
                attrs["CALIDADDEDATO"] = GetFieldValue<string>(source, "CALIDADDED");
                attrs["ESTADOLEGAL"] = GetFieldValue<string>(source, "ESTADOLEGA");
                attrs["OBSERV"] = GetFieldValue<string>(source, "OBSERV");
                attrs["TIPOINSTALACION"] = GetFieldValue<string>(source, "TIPOINSTAL");
                attrs["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_I");
                attrs["NDISENO"] = GetFieldValue<string>(source, "NDISENO");
                attrs["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
                attrs["COSTADO"] = GetFieldValue<string>(source, "COSTADO");
                attrs["T_SECCION"] = GetFieldValue<string>(source, "T_SECCION");
                attrs["AREA_TR_M2"] = GetFieldValue<double?>(source, "AREA_TR_M2");
                attrs["C_RASANTEI"] = GetFieldValue<double?>(source, "C_RASANTEI");
                attrs["C_RASANTEF"] = GetFieldValue<double?>(source, "C_RASANTEF");
                attrs["C_CLAVEI"] = GetFieldValue<double?>(source, "C_CLAVEI");
                attrs["C_CLAVEF"] = GetFieldValue<double?>(source, "C_CLAVEF");
                attrs["PROFUNDIDAD"] = GetFieldValue<double?>(source, "PROFUNDIDA");
                attrs["RUGOSIDAD"] = GetFieldValue<double?>(source, "RUGOSIDAD");
                attrs["LONGITUD_m"] = GetFieldValue<double?>(source, "LONGITUD_m");
                attrs["CODACTIVO_FIJO"] = GetFieldValue<string>(source, "CODACTIVO_");
            }
            catch { }
            return attrs;
        }

        // Mapeo de atributos puntos acueducto (según Python: atrib_p_acu_shp -> atrib_p_acu_gdb)
        private Dictionary<string, object?> BuildPointAttributes(Feature source, FeatureClassDefinition def, int subtipo)
        {
            var a = new Dictionary<string, object?>();
            a["SUBTIPO"] = subtipo;
            // Ubicación / Zona / UGA / IDSIG (agregado)
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
            a["IDENTIFIC"] = GetFieldValue<string>(source, "IDENTIFIC");
            a["NORTE"] = GetFieldValue<double?>(source, "NORTE");
            a["ESTE"] = GetFieldValue<double?>(source, "ESTE");
            a["FECHAINST"] = GetFieldValue<DateTime?>(source, "FECHAINST");
            a["ESTADOENRED"] = GetFieldValue<string>(source, "ESTADOENRE");
            a["LOCALIZACIONRELATIVA"] = GetFieldValue<string>(source, "LOCALIZACI");
            a["CALIDADDATO"] = GetFieldValue<string>(source, "CALIDADDAT");
            a["ROTACION"] = GetFieldValue<double?>(source, "ROTACION");
            a["C_RASANTE"] = GetFieldValue<double?>(source, "C_RASANTE");
            a["PROFUN"] = GetFieldValue<double?>(source, "PROFUN");
            a["MATERIAL"] = GetFieldValue<string>(source, "MATERIAL");
            a["VINCULO"] = GetFieldValue<string>(source, "VINCULO");
            a["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERVACIO");
            a["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_I");
            a["NDISENO"] = GetFieldValue<string>(source, "NDISENO");
            a["TIPOESPPUB"] = GetFieldValue<string>(source, "TIPOESPPUB");
            a["MATESPPUBL"] = GetFieldValue<string>(source, "MATESPPUBL");
            a["AUTOMATIZA"] = GetFieldValue<int?>(source, "AUTOMATIZA");
            a["DIAMETRO1"] = GetFieldValue<string>(source, "DIAMETRO1");
            a["DIAMETRO2"] = GetFieldValue<string>(source, "DIAMETRO2");
            a["SENTIDOOPERAC"] = GetFieldValue<string>(source, "SENTIDOOPE");
            a["ESTADOOPERAC"] = GetFieldValue<string>(source, "ESTADOOPER");
            a["TIPOOPERAC"] = GetFieldValue<string>(source, "TIPOOPERAC");
            a["ESTADOFIS_VAL"] = GetFieldValue<string>(source, "ESTADOFIS_");
            a["TIPOVALVUL"] = GetFieldValue<string>(source, "TIPOVALVUL");
            a["VUELTASCIE"] = GetFieldValue<double?>(source, "VUELTASCIE");
            a["CLASEACCES"] = GetFieldValue<string>(source, "CLASEACCES");
            a["ESTADOFISICOH"] = GetFieldValue<string>(source, "ESTADOFISI");
            a["MARCA"] = GetFieldValue<string>(source, "MARCA");
            a["FUNCIONPIL"] = GetFieldValue<int?>(source, "FUNCIONPIL");
            a["ESTADOMED"] = GetFieldValue<string>(source, "ESTADOMED");
            a["SECTORENTR"] = GetFieldValue<string>(source, "SECTORENTR");
            a["SECTORSALI"] = GetFieldValue<string>(source, "SECTORSALI");
            a["IDTUBERIAMEDIDA"] = GetFieldValue<string>(source, "IDTUBERIAM");
            a["CAUDAL_PROMEDIO"] = GetFieldValue<double?>(source, "CAUDAL_PRO");
            a["TIPO_M"] = GetFieldValue<string>(source, "TIPO_M");
            a["FECHA_TOMA_C"] = GetFieldValue<DateTime?>(source, "FECHA_TOMA");
            a["UBICACCAJI"] = GetFieldValue<string>(source, "UBICACCAJI");
            a["CENTRO"] = GetFieldValue<string>(source, "CENTRO");
            a["L_ALM"] = GetFieldValue<double?>(source, "L_ALM");
            a["AREARESP"] = GetFieldValue<double?>(source, "AREARESP");
            a["TIPO_MUESTR"] = GetFieldValue<string>(source, "TIPO_MUEST");
            a["FUENTEABAS"] = GetFieldValue<string>(source, "FUENTEABAS");
            a["UBICAC_MUES"] = GetFieldValue<string>(source, "UBICAC_MUE");
            a["PTOANALISI"] = GetFieldValue<string>(source, "PTOANALISI");
            a["LOCPUNTO"] = GetFieldValue<string>(source, "LOCPUNTO");
            a["ESTADO"] = GetFieldValue<string>(source, "ESTADO");
            a["FECHAESTADO"] = GetFieldValue<DateTime?>(source, "FECHAESTAD");
            a["CLASEPUNTO"] = GetFieldValue<string>(source, "CLASEPUNTO");
            a["NROFILTROS"] = GetFieldValue<int?>(source, "NROFILTROS");
            a["NROSEDIMEN"] = GetFieldValue<int?>(source, "NROSEDIMEN");
            a["NROCOMPART"] = GetFieldValue<int?>(source, "NROCOMPART");
            a["NROMEZCLAR"] = GetFieldValue<int?>(source, "NROMEZCLAR");
            a["NROFLOCULA"] = GetFieldValue<int?>(source, "NROFLOCULA");
            a["CAPACINSTA"] = GetFieldValue<double?>(source, "CAPACINSTA");
            a["NROBOMBAS"] = GetFieldValue<int?>(source, "NROBOMBAS");
            a["CAPABOMBEO"] = GetFieldValue<double?>(source, "CAPABOMBEO");
            a["COTABOMBEO"] = GetFieldValue<double?>(source, "COTABOMBEO");
            a["ALTURADINA"] = GetFieldValue<double?>(source, "ALTURADINA");
            a["COTAFONDO"] = GetFieldValue<double?>(source, "COTAFONDO");
            a["COTAREBOSE"] = GetFieldValue<double?>(source, "COTAREBOSE");
            a["CAPACIDAD"] = GetFieldValue<double?>(source, "CAPACIDAD");
            a["NIVELMAXIM"] = GetFieldValue<double?>(source, "NIVELMAXIM");
            a["NIVELMINIM"] = GetFieldValue<double?>(source, "NIVELMINIM");
            a["AREATRANSV"] = GetFieldValue<double?>(source, "AREATRANSV");
            a["TIENEVIGIL"] = GetFieldValue<int?>(source, "TIENEVIGIL");
            a["OPERACTANQ"] = GetFieldValue<string>(source, "OPERACTANQ");
            a["TIPOACCESO"] = GetFieldValue<string>(source, "TIPOACCESO");
            a["DIAMETROAC"] = GetFieldValue<string>(source, "DIAMETROAC");
            a["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
            a["DIRECCION"] = GetFieldValue<string>(source, "DIRECCION");
            a["PRESION"] = GetFieldValue<double?>(source, "PRESION");
            a["CODACTIVO_FIJO"] = GetFieldValue<string>(source, "CODACTIVO_");
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

        private (bool ok, string? error) TryInsertRowDirect(FeatureClass targetFC, Dictionary<string, object?> dict)
        {
            try
            {
                using var def = targetFC.GetDefinition();
                var fieldMap = def.GetFields().ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
                
                // Verificar que la geometría está en el diccionario
                string shapeField = def.GetShapeField();
                if (dict.TryGetValue(shapeField, out var geomValue))
                {
                    if (geomValue == null || (geomValue is Geometry geom && geom.IsEmpty))
                    {
                        return (false, "Geometría nula o vacía al insertar");
                    }
                    
                    // ✅ PROYECCIÓN al SR de la clase destino y ajuste Z/M
                    if (geomValue is Geometry sourceGeom)
                    {
                        var targetSR = def.GetSpatialReference();
                        var sourceSR = sourceGeom.SpatialReference;
                        // Proyectar si SR difiere
                        if (targetSR != null && sourceSR != null && sourceSR.Wkid != targetSR.Wkid)
                        {
                            try
                            {
                                var projected = GeometryEngine.Instance.Project(sourceGeom, targetSR);
                                sourceGeom = projected;
                                dict[shapeField] = projected;
                            }
                            catch (Exception exProj)
                            {
                                return (false, $"Error proyectando geometría: {exProj.Message}");
                            }
                        }

                        bool targetHasZ = def.HasZ();
                        bool targetHasM = def.HasM();
                        bool sourceHasZ = sourceGeom.HasZ;
                        bool sourceHasM = sourceGeom.HasM;

                        if ((sourceHasZ && !targetHasZ) || (sourceHasM && !targetHasM))
                        {
                            try
                            {
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
                                        // Iterar segmentos del part (ReadOnlySegmentCollection) y tomar StartPoint + último EndPoint
                                        MapPoint? lastEnd = null;
                                        foreach (var segment in part)
                                        {
                                            var sp = segment.StartPoint;
                                            points.Add(MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, sourceGeom.SpatialReference));
                                            lastEnd = segment.EndPoint;
                                        }
                                        if (lastEnd != null)
                                        {
                                            points.Add(MapPointBuilderEx.CreateMapPoint(lastEnd.X, lastEnd.Y, sourceGeom.SpatialReference));
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
                                        MapPoint? lastEnd = null;
                                        foreach (var segment in part)
                                        {
                                            var sp = segment.StartPoint;
                                            points.Add(MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, sourceGeom.SpatialReference));
                                            lastEnd = segment.EndPoint;
                                        }
                                        // Para polígonos el último EndPoint coincide con el primer StartPoint, no es necesario agregarlo adicionalmente
                                        if (points.Count > 0)
                                            builder.AddPart(points);
                                    }
                                    adjustedGeom = builder.ToGeometry();
                                }

                                dict[shapeField] = adjustedGeom;
                            }
                            catch (Exception ex)
                            {
                                return (false, $"Error ajustando geometría Z/M: {ex.Message}");
                            }
                        }
                    }
                }

                // ✅ Inserción directa sin verificar transacciones (más simple y robusto)
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
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Diagnóstico: verifica cuántas features hay en cada clase de destino
        /// </summary>
        public async Task<string> DiagnoseTargetGdb(string targetGdbPath)
        {
            return await QueuedTask.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                try
                {
                    using var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath)));
                    log.AppendLine("🔍 Diagnóstico de GDB destino:");
                    
                    var acuClasses = new[] { "acd_RedMatriz", "acd_Conduccion", "acd_RedMenor", "acd_LineaLateral", "acd_Accesorio", "acd_ValvulaControl", "acd_ValvulaSistema" };
                    
                    foreach (var className in acuClasses)
                    {
                        try
                        {
                            using var fc = OpenTargetFeatureClass(targetGdb, className);
                            if (fc != null)
                            {
                                var count = fc.GetCount();
                                using var def = fc.GetDefinition();
                                var sr = def.GetSpatialReference();
                                string srInfo = sr != null ? $"WKID={sr.Wkid}" : "Sin SR";
                                log.AppendLine($"   {className}: {count} features ({srInfo})");
                                
                                // Verificar una muestra con más detalle
                                if (count > 0)
                                {
                                    using var cursor = fc.Search(null, false);
                                    if (cursor.MoveNext())
                                    {
                                        using var feature = cursor.Current as Feature;
                                        if (feature != null)
                                        {
                                            var geom = feature.GetShape();
                                            if (geom != null && !geom.IsEmpty)
                                            {
                                                var geomSR = geom.SpatialReference;
                                                log.AppendLine($"      SR Geometría: WKID={geomSR?.Wkid}");
                                                
                                                if (geom is Polyline pl)
                                                {
                                                    var ext = pl.Extent;
                                                    log.AppendLine($"      Polyline: Long={pl.Length:F2}, Pts={pl.PointCount}");
                                                    log.AppendLine($"      Extent: X[{ext.XMin:F2}, {ext.XMax:F2}] Y[{ext.YMin:F2}, {ext.YMax:F2}]");
                                                }
                                                else if (geom is MapPoint mp)
                                                {
                                                    log.AppendLine($"      Point: X={mp.X:F2}, Y={mp.Y:F2}");
                                                    if (mp.HasZ) log.AppendLine($"      Z={mp.Z:F2}");
                                                }
                                            }
                                            else
                                            {
                                                log.AppendLine($"      ⚠ Feature con geometría vacía!");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                log.AppendLine($"   {className}: No existe");
                            }
                        }
                        catch (Exception ex)
                        {
                            log.AppendLine($"   {className}: Error - {ex.Message}");
                        }
                    }
                    
                    return log.ToString();
                }
                catch (Exception ex)
                {
                    return $"❌ Error: {ex.Message}";
                }
            });
        }

        /// <summary>
        /// Agrega las capas migradas al mapa activo con simbología visible
        /// </summary>
        public async Task<(bool ok, string message)> AddMigratedLayersToMap(string targetGdbPath, bool linesOnly = false, bool pointsOnly = false)
        {
            return await QueuedTask.Run(() =>
            {
                var log = new System.Text.StringBuilder();
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        return (false, "No hay un mapa activo. Abre un mapa en ArcGIS Pro primero.");
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
                    
                    log.AppendLine("🗺️ Agregando capas al mapa...");

                    var layersAdded = new List<string>();
                    Envelope? combinedExtent = null;

                    // Clases de líneas
                    if (!pointsOnly)
                    {
                        var lineClasses = new[] { "acd_RedMatriz", "acd_Conduccion", "acd_RedMenor", "acd_LineaLateral" };
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
                    }

                    // Clases de puntos
                    if (!linesOnly)
                    {
                        var pointClasses = new[] { "acd_Accesorio", "acd_ValvulaControl", "acd_ValvulaSistema", "acd_Hidrante", "acd_CamaraAcceso" };
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
                    }

                    if (layersAdded.Count > 0)
                    {
                        log.AppendLine($"✓ Capas agregadas: {string.Join(", ", layersAdded)}");
                        
                        // Hacer zoom al extent combinado
                        if (combinedExtent != null && MapView.Active != null)
                        {
                            log.AppendLine($"📍 Extent: XMin={combinedExtent.XMin:F2}, YMin={combinedExtent.YMin:F2}, XMax={combinedExtent.XMax:F2}, YMax={combinedExtent.YMax:F2}");
                            
                            // Validar que el extent sea válido
                            if (!double.IsNaN(combinedExtent.XMin) && !double.IsNaN(combinedExtent.YMin))
                            {
                                // Expandir el extent un 10% para dar margen
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
                                
                                // Forzar refresh del mapa
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

                // Calcular extent real desde las geometrías
                Envelope? calculatedExtent = null;
                try
                {
                    using var cursor = fc.Search();
                    int geomCount = 0;
                    while (cursor.MoveNext() && geomCount < 100) // Limitar a 100 para performance
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
                        System.Diagnostics.Debug.WriteLine($"📏 {className}: Extent calculado desde geometrías - X[{calculatedExtent.XMin:F2}, {calculatedExtent.XMax:F2}] Y[{calculatedExtent.YMin:F2}, {calculatedExtent.YMax:F2}]");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ {className}: Error calculando extent - {ex.Message}");
                }

                // Verificar si la capa ya existe y si apunta a la misma fuente de datos
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
                        System.Diagnostics.Debug.WriteLine($"✓ {className}: Capa ya existe y apunta a la misma fuente, aplicando simbología");
                        ApplySymbology(existingLayer, isLine);
                        EnsureLayerIsVisibleAndSelectable(existingLayer, fc);
                        return (true, calculatedExtent);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"↻ {className}: Existe una capa con el mismo nombre pero distinta fuente. Se reemplazará.");
                        try { map.RemoveLayer(existingLayer); } catch { }
                    }
                }

                // Obtener la ruta completa
                using var fcDef = fc.GetDefinition();
                var gdbPath = gdb.GetPath().LocalPath;
                
                // Buscar si está dentro de un FeatureDataset
                string fullPath = Path.Combine(gdbPath, className);
                
                // Intentar encontrar en feature datasets
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
                                System.Diagnostics.Debug.WriteLine($"📂 {className}: Encontrada en dataset {fdDef.GetName()}");
                                break;
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                // Crear y agregar la capa directamente desde el FeatureClass (más robusto que por URI)
                System.Diagnostics.Debug.WriteLine($"🎨 Creando capa {className} desde FeatureClass...");
                var flParams = new FeatureLayerCreationParams(fc)
                {
                    Name = className,
                    MapMemberPosition = MapMemberPosition.AddToTop
                };
                var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flParams, map);
                
                if (layer != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ {className}: Capa creada exitosamente");
                    ApplySymbology(layer, isLine);
                    EnsureLayerIsVisibleAndSelectable(layer, fc);
                    
                    // Forzar recálculo de extent de la capa
                    try
                    {
                        layer.SetDefinitionQuery("1=1"); // Forzar refresh
                        layer.SetDefinitionQuery(""); // Limpiar query
                    }
                    catch { }
                    
                    return (true, calculatedExtent);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ {className}: LayerFactory devolvió null");
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ {className}: Error - {ex.Message}");
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
                    // Simbología para líneas
                    var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                        ColorFactory.Instance.CreateRGBColor(0, 112, 255), // Azul
                        3.0, // Grosor ligeramente mayor para asegurarnos de verlo
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
                    // Simbología para puntos
                    var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                        ColorFactory.Instance.CreateRGBColor(255, 0, 0), // Rojo
                        12, // Tamaño mayor para visibilidad inmediata
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
                // Si falla la simbología, la capa se agrega con la simbología por defecto
            }
        }

        /// <summary>
        /// Asegura que la capa esté visible a cualquier escala y resalta una pequeña muestra de features.
        /// </summary>
        private void EnsureLayerIsVisibleAndSelectable(FeatureLayer layer, FeatureClass fc)
        {
            try
            {
                // Visibilidad y sin definición
                try { layer.SetVisibility(true); } catch { }
                try { layer.SetDefinitionQuery(""); } catch { }

                // Quitar restricciones de escala
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

                // Seleccionar una pequeña muestra para resaltar
                try
                {
                    using var def = fc.GetDefinition();
                    var oidField = def.GetObjectIDField();
                    var oids = new List<long>();
                    using (var cursor = fc.Search(null, false))
                    {
                        int cnt = 0;
                        while (cursor.MoveNext() && cnt < 5)
                        {
                            using var row = cursor.Current as Row;
                            if (row != null)
                            {
                                var oid = Convert.ToInt64(row[oidField]);
                                oids.Add(oid);
                                cnt++;
                            }
                        }
                    }

                    if (oids.Count > 0)
                    {
                        var where = $"{oidField} IN (" + string.Join(",", oids) + ")";
                        var qf = new QueryFilter() { WhereClause = where };
                        layer.Select(qf, SelectionCombinationMethod.New);
                        System.Diagnostics.Debug.WriteLine($"✨ Selección de muestra en {layer.Name}: {oids.Count} features");
                        try { MapView.Active?.Redraw(true); } catch { }
                    }
                }
                catch { }
            }
            catch { }
        }

        /// <summary>
        /// Garantiza que exista una capa para la clase de destino en el mapa, incluso si está vacía.
        /// Esto ayuda a que Pro inicialice la sesión de edición para el workspace de la GDB.
        /// </summary>
        private void EnsureLayerForTargetClass(Map map, Geodatabase gdb, string className, bool isLine)
        {
            try
            {
                // Si ya hay una capa con este nombre y apunta a la misma fuente, no hacer nada
                var existingLayer = map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                    .FirstOrDefault(l => l.Name.Equals(className, StringComparison.OrdinalIgnoreCase));
                if (existingLayer != null)
                {
                    try
                    {
                        using var layerFc = existingLayer.GetFeatureClass();
                        var layerGdb = layerFc?.GetDatastore() as Geodatabase;
                        if (layerGdb != null)
                        {
                            var same = string.Equals(layerGdb.GetPath().LocalPath, gdb.GetPath().LocalPath, StringComparison.OrdinalIgnoreCase)
                                       && string.Equals(layerFc?.GetName(), className, StringComparison.OrdinalIgnoreCase);
                            if (same)
                            {
                                ApplySymbology(existingLayer, isLine);
                                EnsureLayerIsVisibleAndSelectable(existingLayer, layerFc!);
                                return;
                            }
                        }
                    }
                    catch { }
                }

                using var fc = OpenTargetFeatureClass(gdb, className);
                if (fc == null)
                    return;

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
                }
            }
            catch { }
        }

        #endregion
    }
}
