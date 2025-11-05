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
                                log.AppendLine($"   Geometría origen: Tipo={geom.GeometryType}, HasZ={geom.HasZ}, HasM={geom.HasM}");
                                if (geom is Polyline polyline)
                                {
                                    log.AppendLine($"   Longitud: {polyline.Length:F2} m, Puntos: {polyline.PointCount}");
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
                    }

                    log.AppendLine($"\n📊 Resumen líneas ACU:");
                    log.AppendLine($"   Total: {total}");
                    log.AppendLine($"   Migradas: {migrated}");
                    log.AppendLine($"   Sin CLASE: {noClase}");
                    log.AppendLine($"   Sin clase destino: {noTarget}");
                    log.AppendLine($"   Fallos: {failed}");

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
                                log.AppendLine($"   Geometría origen: Tipo={geom.GeometryType}, HasZ={geom.HasZ}, HasM={geom.HasM}");
                                if (geom is MapPoint point)
                                {
                                    log.AppendLine($"   Coordenadas: X={point.X:F2}, Y={point.Y:F2}");
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
                    }

                    log.AppendLine($"\n📊 Resumen puntos ACU:");
                    log.AppendLine($"   Total: {total}");
                    log.AppendLine($"   Migradas: {migrated}");
                    log.AppendLine($"   Sin CLASE: {noClase}");
                    log.AppendLine($"   Sin destino: {noTarget}");
                    log.AppendLine($"   Fallos: {failed}");

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
                    return gdb.OpenDataset<FeatureClass>(fcName);
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

                // Eliminar Z y M si el destino no los soporta
                var targetHasZ = featureClassDef.HasZ();
                var targetHasM = featureClassDef.HasM();
                if ((geometry.HasZ && !targetHasZ) || (geometry.HasM && !targetHasM))
                {
                    try
                    {
                        var builder = new PolylineBuilderEx(geometry as Polyline);
                        builder.HasZ = targetHasZ;
                        builder.HasM = targetHasM;
                        geometry = builder.ToGeometry();
                        
                        // Verificar que la geometría sigue siendo válida después de la conversión
                        if (geometry == null || geometry.IsEmpty)
                        {
                            error = "Geometría inválida después de eliminar Z/M";
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = $"Error eliminando dimensiones Z/M: {ex.Message}";
                        return false;
                    }
                }

                // Reproyectar si es necesario
                try
                {
                    var targetSR = featureClassDef.GetSpatialReference();
                    if (geometry.SpatialReference != null && targetSR != null && !geometry.SpatialReference.IsEqual(targetSR))
                    {
                        geometry = GeometryEngine.Instance.Project(geometry, targetSR);
                    }
                }
                catch { }

                var attributes = BuildLineAttributes(sourceFeature, featureClassDef, subtipo);

                // Construir diccionario con solo campos existentes
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

                var editOp = new EditOperation
                {
                    Name = $"Migrar línea ACU -> {targetClassName}",
                    SelectNewFeatures = false
                };
                editOp.Create((Table)targetFC, dict);
                bool ok = editOp.Execute();
                if (!ok)
                {
                    var msg = string.IsNullOrWhiteSpace(editOp.ErrorMessage) ? "Edit operation failed." : editOp.ErrorMessage;

                    if (msg.IndexOf("Editing in the application is not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var (insertOk, insertErr) = TryInsertRowDirect(targetFC, dict);
                        if (insertOk)
                            return true;
                        else
                        {
                            error = $"{msg} | Fallback directo también falló: {insertErr}";
                            return false;
                        }
                    }

                    error = msg;
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

                var targetHasZ = featureClassDef.HasZ();
                var targetHasM = featureClassDef.HasM();
                if ((geometry.HasZ && !targetHasZ) || (geometry.HasM && !targetHasM))
                {
                    try
                    {
                        var builder = new MapPointBuilderEx(geometry as MapPoint);
                        builder.HasZ = targetHasZ;
                        builder.HasM = targetHasM;
                        geometry = builder.ToGeometry();
                        
                        // Verificar que la geometría sigue siendo válida después de la conversión
                        if (geometry == null || geometry.IsEmpty)
                        {
                            error = "Geometría inválida después de eliminar Z/M";
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = $"Error eliminando dimensiones Z/M: {ex.Message}";
                        return false;
                    }
                }

                try
                {
                    var targetSR = featureClassDef.GetSpatialReference();
                    if (geometry.SpatialReference != null && targetSR != null && !geometry.SpatialReference.IsEqual(targetSR))
                    {
                        geometry = GeometryEngine.Instance.Project(geometry, targetSR);
                    }
                }
                catch { }

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

                var editOp = new EditOperation
                {
                    Name = $"Migrar punto ACU -> {targetClassName}",
                    SelectNewFeatures = false
                };
                editOp.Create((Table)targetFC, dict);
                bool ok = editOp.Execute();
                if (!ok)
                {
                    var msg = string.IsNullOrWhiteSpace(editOp.ErrorMessage) ? "Edit operation failed." : editOp.ErrorMessage;

                    if (msg.IndexOf("Editing in the application is not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var (insertOk, insertErr) = TryInsertRowDirect(targetFC, dict);
                        if (insertOk)
                            return true;
                        else
                        {
                            error = $"{msg} | Fallback directo también falló: {insertErr}";
                            return false;
                        }
                    }

                    error = msg;
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

        // Mapeo de atributos líneas acueducto (según Python: atrib_l_ecu_shp -> atrib_l_ecu_gdb)
        private Dictionary<string, object?> BuildLineAttributes(Feature source, FeatureClassDefinition def, int subtipo)
        {
            var attrs = new Dictionary<string, object?>();
            try
            {
                attrs["SUBTIPO"] = subtipo;
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

        #endregion
    }
}
