#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using EAABAddIn.Src.Application.Utils;
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

        // Open the feature class directly from the provided path
        using var fc = FeatureClassUtils.OpenFeatureClass(classPath);
        if (fc == null) return string.Empty;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var def = fc.GetDefinition();

            // try to find NEIGHBORHOOD_DESC field (case-insensitive)
            var nameField = def.GetFields().FirstOrDefault(f => f.Name.Equals("NEIGHBORHOOD_DESC", StringComparison.OrdinalIgnoreCase))?.Name;

            // if not found, fallback to first string field (excluding object id/shape)
            if (string.IsNullOrWhiteSpace(nameField))
            {
                var fallback = def.GetFields().FirstOrDefault(f => f.FieldType == FieldType.String && !f.Name.Equals(def.GetObjectIDField(), StringComparison.OrdinalIgnoreCase));
                nameField = fallback?.Name;
            }

            if (!string.IsNullOrWhiteSpace(nameField))
            {
                var filter = new SpatialQueryFilter
                {
                    FilterGeometry = geo,
                    SpatialRelationship = SpatialRelationship.Intersects,
                    WhereClause = "1=1",
                    SubFields = nameField
                };

                using var cursor = fc.Search(filter, false);
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
        }
        catch
        {
            // ignore errors and return what we have
        }

        var ordered = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return string.Join(", ", ordered);
    }


}



