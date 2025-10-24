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
