#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Editing;

namespace EAABAddIn.Src.Application.UseCases;

public class BuildAffectedAreaPolygonsUseCase
{
    // Actualiza atributos en la misma Feature Class seleccionada (FileGDB)
    public async Task<(bool success, string message, int updatedCount)> InvokeAsync(
        List<Feature> selectedFeatures,
        string targetFeatureClassPath,
        string identifierField,
        string? neighborhoodsPath,
        string? clientsPath)
    {
        if (selectedFeatures == null || selectedFeatures.Count == 0)
            return (false, "No hay features seleccionadas", 0);

        if (string.IsNullOrWhiteSpace(targetFeatureClassPath))
            return (false, "Ruta de Feature Class inválida", 0);

        var (gdbPath, datasetName) = ParseFeatureClassPath(targetFeatureClassPath);
        if (string.IsNullOrWhiteSpace(gdbPath) || string.IsNullOrWhiteSpace(datasetName))
            return (false, "Ruta de Feature Class inválida. Se espera una ruta tipo C:/.../Base.gdb/FeatureClass", 0);

        if (!Directory.Exists(gdbPath))
            return (false, $"La geodatabase no existe: {gdbPath}", 0);

        return await QueuedTask.Run(() => GenerateInternal(
            selectedFeatures,
            gdbPath,
            datasetName,
            identifierField,
            neighborhoodsPath,
            clientsPath));
    }

    private (bool success, string message, int updatedCount) GenerateInternal(
        List<Feature> selectedFeatures,
        string gdbPath,
        string datasetName,
        string identifierField,
        string? neighborhoodsPath,
        string? clientsPath)
    {
        try
        {
            bool includeNeighborhoods = !string.IsNullOrWhiteSpace(neighborhoodsPath);
            bool includeClients = !string.IsNullOrWhiteSpace(clientsPath);

            using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
            using var targetFc = gdb.OpenDataset<FeatureClass>(datasetName);

            // Asegurar campos
            EnsureFieldExists(gdbPath, datasetName, "identificador", "TEXT", 255);
            if (includeNeighborhoods)
                EnsureFieldExists(gdbPath, datasetName, "barrios", "TEXT", 4096);
            if (includeClients)
                EnsureFieldExists(gdbPath, datasetName, "clientes", "LONG", 0);

            // Abrir FC auxiliares
            using var neighborhoodsFc = includeNeighborhoods && neighborhoodsPath != null ? OpenFeatureClassFromPath(neighborhoodsPath) : null;
            using var clientsFc = includeClients && clientsPath != null ? OpenFeatureClassFromPath(clientsPath) : null;

            // Resolver campo de nombre de barrio
            string? barrioNameField = null;
            if (neighborhoodsFc != null)
                barrioNameField = ResolveNeighborhoodNameField(neighborhoodsFc);

            var targetDef = targetFc.GetDefinition();
            if (targetDef.GetShapeType() != GeometryType.Polygon)
                return (false, "La Feature Class objetivo no es de tipo polígono.", 0);

            int successCount = 0;
            var errors = new List<string>();

            // Obtener definiciones reales de los campos para respetar longitud y tipos
            var fields = targetDef.GetFields();
            var idFieldDef = fields.FirstOrDefault(f => f.Name.Equals("identificador", StringComparison.OrdinalIgnoreCase));
            var barriosFieldDef = fields.FirstOrDefault(f => f.Name.Equals("barrios", StringComparison.OrdinalIgnoreCase));
            var clientesFieldDef = fields.FirstOrDefault(f => f.Name.Equals("clientes", StringComparison.OrdinalIgnoreCase));

            int idMaxLen = idFieldDef?.FieldType == FieldType.String ? idFieldDef.Length : 255;
            int barriosMaxLen = barriosFieldDef?.FieldType == FieldType.String ? barriosFieldDef.Length : 4096;
            bool clientesIsNumeric = clientesFieldDef?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;

            // Se realizarán cambios directos con row.Store() dentro del hilo de QueuedTask

            foreach (var feature in selectedFeatures)
            {
                try
                {
                    var geometry = feature.GetShape() as Polygon;
                    if (geometry == null || geometry.IsEmpty)
                    {
                        errors.Add($"Feature {feature.GetObjectID()}: geometría inválida o no es polígono");
                        continue;
                    }

                    string identifierValue = GetIdentifierValue(feature, identifierField);

                    // Buscar fila destino por OID
                    var oid = feature.GetObjectID();
                    var oidFld = targetDef.GetObjectIDField();
                    // Precalcular valores con truncamiento/compatibilidad de tipos
                    string idToWrite = SafeTruncate(identifierValue ?? string.Empty, idMaxLen);
                    string barriosToWrite = string.Empty;
                    object? clientesToWrite = null;

                    if (includeNeighborhoods && neighborhoodsFc != null && !string.IsNullOrWhiteSpace(barrioNameField))
                    {
                        try
                        {
                            var neighborhoods = GetNeighborhoodsForPolygon(neighborhoodsFc, barrioNameField, geometry);
                            barriosToWrite = SafeTruncate(neighborhoods ?? string.Empty, barriosMaxLen);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error barrios OID {oid}: {ex.Message}");
                        }
                    }

                    if (includeClients && clientsFc != null)
                    {
                        try
                        {
                            var clientsCount = GetClientsCountForPolygon(clientsFc, geometry);
                            clientesToWrite = clientesIsNumeric ? clientsCount : clientsCount.ToString();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error clientes OID {oid}: {ex.Message}");
                        }
                    }

                    using (Row? row = SearchRowByOid(targetFc, oidFld, oid))
                    {
                        if (row == null)
                        {
                            errors.Add($"OID {oid}: no encontrado en FC destino");
                        }
                        else
                        {
                            row["identificador"] = idToWrite;
                            if (includeNeighborhoods && barriosFieldDef != null)
                                row["barrios"] = barriosToWrite ?? string.Empty;
                            if (includeClients && clientesFieldDef != null)
                                row["clientes"] = clientesToWrite ?? (clientesIsNumeric ? 0 : "0");
                            row.Store();
                            successCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Feature {feature.GetObjectID()}: {ex.Message}");
                }
            }

            // No se requiere EditOperation; las ediciones se han hecho directamente

            // Refrescar capa
            UpdateOrAddLayerInMap(gdbPath, datasetName);

            string message = $"Se actualizaron {successCount} registro(s) en {datasetName}";
            if (errors.Any())
                message += $"\nErrores: {string.Join("; ", errors.Take(3))}";

            return (successCount > 0, message, successCount);
        }
        catch (Exception ex)
        {
            return (false, $"Error al actualizar la Feature Class: {ex.Message}", 0);
        }
    }

    private string GetIdentifierValue(Feature feature, string identifierField)
    {
        try
        {
            var def = feature.GetTable().GetDefinition();
            var field = def.GetFields().FirstOrDefault(f => f.Name.Equals(identifierField, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                var value = feature[field.Name]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch { }

        return $"FID_{feature.GetObjectID()}";
    }

    private (string gdbPath, string datasetName) ParseFeatureClassPath(string featureClassPath)
    {
        var idx = featureClassPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return (string.Empty, string.Empty);

        var gdbEnd = idx + 4;
        var gdbPath = featureClassPath.Substring(0, gdbEnd);
        var remainder = featureClassPath.Length > gdbEnd ? featureClassPath.Substring(gdbEnd).TrimStart('\\', '/') : string.Empty;
        if (string.IsNullOrWhiteSpace(remainder))
            return (gdbPath, string.Empty);

        var datasetName = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last();
        return (gdbPath, datasetName);
    }

    private void EnsureFieldExists(string gdbPath, string featureClassName, string fieldName, string fieldType, int length)
    {
        try
        {
            using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
            using var fc = gdb.OpenDataset<FeatureClass>(featureClassName);
            var exists = fc.GetDefinition().GetFields().Any(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            if (exists) return;
        }
        catch { }

        try
        {
            var fcPath = Path.Combine(gdbPath, featureClassName);
            if (string.Equals(fieldType, "TEXT", StringComparison.OrdinalIgnoreCase))
            {
                var addFieldParams = Geoprocessing.MakeValueArray(fcPath, fieldName, fieldType, "", "", length > 0 ? length : 255);
                Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams).GetAwaiter().GetResult();
            }
            else
            {
                var addFieldParams = Geoprocessing.MakeValueArray(fcPath, fieldName, fieldType);
                Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams).GetAwaiter().GetResult();
            }
        }
        catch { }
    }

    private FeatureClass? OpenFeatureClassFromPath(string featureClassPath)
    {
        try
        {
            var (gdbPath, datasetName) = ParseFeatureClassPath(featureClassPath);
            if (string.IsNullOrWhiteSpace(gdbPath) || string.IsNullOrWhiteSpace(datasetName)) return null;
            var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
            return gdb.OpenDataset<FeatureClass>(datasetName);
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveNeighborhoodNameField(FeatureClass neighborhoodsFc)
    {
        var def = neighborhoodsFc.GetDefinition();
        var nameCandidates = new[] { "NEIGHBORHOOD_DESC", "NEIGHBORHOOD", "BARRIO", "BARRIOS", "NOMBRE", "NOMBRE_BARRIO", "NAME", "DESCRIPCION" };
        return def.GetFields().Select(f => f.Name).FirstOrDefault(n => nameCandidates.Any(c => c.Equals(n, StringComparison.OrdinalIgnoreCase)));
    }

    private string GetNeighborhoodsForPolygon(FeatureClass neighborhoodsFc, string nameField, Polygon poly)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filter = new SpatialQueryFilter
        {
            WhereClause = "1=1",
            SubFields = nameField,
            SpatialRelationship = SpatialRelationship.Intersects,
            FilterGeometry = poly
        };
        using var cursor = neighborhoodsFc.Search(filter, false);
        while (cursor.MoveNext())
        {
            using var row = cursor.Current;
            var val = row[nameField]?.ToString();
            if (!string.IsNullOrWhiteSpace(val)) names.Add(val.Trim());
        }
        return string.Join(", ", names.OrderBy(n => n));
    }

    private int GetClientsCountForPolygon(FeatureClass clientsFc, Polygon poly)
    {
        int count = 0;
        var def = clientsFc.GetDefinition();
        var fields = def.GetFields();

        // Detectar campos candidatos
        string? tipoServicioFld = fields.Select(f => f.Name).FirstOrDefault(n =>
            new[] { "TIPOSERVICIOAC", "TIPO_SERVICIO", "TIPO_SERV", "TIPOSERVICIO", "SERVICE_TYPE" }
            .Any(c => c.Equals(n, StringComparison.OrdinalIgnoreCase)));

        string? clasificacionFld = fields.Select(f => f.Name).FirstOrDefault(n =>
            new[] { "DOMCLASIFICACIONPREDIO", "CLASIFICACION", "CLASIF", "CATEGORIA" }
            .Any(c => c.Equals(n, StringComparison.OrdinalIgnoreCase)));

        bool filtroTieneTipo = !string.IsNullOrWhiteSpace(tipoServicioFld);
        bool filtroTieneClas = !string.IsNullOrWhiteSpace(clasificacionFld);

        var tipoFieldDef = filtroTieneTipo ? fields.First(f => f.Name.Equals(tipoServicioFld, StringComparison.OrdinalIgnoreCase)) : null;
        var clasFieldDef = filtroTieneClas ? fields.First(f => f.Name.Equals(clasificacionFld, StringComparison.OrdinalIgnoreCase)) : null;

        bool tipoServicioEsNumerico = tipoFieldDef?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;
        bool clasificacionEsNumerico = clasFieldDef?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;

        // Construir SubFields para leer en memoria
        var neededFields = new List<string> { def.GetObjectIDField() };
        if (filtroTieneTipo && tipoFieldDef != null) neededFields.Add(tipoFieldDef.Name);
        if (filtroTieneClas && clasFieldDef != null) neededFields.Add(clasFieldDef.Name);
        string subFields = string.Join(",", neededFields.Distinct(StringComparer.OrdinalIgnoreCase));

        var filter = new SpatialQueryFilter
        {
            SpatialRelationship = SpatialRelationship.Intersects,
            FilterGeometry = poly,
            SubFields = subFields
        };

        using var cursor = clientsFc.Search(filter, true);
        while (cursor.MoveNext())
        {
            using var row = cursor.Current;

            bool pasaTipo = true;
            bool pasaClas = true;

            if (filtroTieneTipo && tipoFieldDef != null)
            {
                var val = row[tipoFieldDef.Name];
                if (val == null || val is DBNull)
                {
                    pasaTipo = false;
                }
                else if (tipoServicioEsNumerico)
                {
                    long? v = ConvertToLong(val);
                    pasaTipo = v.HasValue && v.Value == 10;
                }
                else
                {
                    var s = val.ToString()?.Trim();
                    pasaTipo = string.Equals(s, "10", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (filtroTieneClas && clasFieldDef != null)
            {
                var val = row[clasFieldDef.Name];
                if (val == null || val is DBNull)
                {
                    pasaClas = false;
                }
                else if (clasificacionEsNumerico)
                {
                    long? v = ConvertToLong(val);
                    pasaClas = v.HasValue && (v.Value == 1 || v.Value == 4 || v.Value == 6);
                }
                else
                {
                    var s = val.ToString()?.Trim();
                    pasaClas = s == "1" || s == "4" || s == "6";
                }
            }

            // Reglas: si no existe un campo, no se filtra por él; si existen ambos, deben cumplirse ambos
            if (pasaTipo && pasaClas)
                count++;
        }

        // Si no se encontraron ninguno de los campos de filtro, contar todos los puntos que intersectan
        if (!filtroTieneTipo && !filtroTieneClas)
        {
            count = 0;
            var f2 = new SpatialQueryFilter
            {
                SpatialRelationship = SpatialRelationship.Intersects,
                FilterGeometry = poly,
                SubFields = def.GetObjectIDField()
            };
            using var cur2 = clientsFc.Search(f2, true);
            while (cur2.MoveNext()) count++;
        }

        return count;
    }

    private static long? ConvertToLong(object? val)
    {
        try
        {
            if (val == null || val is DBNull) return null;
            if (val is long l) return l;
            if (val is int i) return i;
            if (val is short s) return s;
            if (long.TryParse(val.ToString(), out var parsed)) return parsed;
            return null;
        }
        catch { return null; }
    }

    private Row? SearchRowByOid(FeatureClass fc, string oidFieldName, long oid)
    {
        var qf = new QueryFilter { WhereClause = $"{oidFieldName} = {oid}", SubFields = "*" };
        using var cursor = fc.Search(qf, false);
        if (cursor.MoveNext()) return cursor.Current;
        return null;
    }

    private static string SafeTruncate(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (maxLen <= 0) return value;
        return value.Length <= maxLen ? value : value.Substring(0, maxLen);
    }

    private void UpdateOrAddLayerInMap(string gdbPath, string featureClassName)
    {
        try
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null) return;

            var existing = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(l => l.Name.Equals(featureClassName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.ClearDisplayCache();
                existing.SetVisibility(true);
                var ext = existing.QueryExtent();
                if (ext != null && !ext.IsEmpty)
                    mapView.ZoomTo(ext, new TimeSpan(0, 0, 0, 0, 600));
                return;
            }

            using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
            using var fc = gdb.OpenDataset<FeatureClass>(featureClassName);
            var layerParams = new FeatureLayerCreationParams(fc) { Name = featureClassName };
            var newLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, mapView.Map);
            if (newLayer != null)
            {
                var extent = newLayer.QueryExtent();
                if (extent != null && !extent.IsEmpty)
                    mapView.ZoomTo(extent, new TimeSpan(0, 0, 0, 0, 600));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al refrescar la capa: {ex.Message}");
        }
    }
}

