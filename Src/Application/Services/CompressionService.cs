using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace EAABAddIn.Src.Application.Services
{
    /// <summary>
    /// Servicio para comprimir carpetas y archivos en formato ZIP
    /// </summary>
    public static class CompressionService
    {
        /// <summary>
        /// Comprime una carpeta en un archivo ZIP
        /// </summary>
        /// <param name="carpetaPath">Ruta de la carpeta a comprimir</param>
        /// <returns>Tupla con éxito, ruta del ZIP creado y mensaje</returns>
        public static async Task<(bool ok, string zipPath, string message)> ComprimirEnZip(string carpetaPath)
        {
            if (string.IsNullOrWhiteSpace(carpetaPath))
                return (false, string.Empty, "La ruta de la carpeta no puede estar vacía");

            if (!Directory.Exists(carpetaPath))
                return (false, string.Empty, $"La carpeta no existe: {carpetaPath}");

            try
            {
                var nombreCarpeta = new DirectoryInfo(carpetaPath).Name;
                var carpetaPadre = Directory.GetParent(carpetaPath)?.FullName;

                if (string.IsNullOrWhiteSpace(carpetaPadre))
                    return (false, string.Empty, "No se pudo determinar la carpeta padre");

                // Generar nombre del ZIP con timestamp
                var nombreZip = GenerarNombreConTimestamp(nombreCarpeta, ".zip");
                var zipPath = Path.Combine(carpetaPadre, nombreZip);

                // Si ya existe un archivo con ese nombre, agregar un sufijo
                int contador = 1;
                while (File.Exists(zipPath))
                {
                    nombreZip = GenerarNombreConTimestamp($"{nombreCarpeta}_{contador}", ".zip");
                    zipPath = Path.Combine(carpetaPadre, nombreZip);
                    contador++;
                }

                // Comprimir en un hilo separado para evitar bloquear la UI
                await Task.Run(() =>
                {
                    ZipFile.CreateFromDirectory(
                        carpetaPath,
                        zipPath,
                        CompressionLevel.Optimal,
                        includeBaseDirectory: false
                    );
                });

                return (true, zipPath, $"Archivo ZIP creado exitosamente: {Path.GetFileName(zipPath)}");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Error al comprimir: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene la lista de archivos en una carpeta (no recursivo)
        /// Excluye archivos de hash (_HASH.txt)
        /// </summary>
        /// <param name="carpetaPath">Ruta de la carpeta</param>
        /// <returns>Lista de rutas completas de archivos</returns>
        public static List<string> ObtenerArchivosCarpeta(string carpetaPath)
        {
            if (string.IsNullOrWhiteSpace(carpetaPath))
                throw new ArgumentException("La ruta de la carpeta no puede estar vacía", nameof(carpetaPath));

            if (!Directory.Exists(carpetaPath))
                throw new DirectoryNotFoundException($"La carpeta no existe: {carpetaPath}");

            var archivos = Directory.GetFiles(carpetaPath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith("_HASH.txt", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return archivos;
        }

        /// <summary>
        /// Genera un nombre con timestamp en formato: nombreBase_AAAAMMDDHHMMSS + extensión
        /// </summary>
        /// <param name="nombreBase">Nombre base del archivo o carpeta</param>
        /// <param name="extension">Extensión del archivo (incluir el punto, ej: ".zip")</param>
        /// <returns>Nombre completo con timestamp</returns>
        public static string GenerarNombreConTimestamp(string nombreBase, string extension = "")
        {
            if (string.IsNullOrWhiteSpace(nombreBase))
                throw new ArgumentException("El nombre base no puede estar vacío", nameof(nombreBase));

            // Limpiar caracteres no válidos del nombre
            var nombreLimpio = LimpiarNombreArchivo(nombreBase);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            
            return $"{nombreLimpio}_{timestamp}{extension}";
        }

        /// <summary>
        /// Limpia un nombre de archivo de caracteres no válidos
        /// </summary>
        /// <param name="nombre">Nombre a limpiar</param>
        /// <returns>Nombre limpio</returns>
        private static string LimpiarNombreArchivo(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "archivo";

            var caracteresInvalidos = Path.GetInvalidFileNameChars();
            var nombreLimpio = new string(nombre.Select(c => 
                caracteresInvalidos.Contains(c) ? '_' : c
            ).ToArray());

            return nombreLimpio;
        }

        /// <summary>
        /// Valida si una ruta corresponde a una GDB (Geodatabase de ESRI)
        /// </summary>
        /// <param name="carpetaPath">Ruta de la carpeta</param>
        /// <returns>True si es una GDB válida</returns>
        public static bool EsGDB(string carpetaPath)
        {
            if (string.IsNullOrWhiteSpace(carpetaPath))
                return false;

            if (!Directory.Exists(carpetaPath))
                return false;

            // Una GDB debe tener extensión .gdb en su nombre de carpeta
            return carpetaPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Obtiene el tamaño total de una carpeta en bytes
        /// </summary>
        /// <param name="carpetaPath">Ruta de la carpeta</param>
        /// <returns>Tamaño en bytes</returns>
        public static long ObtenerTamañoCarpeta(string carpetaPath)
        {
            if (string.IsNullOrWhiteSpace(carpetaPath) || !Directory.Exists(carpetaPath))
                return 0;

            try
            {
                var dirInfo = new DirectoryInfo(carpetaPath);
                return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Formatea un tamaño en bytes a una representación legible (KB, MB, GB)
        /// </summary>
        /// <param name="bytes">Tamaño en bytes</param>
        /// <returns>String formateado</returns>
        public static string FormatearTamaño(long bytes)
        {
            string[] sufijos = { "B", "KB", "MB", "GB", "TB" };
            int indice = 0;
            double tamaño = bytes;

            while (tamaño >= 1024 && indice < sufijos.Length - 1)
            {
                tamaño /= 1024;
                indice++;
            }

            return $"{tamaño:F2} {sufijos[indice]}";
        }
    }
}
