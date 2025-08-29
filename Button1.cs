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
    using Src.Core.Data.Common;

    internal class Button1 : Button
    {
        protected override void OnClick()
        {
            // Ejecutamos el código asíncrono en un QueuedTask para manejar adecuadamente las operaciones asíncronas en ArcGIS Pro
            QueuedTask.Run(async () =>
            {
                try
                {
                    // Probar conexión a Oracle
                    bool oracleConnected = await TestOracleConnectionAsync();

                    // Mostrar resultados
                    ShowConnectionResults(oracleConnected);
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
        /// Prueba la conexión a la base de datos Oracle
        /// </summary>
        /// <returns>True si la conexión es exitosa, False en caso contrario</returns>
        private async Task<bool> TestOracleConnectionAsync()
        {
            try
            {
                using (var connection = new DatabaseConnection(
                    DatabaseStrategyFactory.CreateDatabaseStrategy(DatabaseType.Oracle)))
                {
                    return await connection.TestConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al probar la conexión a Oracle: {ex.Message}", "Error de Conexión");
                return false;
            }
        }

        /// <summary>
        /// Muestra los resultados de las pruebas de conexión
        /// </summary>
        /// <param name="pgConnected">Resultado de la conexión a PostgreSQL</param>
        /// <param name="oracleConnected">Resultado de la conexión a Oracle</param>
        private void ShowConnectionResults(bool oracleConnected)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Resultados de la prueba de conexión:");
            message.AppendLine();

            message.AppendLine($"Oracle: {(oracleConnected ? "EXITOSA ✓" : "FALLIDA ✗")}");

            // Determinar el título según los resultados
            string title = (oracleConnected) ?
                "Prueba de Conexión - Éxito Parcial" :
                "Prueba de Conexión - Error";

            if (oracleConnected)
            {
                title = "Prueba de Conexión - Éxito";
            }

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(message.ToString(), title);
        }
    }
}
