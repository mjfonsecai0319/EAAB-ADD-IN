using ArcGIS.Desktop.Framework.Contracts;
using EAABAddIn.Src.UI;
using EAABAddIn.Src.Core.Entities;
using EAABAddIn.Src.Core.Map;
using EAABAddIn.Src.Domain.Repositories;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using System.Windows;
using EAABAddIn.Src.Core;


namespace EAABAddIn.Src.Presentation.View
{
    internal class Button2 : Button
    {
        protected override async void OnClick()
        {
            var dialog = new FileUploadDialog();
            bool? result = dialog.ShowDialog();

            if (result != true)
                return;

            string filePath = dialog.ViewModel.FilePath;

            // 🔹 Verificar extensión válida
            if (!File.Exists(filePath) || 
                !(filePath.EndsWith(".xlsx") || filePath.EndsWith(".xlsm")))
            {
                MessageBox.Show("❌ Solo se permiten archivos Excel (.xlsx o .xlsm)", "Error");
                return;
            }

            List<string> direcciones;
            try
            {
                direcciones = LeerDireccionesExcel(filePath);
            }
            catch
            {
                MessageBox.Show("❌ Error al leer el archivo Excel.", "Error");
                return;
            }

            // 🔹 Determinar motor de BD (ajusta según tu caso real)
            string engineName = "POSTGRESQL"; // o "ORACLE"
            DBEngine engine = engineName.ToDBEngine();

            IPtAddressGralEntityRepository repo = engine switch
            {
                DBEngine.Oracle => new PtAddressGralOracleRepository(),
                DBEngine.PostgreSQL => new PtAddressGralPostgresRepository(),
                _ => null
            };

            if (repo == null) return;

            await QueuedTask.Run(async () =>
            {
                int encontrados = 0;
                int noEncontrados = 0;

                foreach (var direccion in direcciones)
                {
                    var resultados = repo.FindByAddress(null, direccion);

                    if (resultados.Count > 0)
                    {
                        var entidad = resultados[0];

                        if (entidad.Latitud.HasValue && entidad.Longitud.HasValue)
                        {
                            await ResultsLayerService.AddPointAsync(
                                entidad.Latitud.Value,
                                entidad.Longitud.Value
                            );
                            encontrados++;
                        }
                        else
                        {
                            noEncontrados++;
                        }
                    }
                    else
                    {
                        noEncontrados++;
                    }
                }

                MessageBox.Show(
                    $"✅ Se marcaron {encontrados} direcciones.\n⚠️ No se encontraron {noEncontrados}.",
                    "Resultado geocodificación"
                );
            });
        }

        private List<string> LeerDireccionesExcel(string filePath)
        {
            var lista = new List<string>();
            using (var workbook = new ClosedXML.Excel.XLWorkbook(filePath))
            {
                var ws = workbook.Worksheet(1);
                foreach (var row in ws.RowsUsed())
                {
                    string direccion = row.Cell(1).GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(direccion))
                        lista.Add(direccion);
                }
            }
            return lista;
        }

    }
}
