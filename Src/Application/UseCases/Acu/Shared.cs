#nullable enable

namespace EAABAddIn.Src.Application.UseCases.Acu;

using System;
using System.Collections.Generic;
using System.Linq;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Application.Utils;

internal static class Shared
{
    internal static readonly IEqualityComparer<string> StringComparer = System.StringComparer.OrdinalIgnoreCase;

    internal static bool FeatureClassExists(Geodatabase gdb, string className)
    {
        try
        {
            using var fc = FeatureClassUtils.TryOpenFromGeodatabase(gdb, className);
            return fc is not null;
        }
        catch
        {
            return false;
        }
    }

    internal static object? CoerceToFieldType(object? value, Field fieldDef)
    {
        if (value is null or DBNull)
        {
            return DBNull.Value;
        }

        try
        {
            return fieldDef.FieldType switch
            {
                FieldType.String => CoerceString(value, fieldDef.Length),
                FieldType.Integer => Convert.ChangeType(value, typeof(int)),
                FieldType.SmallInteger => Convert.ChangeType(value, typeof(short)),
                FieldType.Double => Convert.ChangeType(value, typeof(double)),
                FieldType.Single => Convert.ChangeType(value, typeof(float)),
                FieldType.Date => Convert.ChangeType(value, typeof(DateTime)),
                FieldType.GUID or FieldType.GlobalID => value.ToString(),
                FieldType.OID => DBNull.Value,
                _ => value
            };
        }
        catch
        {
            return DBNull.Value;
        }
    }

    internal static (bool ok, string? error) TryInsertRowDirect(
        Geodatabase targetGdb,
        FeatureClass targetFC,
        Dictionary<string, object?> dict)
    {
        try
        {
            using var def = targetFC.GetDefinition();
            var fieldMap = def.GetFields().ToDictionary(f => f.Name, f => f, StringComparer);

            string shapeField = def.GetShapeField();
            if (dict.TryGetValue(shapeField, out var geomValue))
            {
                if (geomValue is null || (geomValue is Geometry geom && geom.IsEmpty))
                {
                    return (false, "Geometria nula o vacia al insertar");
                }

                if (geomValue is Geometry sourceGeom)
                {
                    var targetSR = def.GetSpatialReference();
                    var sourceSR = sourceGeom.SpatialReference;
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
                            return (false, $"Error proyectando geometria: {exProj.Message}");
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
                            Geometry adjustedGeom = StripZAndM(sourceGeom);
                            dict[shapeField] = adjustedGeom;
                        }
                        catch (Exception ex)
                        {
                            return (false, $"Error ajustando geometria Z/M: {ex.Message}");
                        }
                    }
                }
            }

            targetGdb.ApplyEdits(() =>
            {
                using var rowBuffer = targetFC.CreateRowBuffer();
                foreach (var kv in dict)
                {
                    if (!fieldMap.TryGetValue(kv.Key, out var fieldDef))
                    {
                        continue;
                    }

                    if (fieldDef.FieldType == FieldType.OID || fieldDef.FieldType == FieldType.GlobalID)
                    {
                        continue;
                    }

                    rowBuffer[kv.Key] = kv.Value ?? DBNull.Value;
                }

                using var row = targetFC.CreateRow(rowBuffer);
                row.Store();
            });

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    internal static void EnsureLayerForTargetClass(Map map, Geodatabase gdb, string className, bool isLine)
    {
        try
        {
            var existingLayer = map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
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
                catch
                {
                }
            }

            using var fc = FeatureClassUtils.TryOpenFromGeodatabase(gdb, className);
            if (fc == null)
            {
                return;
            }

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
        catch
        {
        }
    }

    private static string CoerceString(object value, int maxLen)
    {
        string text = (value.ToString() ?? string.Empty).Trim();
        if (maxLen > 0 && text.Length > maxLen)
        {
            text = text.Substring(0, maxLen);
        }

        return text;
    }

    private static Geometry StripZAndM(Geometry sourceGeom)
    {
        return sourceGeom switch
        {
            MapPoint point => MapPointBuilderEx.CreateMapPoint(point.X, point.Y, sourceGeom.SpatialReference),
            Polyline line => BuildPolylineWithoutZ(line),
            Polygon poly => BuildPolygonWithoutZ(poly),
            _ => sourceGeom
        };
    }

    private static Polyline BuildPolylineWithoutZ(Polyline line)
    {
        var builder = new PolylineBuilderEx(line.SpatialReference);
        foreach (var part in line.Parts)
        {
            var points = new List<MapPoint>();
            MapPoint? lastEnd = null;
            foreach (var segment in part)
            {
                var sp = segment.StartPoint;
                points.Add(MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, line.SpatialReference));
                lastEnd = segment.EndPoint;
            }
            if (lastEnd != null)
            {
                points.Add(MapPointBuilderEx.CreateMapPoint(lastEnd.X, lastEnd.Y, line.SpatialReference));
            }
            if (points.Count > 0)
            {
                builder.AddPart(points);
            }
        }
        return builder.ToGeometry();
    }

    private static Polygon BuildPolygonWithoutZ(Polygon poly)
    {
        var builder = new PolygonBuilderEx(poly.SpatialReference);
        foreach (var part in poly.Parts)
        {
            var points = new List<MapPoint>();
            MapPoint? lastEnd = null;
            foreach (var segment in part)
            {
                var sp = segment.StartPoint;
                points.Add(MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, poly.SpatialReference));
                lastEnd = segment.EndPoint;
            }
            if (lastEnd != null)
            {
                points.Add(MapPointBuilderEx.CreateMapPoint(lastEnd.X, lastEnd.Y, poly.SpatialReference));
            }
            if (points.Count > 0)
            {
                builder.AddPart(points);
            }
        }
        return builder.ToGeometry();
    }

    internal static void ApplySymbology(FeatureLayer layer, bool isLine)
    {
        try
        {
            if (isLine)
            {
                var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                    ColorFactory.Instance.CreateRGBColor(0, 112, 255),
                    1.2,
                    SimpleLineStyle.Solid);

                var rendererDef = new SimpleRendererDefinition
                {
                    SymbolTemplate = lineSymbol.MakeSymbolReference()
                };

                var renderer = layer.CreateRenderer(rendererDef);
                layer.SetRenderer(renderer);
            }
            else
            {
                var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                    ColorFactory.Instance.CreateRGBColor(255, 0, 0),
                    4,
                    SimpleMarkerStyle.Circle);

                var rendererDef = new SimpleRendererDefinition
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

    internal static void EnsureLayerIsVisibleAndSelectable(FeatureLayer layer, FeatureClass fc)
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
            catch
            {
            }

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
                    var qf = new QueryFilter { WhereClause = where };
                    layer.Select(qf, SelectionCombinationMethod.New);
                }
            }
            catch
            {
            }
        }
        catch
        {
        }
    }
}
