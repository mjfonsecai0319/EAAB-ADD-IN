using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Core.Map
{
    public static class AddressNotFoundTableService
    {
        private const string TableName = "GeocodeNotFound";

        private static StandaloneTable _notFoundTable;

        public static StandaloneTable NotFoundTable
        {
            get => _notFoundTable;
            private set => _notFoundTable = value;
        }

        public static (string Name, string Type, string Length)[] TableFields =>
        [
            ("Identificador", "TEXT", "100"),
            ("Direccion", "TEXT", "255"),
            ("Poblacion", "TEXT", "100"),
            ("full_address_eaab", "TEXT", "255"),
            ("full_address_uacd", "TEXT", "255"),
            ("Geocoder", "TEXT", "100"),
            ("FechaHora", "DATE", "")
        ];

        public static Task AddRecordAsync(AddressNotFoundRecord record, string gdbPath = null)
        {
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(gdbPath))
            {
                gdbPath = Project.Current.DefaultGeodatabasePath;
            }

            return QueuedTask.Run(() => _AddRecordAsync(record, gdbPath));
        }

        private static async Task _AddRecordAsync(AddressNotFoundRecord record, string gdbPath)
        {
            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            using (var gdb = new Geodatabase(gdbConnectionPath))
            {
                var exists = TableExists(gdb, TableName);

                if (!exists)
                {
                    var result = await Geoprocessing.ExecuteToolAsync(
                        toolPath: "management.CreateTable",
                        values: Geoprocessing.MakeValueArray(gdbPath, TableName),
                        environments: null,
                        progressor: CancelableProgressor.None,
                        flags: GPExecuteToolFlags.AddToHistory
                    );

                    System.Diagnostics.Debug.WriteLine($"CreateTable result: {result.IsFailed}, {result.ReturnValue}");
                    if (result.IsFailed) return;
                }

                await EnsureFieldsExist(gdb);

                AddNotFoundTable(gdb);

                using (var table = gdb.OpenDataset<Table>(TableName))
                {
                    InsertRecord(table, record);
                }
            }

        }

        private static bool TableExists(Geodatabase geodatabase, string tableName)
        {
            return geodatabase.GetDefinitions<TableDefinition>().Any(d => string.Equals(d.GetName(), tableName, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddNotFoundTable(Geodatabase gdb)
        {
            var mapView = MapView.Active;

            if (mapView?.Map == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: "No se pudo agregar la tabla de direcciones no encontradas al mapa activo porque no hay ningún mapa abierto.",
                    caption: "EAAB Add-In",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Error
                );
                return;
            }

            if (NotFoundTable is null)
            {
                using (var table = gdb.OpenDataset<Table>(TableName))
                {
                    var stParams = new StandaloneTableCreationParams(table)
                    {
                        Name = TableName
                    };
                    NotFoundTable = StandaloneTableFactory.Instance.CreateStandaloneTable(stParams, mapView.Map);
                }
                return;
            }

            var existing = mapView.Map.StandaloneTables.FirstOrDefault(
                st => string.Equals(st.Name, TableName, StringComparison.OrdinalIgnoreCase)
            );

            if (existing is not null)
            {
                NotFoundTable = existing;
                return;
            }
        }

        private static void InsertRecord(Table table, AddressNotFoundRecord record)
        {

            try
            {
                using (var rowBuffer = table.CreateRowBuffer())
                {
                    var def = table.GetDefinition();

                    rowBuffer["Identificador"] = record.Id;
                    rowBuffer["Direccion"] = record.Address;
                    rowBuffer["Poblacion"] = record.CityCode;
                    rowBuffer["full_address_eaab"] = null;
                    rowBuffer["full_address_uacd"] = null;
                    rowBuffer["Geocoder"] = string.IsNullOrWhiteSpace(record.Geocoder) ? "EAAB" : record.Geocoder;
                    if (def.GetFields().Any(f => f.Name.Equals("FechaHora", StringComparison.OrdinalIgnoreCase)))
                    {
                        rowBuffer["FechaHora"] = DateTime.Now;
                    }

                    using (var row = table.CreateRow(rowBuffer)) { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo insertar el registro en la tabla de direcciones no encontradas: {ex}");
            }
        }

        private static async Task EnsureFieldsExist(Geodatabase gdb)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (gdb.GetDefinitions<TableDefinition>().Any(d => string.Equals(d.GetName(), TableName, StringComparison.OrdinalIgnoreCase)))
                {
                    using (var table = gdb.OpenDataset<Table>(TableName))
                    {
                        existing = table
                            .GetDefinition()
                            .GetFields()
                            .Select(f => f.Name)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    messageText: "No se pudo leer la estructura de la tabla de direcciones no encontradas. Se intentará agregar los campos faltantes.",
                    caption: "EAAB Add-In",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Warning
                );
            }

            await AddMissingTableFieldsAsync(gdb, existing);
        }

        private static async Task AddMissingTableFieldsAsync(Geodatabase gdb, HashSet<string> existing)
        {
            var tablePath = Path.Combine(gdb.GetPath().AbsolutePath, TableName);

            foreach (var field in TableFields)
            {
                if (existing.Contains(field.Name))
                {
                    continue;
                }

                await Geoprocessing.ExecuteToolAsync(
                    "management.AddField",
                    Geoprocessing.MakeValueArray(tablePath, field.Name, field.Type, "", "", field.Length),
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.AddToHistory
                );
            }
        }
    }
}
