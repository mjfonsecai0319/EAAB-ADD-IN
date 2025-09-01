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
        protected override void OnClick()
        {
            // Ejecutamos el código asíncrono en un QueuedTask para manejar adecuadamente las operaciones asíncronas en ArcGIS Pro
            QueuedTask.Run(async () =>
            {
                try
                {
                    // Probar conexiones
                    bool oracleConnected = await TestOracleConnectionAsync();
                    bool pgConnected = await TestPostgresConnectionAsync();

                    // Mostrar resultados
                    ShowConnectionResults(pgConnected, oracleConnected);
                }
                catch (Exception ex)
                {
                    // Mostrar cualquier error que ocurra
                    MessageBox.Show(
                        $"Error al probar conexiones de base de datos: {ex.Message}\n\n{ex.StackTrace}",
                        "Error de Conexión");
                }
            });
        }

        /// <summary>
        /// Prueba la conexión a la base de datos PostgreSQL
        /// </summary>
        /// <returns>True si la conexión es exitosa, False en caso contrario</returns>
        private async Task<bool> TestPostgresConnectionAsync()
        {
            var dbContext = new DatabaseContext();
            dbContext.SetStrategy(new PostgresStrategy());

            // TODO: Reemplazar con la cadena de conexión real de PostgreSQL
            string connectionString = "Host=your_host;Username=your_user;Password=your_password;Database=your_database";

            try
            {
                using (var connection = dbContext.Connect(connectionString))
                {
                    await Task.Run(() => connection.Open());
                    return connection.State == System.Data.ConnectionState.Open;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Prueba la conexión a la base de datos Oracle
        /// </summary>
        /// <returns>True si la conexión es exitosa, False en caso contrario</returns>
        private async Task<bool> TestOracleConnectionAsync()
        {
            var dbContext = new DatabaseContext();
            dbContext.SetStrategy(new OracleStrategy());

            // TODO: Reemplazar con la cadena de conexión real de Oracle
            string connectionString = "...";

            try
            {
                using (var connection = dbContext.Connect(connectionString))
                {
                    await Task.Run(() => connection.Open());
                    return connection.State == System.Data.ConnectionState.Open;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Muestra los resultados de las pruebas de conexión
        /// </summary>
        /// <param name="pgConnected">Resultado de la conexión a PostgreSQL</param>
        /// <param name="oracleConnected">Resultado de la conexión a Oracle</param>
        private void ShowConnectionResults(bool pgConnected, bool oracleConnected)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Resultados de la prueba de conexión:");
            message.AppendLine();

            message.AppendLine($"PostgreSQL: {(pgConnected ? "EXITOSA ✓" : "FALLIDA ✗")}");
            message.AppendLine($"Oracle: {(oracleConnected ? "EXITOSA ✓" : "FALLIDA ✗")}");

            // Determinar el título según los resultados
            string title;
            if (pgConnected && oracleConnected)
            {
                title = "Prueba de Conexión - Éxito";
            }
            else if (pgConnected || oracleConnected)
            {
                title = "Prueba de Conexión - Éxito Parcial";
            }
            else
            {
                title = "Prueba de Conexión - Error";
            }

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(message.ToString(), title);
        }
    }
}
