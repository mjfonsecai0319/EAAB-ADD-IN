#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn.Src.Application.UseCases;

public class BuildAffectedAreaPolygonsUseCase
{
    private const string OUTPUT_FEATURE_CLASS = "AreaAfectada";

    public async Task<(bool success, string message, int polygonsCreated)> InvokeAsync(
        List<Feature> selectedFeatures,
        string workspace,
        string identifierField,
        string? neighborhoodsPath,
        string? clientsPath)
    {
        if (selectedFeatures == null || selectedFeatures.Count == 0)
            return (false, "No hay features seleccionadas", 0);

        if (string.IsNullOrWhiteSpace(workspace))
            workspace = Project.Current.DefaultGeodatabasePath;

        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
            return (false, "Workspace inválido", 0);

        return await QueuedTask.Run(() => GeneratePolygonsInternal(
            selectedFeatures,
            workspace,
            identifierField,
            neighborhoodsPath,
            clientsPath));
    }

    private (bool success, string message, int polygonsCreated) GeneratePolygonsInternal(
        List<Feature> selectedFeatures,
        string workspace,
        string identifierField,
        string? neighborhoodsPath,
        string? clientsPath)
    {
        try
        {
            bool includeNeighborhoods = !string.IsNullOrWhiteSpace(neighborhoodsPath);
            bool includeClients = !string.IsNullOrWhiteSpace(clientsPath);

            var firstFeature = selectedFeatures.First();
            var spatialRef = firstFeature.GetShape()?.SpatialReference;
            if (spatialRef == null)
                return (false, "No se pudo obtener la referencia espacial", 0);

            var gdbConn = new FileGeodatabaseConnectionPath(new Uri(workspace));
            using var gdb = new Geodatabase(gdbConn);

            EnsureOutputFeatureClass(workspace, gdb, spatialRef);
            
            EnsureFieldExists(workspace, OUTPUT_FEATURE_CLASS, "identificador", "TEXT", 255);
            
            if (includeNeighborhoods)
                EnsureFieldExists(workspace, OUTPUT_FEATURE_CLASS, "barrios", "TEXT", 4096);
            
            if (includeClients)
                EnsureFieldExists(workspace, OUTPUT_FEATURE_CLASS, "clientes_afectados", "LONG", 0);

            using var neighborhoodsFc = includeNeighborhoods ? OpenFeatureClassFromPath(neighborhoodsPath!) : null;
            using var clientsFc = includeClients ? OpenFeatureClassFromPath(clientsPath!) : null;

            string? barrioNameField = null;
            if (neighborhoodsFc != null)
            {
                barrioNameField = ResolveNeighborhoodNameField(neighborhoodsFc);
            }

            using var outputFc = gdb.OpenDataset<FeatureClass>(OUTPUT_FEATURE_CLASS);
            var outputDef = outputFc.GetDefinition();
            
            DeleteAllFeatures(outputFc);

            int successCount = 0;
            var errors = new List<string>();

            foreach (var feature in selectedFeatures)
            {
                try
                {
                    var geometry = feature.GetShape() as Polygon;
                    if (geometry == null || geometry.IsEmpty)
                    {
                        errors.Add($"Feature {feature.GetObjectID()}: geometría inválida");
                        continue;
                    }

                    string identifierValue = GetIdentifierValue(feature, identifierField);

                    using var rowBuffer = outputFc.CreateRowBuffer();
                    rowBuffer[outputDef.GetShapeField()] = geometry;
                    rowBuffer["identificador"] = identifierValue;

                    if (includeNeighborhoods && neighborhoodsFc != null && !string.IsNullOrWhiteSpace(barrioNameField))
                    {
                        try
                        {
                            var neighborhoods = GetNeighborhoodsForPolygon(neighborhoodsFc, barrioNameField, geometry);
                            if (!string.IsNullOrWhiteSpace(neighborhoods))
                            {
                                if (neighborhoods.Length > 4096)
                                    neighborhoods = neighborhoods.Substring(0, 4095);
                                
                                rowBuffer["barrios"] = neighborhoods;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error obteniendo barrios para {identifierValue}: {ex.Message}");
                        }
                    }

                    if (includeClients && clientsFc != null)
                    {
                        try
                        {
                            var clientsCount = GetClientsCountForPolygon(clientsFc, geometry);
                            rowBuffer["clientes_afectados"] = clientsCount;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error obteniendo clientes para {identifierValue}: {ex.Message}");
                        }
                    }

                    using var newRow = outputFc.CreateRow(rowBuffer);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Feature {feature.GetObjectID()}: {ex.Message}");
                }
            }

            AddLayerToMap(workspace, OUTPUT_FEATURE_CLASS);

            string message = $"Se crearon {successCount} polígono(s) de área afectada";
            if (errors.Any())
                message += $"\nErrores: {string.Join("; ", errors.Take(3))}";

            return (successCount > 0, message, successCount);
        }
        catch (Exception ex)
        {
            return (false, $"Error al generar polígonos: {ex.Message}", 0);
        }
    }

    private string GetIdentifierValue(Feature feature, string identifierField)
    {
        try
        {
            var def = feature.GetTable().GetDefinition();
            var field = def.GetFields().FirstOrDefault(f => 
                f.Name.Equals(identifierField, StringComparison.OrdinalIgnoreCase));
            
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

    private void EnsureOutputFeatureClass(string gdbPath, Geodatabase gdb, SpatialReference sr)
    {
        var exists = gdb.GetDefinitions<FeatureClassDefinition>()
            .Any(d => d.GetName().Equals(OUTPUT_FEATURE_CLASS, StringComparison.OrdinalIgnoreCase));
        
        if (exists)
        {
            try
            {
                var parameters = Geoprocessing.MakeValueArray(Path.Combine(gdbPath, OUTPUT_FEATURE_CLASS));
                Geoprocessing.ExecuteToolAsync("management.Delete", parameters, null, 
                    CancelableProgressor.None, GPExecuteToolFlags.None).GetAwaiter().GetResult();
            }
            catch { }
        }

        // Crear feature class
        var createParams = Geoprocessing.MakeValueArray(
            gdbPath,
            OUTPUT_FEATURE_CLASS,
            "POLYGON",
            "",
            "DISABLED",
            "DISABLED",
            sr.Wkid > 0 ? sr.Wkid : 4326
        );
        
        Geoprocessing.ExecuteToolAsync("management.CreateFeatureclass", createParams, null, 
            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory).GetAwaiter().GetResult();
    }

    private void EnsureFieldExists(string gdbPath, string featureClassName, string fieldName, string fieldType, int length)
    {
        try
        {
            var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new Geodatabase(gdbConn);
            using var fc = gdb.OpenDataset<FeatureClass>(featureClassName);
            
            var exists = fc.GetDefinition().GetFields()
                .Any(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            
            if (exists) return;
        }
        catch { }

        try
        {
            var fcPath = Path.Combine(gdbPath, featureClassName);
            
            if (string.Equals(fieldType, "TEXT", StringComparison.OrdinalIgnoreCase))
            {
                var addFieldParams = Geoprocessing.MakeValueArray(
                    fcPath, fieldName, fieldType, "", "", length > 0 ? length : 255);
                Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams, null, 
                    CancelableProgressor.None, GPExecuteToolFlags.None).GetAwaiter().GetResult();
            }
            else
            {
                var addFieldParams = Geoprocessing.MakeValueArray(fcPath, fieldName, fieldType);
                Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams, null, 
                    CancelableProgressor.None, GPExecuteToolFlags.None).GetAwaiter().GetResult();
            }
        }
        catch { }
    }

    private FeatureClass? OpenFeatureClassFromPath(string featureClassPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(featureClassPath)) return null;
            
            var idx = featureClassPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            
            var gdbEnd = idx + 4;
            var gdbPath = featureClassPath.Substring(0, gdbEnd);
            var remainder = featureClassPath.Length > gdbEnd 
                ? featureClassPath.Substring(gdbEnd).TrimStart('\\', '/') 
                : string.Empty;
            
            if (string.IsNullOrWhiteSpace(remainder)) return null;
            
            var datasetName = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last();
            
            var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            var gdb = new Geodatabase(gdbConn);
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
        var nameCandidates = new[] 
        { 
            "NEIGHBORHOOD_DESC", "NEIGHBORHOOD", "BARRIO", "BARRIOS", 
            "NOMBRE", "NOMBRE_BARRIO", "NAME", "DESCRIPCION" 
        };
        
        return def.GetFields()
            .Select(f => f.Name)
            .FirstOrDefault(n => nameCandidates.Any(c => 
                c.Equals(n, StringComparison.OrdinalIgnoreCase)));
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
            if (!string.IsNullOrWhiteSpace(val))
                names.Add(val.Trim());
        }
        
        return string.Join(", ", names.OrderBy(n => n));
    }

    private int GetClientsCountForPolygon(FeatureClass clientsFc, Polygon poly)
    {
        int count = 0;
        
        var def = clientsFc.GetDefinition();
        var fields = def.GetFields();
        string oidFld = def.GetObjectIDField();
        
        string tipoServicioFld = fields.FirstOrDefault(f => 
            f.Name.Equals("TIPOSERVICIOAC", StringComparison.OrdinalIgnoreCase))?.Name ?? "TIPOSERVICIOAC";
        string clasificacionFld = fields.FirstOrDefault(f => 
            f.Name.Equals("DOMCLASIFICACIONPREDIO", StringComparison.OrdinalIgnoreCase))?.Name ?? "DOMCLASIFICACIONPREDIO";
        
        bool tipoServicioEsNumerico = fields.FirstOrDefault(f => 
            f.Name.Equals(tipoServicioFld, StringComparison.OrdinalIgnoreCase))?.FieldType 
            is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;
        
        bool clasificacionEsNumerico = fields.FirstOrDefault(f => 
            f.Name.Equals(clasificacionFld, StringComparison.OrdinalIgnoreCase))?.FieldType 
            is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;
        
        string whereTipo = tipoServicioEsNumerico 
            ? $"{tipoServicioFld} = 10" 
            : $"{tipoServicioFld} = '10'";
        
        string whereClasif = clasificacionEsNumerico
            ? $"{clasificacionFld} IN (1,4,6)"
            : $"{clasificacionFld} IN ('1','4','6')";
        
        var filter = new SpatialQueryFilter
        {
            SpatialRelationship = SpatialRelationship.Intersects,
            FilterGeometry = poly,
            SubFields = oidFld,
            WhereClause = $"{whereTipo} AND {whereClasif}"
        };
        
        using var cursor = clientsFc.Search(filter, true);
        while (cursor.MoveNext())
        {
            count++;
        }
        
        return count;
    }

    private void DeleteAllFeatures(FeatureClass fc)
    {
        var oidField = fc.GetDefinition().GetObjectIDField();
        var toDelete = new List<Row>();
        
        using (var cursor = fc.Search(new QueryFilter { SubFields = oidField }, false))
        {
            while (cursor.MoveNext())
            {
                if (cursor.Current is Row row)
                    toDelete.Add(row);
            }
        }
        
        foreach (var row in toDelete)
        {
            try { row.Delete(); } 
            catch { }
            row.Dispose();
        }
    }

    private void AddLayerToMap(string gdbPath, string featureClassName)
    {
        try
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null) return;
            
            var existing = mapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .FirstOrDefault(l => l.Name.Equals(featureClassName, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
                mapView.Map.RemoveLayer(existing);
            
            var gdbConn = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using var gdb = new Geodatabase(gdbConn);
            using var fc = gdb.OpenDataset<FeatureClass>(featureClassName);
            
            var layerParams = new FeatureLayerCreationParams(fc) { Name = featureClassName };
            var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, mapView.Map);
            
            if (layer != null)
            {
                var extent = layer.QueryExtent();
                if (extent != null && !extent.IsEmpty)
                    mapView.ZoomTo(extent, new TimeSpan(0, 0, 0, 0, 600));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error agregando capa al mapa: {ex.Message}");
        }
    }
}
