#nullable enable

using System;
using System.Linq;
using ArcGIS.Core.Data;

namespace EAABAddIn.Src.Application.Utils;

public static class FeatureClassUtils
{
    /// <summary>
    /// Open a FeatureClass from a feature class path of the form '...\.gdb\\DatasetName' or '...\\DatasetName'.
    /// Returns null on failure.
    /// </summary>
    public static FeatureClass? OpenFeatureClass(string featureClassPath)
    {
        if (string.IsNullOrWhiteSpace(featureClassPath))
            return null;

        var idx = featureClassPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        try
        {
            var gdbEnd = idx + 4;
            var gdbPath = featureClassPath.Substring(0, gdbEnd);
            var remainder = featureClassPath.Length > gdbEnd ? featureClassPath.Substring(gdbEnd).TrimStart('\\', '/') : string.Empty;
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
}
