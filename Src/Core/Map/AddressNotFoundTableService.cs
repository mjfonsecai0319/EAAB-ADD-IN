using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Core.Map
{
    public static class AddressNotFoundTableService
    {
        private const string TableName = "GeocodeNotFound";

        public static Task AddRecordAsync(AddressNotFoundRecord record)
        {
            return QueuedTask.Run(() => _AddRecordAsync(record));
        }

        private static async Task _AddRecordAsync(AddressNotFoundRecord record)
        {
            var gdbPath = Project.Current.DefaultGeodatabasePath;
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(Path.GetDirectoryName(gdbPath)))
                return;

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            // Crear tabla si no existe
            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            {
                if (geodatabase.GetDefinitions<TableDefinition>().All(d => d.GetName() != TableName))
                {
                    var createTableParams = Geoprocessing.MakeValueArray(
                        gdbPath,
                        TableName
                    );
                    var result = await Geoprocessing.ExecuteToolAsync("management.CreateTable", createTableParams);
                    if (result.IsFailed) return;

                    // Agregar campos requeridos
                    await EnsureFieldsExist(gdbPath);
                }
            }

            var attributes = new Dictionary<string, object>
            {
                { "Identificador", record.Identificador },
                { "Direccion", record.Direccion },
                { "Poblacion", record.Poblacion },
                { "full_address_eaab", record.FullAddressEaab },
                { "full_address_uacd", record.FullAddressUacd },
                { "Geocoder", record.Geocoder },
                { "Score", record.Score ?? (object)DBNull.Value }
            };

            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            using (var table = geodatabase.OpenDataset<Table>(TableName))
            {
                var op = new EditOperation { Name = "Insert NotFound Record" };
                op.Create(table, attributes);
                await op.ExecuteAsync();
            }
        }

        private static async Task EnsureFieldsExist(string gdbPath)
        {
            var fields = new[]
            {
                new { Name = "Identificador", Type = "TEXT", Length = "100" },
                new { Name = "Direccion", Type = "TEXT", Length = "255" },
                new { Name = "Poblacion", Type = "TEXT", Length = "100" },
                new { Name = "full_address_eaab", Type = "TEXT", Length = "255" },
                new { Name = "full_address_uacd", Type = "TEXT", Length = "255" },
                new { Name = "Geocoder", Type = "TEXT", Length = "100" },
                new { Name = "Score", Type = "DOUBLE", Length = "" }
            };

            var tablePath = Path.Combine(gdbPath, TableName);

            foreach (var field in fields)
            {
                var addFieldParams = Geoprocessing.MakeValueArray(
                    tablePath,
                    field.Name,
                    field.Type,
                    "",
                    "",
                    field.Length
                );

                await Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams,
                    null, CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            }
        }
    }
}
