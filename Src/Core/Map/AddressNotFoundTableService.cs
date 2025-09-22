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

        public static Task AddRecordAsync(AddressNotFoundRecord record)
        {
            return QueuedTask.Run(() => _AddRecordAsync(record));
        }

        private static async Task _AddRecordAsync(AddressNotFoundRecord record)
        {
            var gdbPath = Project.Current.DefaultGeodatabasePath;
            // Validar que el path de la GDB exista (la carpeta .gdb)
            if (string.IsNullOrEmpty(gdbPath) || !Directory.Exists(gdbPath))
                return;

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            // Crear tabla si no existe (evitar schema locks abriendo y cerrando conexión)
            bool exists;
            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            {
                exists = geodatabase
                    .GetDefinitions<TableDefinition>()
                    .Any(d => string.Equals(d.GetName(), TableName, StringComparison.OrdinalIgnoreCase));
            }

            if (!exists)
            {
                var createTableParams = Geoprocessing.MakeValueArray(
                    gdbPath,
                    TableName
                );
                var result = await Geoprocessing.ExecuteToolAsync(
                    "management.CreateTable",
                    createTableParams,
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.AddToHistory);
                if (result.IsFailed) return;
            }

            // Asegurar campos requeridos
            await EnsureFieldsExist(gdbPath);

            // Agregar la tabla al mapa si no existe
            _AddStandaloneTableToMapView(gdbPath);

            using (var geodatabase = new Geodatabase(gdbConnectionPath))
            using (var table = geodatabase.OpenDataset<Table>(TableName))
            {
                try
                {
                    using (var rowBuffer = table.CreateRowBuffer())
                    {
                        var def = table.GetDefinition();
                        // Cargar valores en el buffer con seguridad ante nulos
                        rowBuffer["Identificador"] = record.Identificador ?? string.Empty;
                        rowBuffer["Direccion"] = record.Direccion ?? string.Empty;
                        rowBuffer["Poblacion"] = record.Poblacion ?? string.Empty;
                        rowBuffer["full_address_eaab"] = record.FullAddressEaab ?? string.Empty;
                        rowBuffer["full_address_uacd"] = record.FullAddressUacd ?? string.Empty;
                        rowBuffer["Geocoder"] = string.IsNullOrWhiteSpace(record.Geocoder) ? "EAAB" : record.Geocoder;
                        if (record.Score.HasValue) rowBuffer["Score"] = record.Score.Value; else rowBuffer["Score"] = null;

                        using (var row = table.CreateRow(rowBuffer)) { }
                    }
                }
                catch (Exception)
                {
                    // Silenciar para no bloquear el flujo en caso de esquema inconsistente
                    // (similar a manejo en ResultsLayerService)
                }
            }
        }

        private static void _AddStandaloneTableToMapView(string gdbPath)
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null)
                return;

            // Buscar si ya está agregada
            if (_notFoundTable is null)
            {
                try
                {
                    var existing = mapView.Map.StandaloneTables
                        .FirstOrDefault(st => string.Equals(st.Name, TableName, StringComparison.OrdinalIgnoreCase));
                    if (existing is not null)
                    {
                        _notFoundTable = existing;
                        return;
                    }
                }
                catch
                {
                    // Si falla la inspección, continuamos con la creación
                }
            }

            if (_notFoundTable is not null)
                return;

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));
            using (var gdb = new Geodatabase(gdbConnectionPath))
            using (var table = gdb.OpenDataset<Table>(TableName))
            {
                var stParams = new StandaloneTableCreationParams(table)
                {
                    Name = TableName
                };
                _notFoundTable = StandaloneTableFactory.Instance.CreateStandaloneTable(stParams, mapView.Map);
            }
        }

        private static async Task EnsureFieldsExist(string gdbPath)
        {
            var required = new (string Name, string Type, string Length)[]
            {
                ("Identificador", "TEXT", "100"),
                ("Direccion", "TEXT", "255"),
                ("Poblacion", "TEXT", "100"),
                ("full_address_eaab", "TEXT", "255"),
                ("full_address_uacd", "TEXT", "255"),
                ("Geocoder", "TEXT", "100"),
                ("Score", "DOUBLE", "")
            };

            var gdbConnectionPath = new FileGeodatabaseConnectionPath(new Uri(gdbPath));

            // Verificar campos existentes primero para evitar errores al intentar crear duplicados
            HashSet<string> existingFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var gdb = new Geodatabase(gdbConnectionPath))
                {
                    // Si la tabla no existe aún, saltar comprobación de campos (se crearán después)
                    if (gdb.GetDefinitions<TableDefinition>().Any(d => string.Equals(d.GetName(), TableName, StringComparison.OrdinalIgnoreCase)))
                    {
                        using (var table = gdb.OpenDataset<Table>(TableName))
                        {
                            existingFieldNames = table
                                .GetDefinition()
                                .GetFields()
                                .Select(f => f.Name)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                // Si falla la lectura del esquema, continuamos a intentar agregar campos via GP
            }

            foreach (var field in required)
            {
                if (existingFieldNames.Contains(field.Name))
                    continue;

                var tablePath = Path.Combine(gdbPath, TableName);
                var addFieldParams = Geoprocessing.MakeValueArray(
                    tablePath,
                    field.Name,
                    field.Type,
                    "",
                    "",
                    field.Length
                );
                await Geoprocessing.ExecuteToolAsync(
                    "management.AddField",
                    addFieldParams,
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.AddToHistory);
            }
        }
    }
}
