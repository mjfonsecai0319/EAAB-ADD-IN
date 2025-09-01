using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace EAABAddIn
{
    using Src.Core.Data;

    internal class Button1 : Button
    {
        private readonly IDatabaseConnectionService _connectionService;

        public Button1()
        {
            _connectionService = new DatabaseConnectionService();
        }

        protected override void OnClick()
        {
            QueuedTask.Run(async () =>
            {
                try
                {
                    bool oracleConnected = await TestOracleConnectionAsync();
                    bool pgConnected = await TestPostgresConnectionAsync();

                    ShowConnectionResults(pgConnected, oracleConnected);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al probar conexiones: {ex.Message}", "Error de Conexión");
                }
            });
        }

        private async Task<bool> TestPostgresConnectionAsync()
        {
            // TODO: Reemplazar con tus credenciales de PostgreSQL
            var connectionProps = ConnectionPropertiesFactory.CreatePostgresConnection(
                "your_pg_instance",
                "your_pg_user",
                "your_pg_password",
                "your_pg_database");

            return await _connectionService.TestConnectionAsync(connectionProps);
        }

        private async Task<bool> TestOracleConnectionAsync()
        {
            var connectionProps = ConnectionPropertiesFactory.CreateOracleConnection(
                "172.19.8.169:1548/SITIODEV",
                "sgo",
                "sgodev01");

            return await _connectionService.TestConnectionAsync(connectionProps);
        }

        private void ShowConnectionResults(bool pgConnected, bool oracleConnected)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Resultados de la prueba de conexión:");
            message.AppendLine();
            message.AppendLine($"PostgreSQL: {(pgConnected ? "EXITOSA ✓" : "FALLIDA ✗")}");
            message.AppendLine($"Oracle: {(oracleConnected ? "EXITOSA ✓" : "FALLIDA ✗")}");

            string title = (pgConnected && oracleConnected) ? "Prueba de Conexión - Éxito" :
                           (pgConnected || oracleConnected) ? "Prueba de Conexión - Éxito Parcial" :
                           "Prueba de Conexión - Error";

            MessageBox.Show(message.ToString(), title);
        }
    }
}
