using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EAABAddIn.Src.Application.Services
{
    public class CsvReportService
    {
        public string EnsureReportsFolder(string baseFolder)
        {
            var folder = Path.Combine(baseFolder, "reportes");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public string WriteReport(string reportsFolder, string datasetName, IEnumerable<string[]> rows)
        {
            var file = Path.Combine(reportsFolder, $"{Sanitize(datasetName)}_validacion.csv");
            using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)); // con BOM para Excel
            // encabezado simple
            writer.WriteLine("Tipo,Mensaje,Detalle");
            foreach (var r in rows ?? Enumerable.Empty<string[]>())
            {
                var cells = (r ?? Array.Empty<string>()).Select(c => Escape(c ?? string.Empty));
                writer.WriteLine(string.Join(",", cells));
            }
            return file;
        }

        /// <summary>
        /// Escribe un resumen de migración por clase de destino.
        /// </summary>
        public string WriteMigrationSummary(
            string reportsFolder,
            string datasetName,
            IEnumerable<(string className, int attempts, int migrated, int failed)> stats,
            int sinClase,
            int sinDestino)
        {
            var file = Path.Combine(reportsFolder, $"{Sanitize(datasetName)}_migracion.csv");
            using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)); // con BOM
            writer.WriteLine("ClaseDestino,Intentos,Migradas,Fallos");
            foreach (var s in stats ?? Enumerable.Empty<(string className, int attempts, int migrated, int failed)>())
            {
                var row = new[] { Escape(s.className), s.attempts.ToString(), s.migrated.ToString(), s.failed.ToString() };
                writer.WriteLine(string.Join(",", row));
            }
            // Filas de resumen global
            writer.WriteLine(string.Join(",", new[] { Escape("_SinCLASE"), sinClase.ToString(), "0", "0" }));
            writer.WriteLine(string.Join(",", new[] { Escape("_SinDestino"), sinDestino.ToString(), "0", "0" }));
            return file;
        }

        private static string Escape(string input)
        {
            if (input.Contains('"') || input.Contains(',') || input.Contains('\n'))
                return '"' + input.Replace("\"", "\"\"") + '"';
            return input;
        }

        private static string Sanitize(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name;
        }
    }
}
