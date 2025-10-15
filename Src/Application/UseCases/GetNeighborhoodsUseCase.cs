#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn.Src.Application.UseCases;

public class GetNeighborhoodsUseCase
{
    /// <summary>
    /// Given a feature and a feature-class path (dataset path), finds features in the map layer(s)
    /// matching the dataset name that intersect the input feature and returns a CSV of neighborhood names.
    /// It attempts to read the field "NEIGHBORHOOD_DESC" (case-insensitive) and falls back to the first string field.
    /// </summary>
    public async Task<string> Invoke(Feature feature, string classPath)
    {
        if (feature == null || string.IsNullOrWhiteSpace(classPath))
            return string.Empty;

        // Do geometry and table access on the QueuedTask
        var result = await QueuedTask.Run(() => InvokeInternal(feature, classPath));
        return result ?? string.Empty;
    }

    private string? InvokeInternal(Feature feature, string classPath)
    {
        var map = MapView.Active?.Map;
        if (map == null)
            return string.Empty;

        var geo = feature.GetShape() as Geometry;
        if (geo == null)
            return string.Empty;

        var datasetName = GetDatasetNameFromPath(classPath);
        if (string.IsNullOrWhiteSpace(datasetName))
            return string.Empty;

        var layers = map.FindLayers(datasetName).OfType<FeatureLayer>().ToList();
        if (layers.Count == 0)
            return string.Empty;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var filter = new SpatialQueryFilter
        {
            FilterGeometry = geo,
            SpatialRelationship = SpatialRelationship.Intersects,
            WhereClause = "1=1"
        };

        foreach (var fl in layers)
        {
            try
            {
                var table = fl.GetTable();
                var def = table.GetDefinition();

                // try to find NEIGHBORHOOD_DESC field (case-insensitive)
                var nameField = def.GetFields().FirstOrDefault(f => f.Name.Equals("NEIGHBORHOOD_DESC", StringComparison.OrdinalIgnoreCase))?.Name;

                // if not found, fallback to first string field (excluding object id/shape)
                if (string.IsNullOrWhiteSpace(nameField))
                {
                    var fallback = def.GetFields().FirstOrDefault(f => f.FieldType == FieldType.String && !f.Name.Equals(def.GetObjectIDField(), StringComparison.OrdinalIgnoreCase));
                    nameField = fallback?.Name;
                }

                // if still no name field, skip
                if (string.IsNullOrWhiteSpace(nameField))
                    continue;

                filter.SubFields = nameField;

                using var cursor = table.Search(filter, false);
                while (cursor.MoveNext())
                {
                    using var row = cursor.Current;
                    try
                    {
                        var val = row[nameField]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val))
                            names.Add(val.Trim());
                    }
                    catch
                    {
                        // ignore problematic rows
                    }
                }
            }
            catch
            {
                // ignore layer errors and continue with others
            }
        }

        var ordered = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return string.Join(", ", ordered);
    }

    private static string GetDatasetNameFromPath(string featureClassPath)
    {
        if (string.IsNullOrWhiteSpace(featureClassPath))
        {
            return featureClassPath;
        }

        var idx = featureClassPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);

        if (idx >= 0)
        {
            var gdbEnd = idx + 4;
            var remainder = featureClassPath.Length > gdbEnd ? featureClassPath.Substring(gdbEnd).TrimStart('\\', '/') : string.Empty;
            if (string.IsNullOrWhiteSpace(remainder))
            {
                return System.IO.Path.GetFileNameWithoutExtension(featureClassPath);
            }

            var parts = remainder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Last();
        }

        return System.IO.Path.GetFileName(featureClassPath);
    }
}


// private static string GetNeighborhoodsForPolygon(FeatureClass neighborhoodsFc, string nameField, Polygon poly)
//     {
//         var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
//         var filter = new SpatialQueryFilter
//         {
//             WhereClause = "1=1",
//             SubFields = nameField,
//             SpatialRelationship = SpatialRelationship.Intersects,
//             FilterGeometry = poly
//         };
//         using var cursor = neighborhoodsFc.Search(filter, false);
//         while (cursor.MoveNext())
//         {
//             using var row = cursor.Current;
//             var val = row[nameField]?.ToString();
//             if (!string.IsNullOrWhiteSpace(val)) names.Add(val.Trim());
//         }
//         return string.Join(", ", names.OrderBy(n => n));
//     }

//     private static int GetClientsCountForPolygon(FeatureClass clientsFc, Polygon poly)
//     {
//         int count = 0;

//         var def = clientsFc.GetDefinition();
//         var fields = def.GetFields();
//         string oidFld = def.GetObjectIDField();

//         string tipoServicioFld = fields.FirstOrDefault(f => f.Name.Equals("TIPOSERVICIOAC", StringComparison.OrdinalIgnoreCase))?.Name ?? "TIPOSERVICIOAC";
//         string clasificacionFld = fields.FirstOrDefault(f => f.Name.Equals("DOMCLASIFICACIONPREDIO", StringComparison.OrdinalIgnoreCase))?.Name ?? "DOMCLASIFICACIONPREDIO";

//         bool tipoServicioEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(tipoServicioFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;
//         bool clasificacionEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(clasificacionFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;

//         string whereTipo = tipoServicioEsNumerico ? $"{tipoServicioFld} = 10" : $"{tipoServicioFld} = '10'";
//         string whereClasif;
//         if (clasificacionEsNumerico)
//             whereClasif = $"{clasificacionFld} IN (1,4,6)";
//         else
//             whereClasif = $"{clasificacionFld} IN ('1','4','6')";

//         var filter = new SpatialQueryFilter
//         {
//             SpatialRelationship = SpatialRelationship.Intersects,
//             FilterGeometry = poly,
//             SubFields = oidFld,
//             WhereClause = $"{whereTipo} AND {whereClasif}"
//         };

//         using var cursor = clientsFc.Search(filter, true);
//         while (cursor.MoveNext())
//         {
//             count++;
//         }
//         return count;
//     }