#nullable enable

namespace EAABAddIn.Src.Application.Utils;

using System;
using System.IO;

using ArcGIS.Core.Data;

public static class FeatureClassUtils
{
    /// <summary>
    /// Opens a <see cref="FeatureClass"/> from the specified path.
    /// Supports shapefiles (<c>.shp</c>) and file geodatabase paths
    /// (e.g. <c>C:\data.gdb\FeatureClassName</c> or <c>C:\data.gdb\Dataset\FeatureClassName</c>).
    /// </summary>
    /// <param name="path">The full path to the feature class.</param>
    /// <returns>The <see cref="FeatureClass"/> if opened successfully; otherwise, <c>null</c>.</returns>
    public static FeatureClass? TryOpenFeatureClass(string path)
    {
        try
        {
            path = Path.GetFullPath(path);

            if (path.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
            {
                return TryOpenShapefile(path);
            }

            int gdbIndex = path.LastIndexOf(".gdb", StringComparison.OrdinalIgnoreCase);

            if (gdbIndex < 0)
            {
                return null;
            }

            string gdbPath = path[..(gdbIndex + 4)];

            if (path.Length <= gdbIndex + 5)
            {
                return null;
            }

            string fcName = path[(gdbIndex + 5)..].TrimStart('\\', '/');

            if (fcName.Length == 0)
            {
                return null;
            }

            return TryOpenFromGeodatabase(gdbPath, fcName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Opens a shapefile <see cref="FeatureClass"/> from disk.
    /// </summary>
    public static FeatureClass? TryOpenShapefile(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder))
            return null;

        string name = Path.GetFileNameWithoutExtension(path);
        var conn = new FileSystemConnectionPath(
            new Uri(folder),
            FileSystemDatastoreType.Shapefile);
        var datastore = new FileSystemDatastore(conn);
        return datastore.OpenDataset<FeatureClass>(name);
    }

    /// <summary>
    /// Opens a feature class from a file geodatabase by path, searching direct and
    /// feature-dataset locations.
    /// </summary>
    public static FeatureClass? TryOpenFromGeodatabase(string gdbPath, string fcName)
    {
        using Geodatabase? gdb = GeodatabaseUtils.OpenGeodatabase(gdbPath);
        return gdb is null ? null : TryOpenFromGeodatabase(gdb, fcName);
    }

    /// <summary>
    /// Opens a feature class from an already-open <see cref="Geodatabase"/>,
    /// searching direct and feature-dataset locations.
    /// </summary>
    public static FeatureClass? TryOpenFromGeodatabase(Geodatabase gdb, string fcName)
    {
        // 1) Try direct open
        FeatureClass? fc = TryOpenDataset<FeatureClass>(gdb, fcName);
        if (fc != null)
            return fc;

        // 2) Parse dataset / feature-class parts
        string[] parts = fcName.Split(
            new[] { '\\', '/' },
            StringSplitOptions.RemoveEmptyEntries);
        string className = parts.Length > 0 ? parts[^1] : fcName;

        // 3) If the path explicitly contains a dataset name, try it first
        if (parts.Length >= 2)
        {
            fc = TryOpenFromFeatureDataset(gdb, parts[0], className);
            if (fc != null)
                return fc;
        }

        // 4) Fallback: scan every feature dataset
        return TryOpenFromAllDatasets(gdb, className);
    }

    /// <summary>
    /// Safely attempts <c>gdb.OpenDataset&lt;T&gt;(name)</c>, silencing exceptions.
    /// </summary>
    public static T? TryOpenDataset<T>(Geodatabase gdb, string name) where T : Dataset
    {
        try
        {
            return gdb.OpenDataset<T>(name);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Opens a feature class nested inside a named feature dataset.
    /// </summary>
    public static FeatureClass? TryOpenFromFeatureDataset(
        Geodatabase gdb,
        string datasetName,
        string className)
    {
        try
        {
            var fd = gdb.OpenDataset<FeatureDataset>(datasetName);
            return fd.OpenDataset<FeatureClass>(className);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Iterates all feature datasets in the geodatabase looking for a
    /// feature class with the given name.
    /// </summary>
    public static FeatureClass? TryOpenFromAllDatasets(
        Geodatabase gdb,
        string className)
    {
        foreach (var fdDef in gdb.GetDefinitions<FeatureDatasetDefinition>())
        {
            FeatureClass? fc = TryOpenFromFeatureDataset(
                gdb,
                fdDef.GetName(),
                className);
            if (fc != null)
                return fc;
        }

        return null;
    }

    /// <summary>
    /// Reads a field value from a <see cref="Feature"/> and converts it to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type (e.g. <c>string</c>, <c>int?</c>, <c>double?</c>, <c>DateTime?</c>).</typeparam>
    /// <param name="feature">The feature to read from.</param>
    /// <param name="fieldName">The name of the field to read.</param>
    /// <returns>The field value converted to <typeparamref name="T"/>, or <c>default</c> if the field is not found, is null, or conversion fails.</returns>
    public static T? GetFieldValue<T>(Feature feature, string fieldName)
    {
        try
        {
            int index = feature.FindField(fieldName);

            if (index < 0)
            {
                return default;
            }

            object? value = feature[index];

            if (value is null or DBNull)
            {
                return default;
            }

            if (typeof(T) == typeof(string))
            {
                string text = value.ToString() ?? string.Empty;
                return (T)(object)text;
            }

            Type targetType = typeof(T);

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Type underlyingType = Nullable.GetUnderlyingType(targetType)!;
                return (T)Convert.ChangeType(value, underlyingType);
            }

            return (T)Convert.ChangeType(value, targetType);
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
/// Extension methods for <see cref="Feature"/>.
/// </summary>
public static class FeatureExtensions
{
    /// <summary>
    /// Reads a field value from a <see cref="Feature"/> and converts it to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type (e.g. <c>string</c>, <c>int?</c>, <c>double?</c>, <c>DateTime?</c>).</typeparam>
    /// <param name="feature">The feature to read from.</param>
    /// <param name="fieldName">The name of the field to read.</param>
    /// <returns>The field value converted to <typeparamref name="T"/>, or <c>default</c> if not found, null, or conversion fails.</returns>
    public static T? GetFieldValue<T>(this Feature feature, string fieldName)
    {
        return FeatureClassUtils.GetFieldValue<T>(feature, fieldName);
    }
}
