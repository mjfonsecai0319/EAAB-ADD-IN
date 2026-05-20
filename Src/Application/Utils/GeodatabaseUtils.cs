#nullable enable

namespace EAABAddIn.Src.Application.Utils;

using System;
using ArcGIS.Core.Data;

/// <summary>
/// Utility methods for opening and working with file geodatabases.
/// </summary>
public static class GeodatabaseUtils
{
    /// <summary>
    /// Opens a file geodatabase from the specified folder path.
    /// </summary>
    /// <param name="gdbFolderPath">The full path to the <c>.gdb</c> folder.</param>
    /// <returns>The <see cref="Geodatabase"/> if opened successfully; otherwise, <c>null</c>.</returns>
    public static Geodatabase? OpenGeodatabase(string gdbFolderPath)
    {
        try
        {
            var conn = new FileGeodatabaseConnectionPath(new Uri(gdbFolderPath));
            return new Geodatabase(conn);
        }
        catch
        {
            return null;
        }
    }
}