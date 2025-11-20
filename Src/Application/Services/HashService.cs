using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EAABAddIn.Src.Application.Services
{
    /// <summary>
    /// Servicio para calcular y gestionar hashes SHA256 de archivos
    /// </summary>
    public static class HashService
    {
        /// <summary>
        /// Calcula el SHA256 de un archivo
        /// </summary>
        /// <param name="archivoPath">Ruta completa del archivo</param>
        /// <returns>String hexadecimal del hash en minúsculas</returns>
        public static string CalcularSHA256(string archivoPath)
        {
            if (string.IsNullOrWhiteSpace(archivoPath))
                throw new ArgumentException("La ruta del archivo no puede estar vacía", nameof(archivoPath));

            if (!File.Exists(archivoPath))
                throw new FileNotFoundException("El archivo no existe", archivoPath);

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(archivoPath);
            
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Calcula el SHA256 de todos los archivos en una carpeta (no recursivo)
        /// </summary>
        /// <param name="carpetaPath">Ruta de la carpeta</param>
        /// <returns>Diccionario con nombre de archivo como clave y hash como valor</returns>
        public static Dictionary<string, string> CalcularSHA256Carpeta(string carpetaPath)
        {
            if (string.IsNullOrWhiteSpace(carpetaPath))
                throw new ArgumentException("La ruta de la carpeta no puede estar vacía", nameof(carpetaPath));

            if (!Directory.Exists(carpetaPath))
                throw new DirectoryNotFoundException($"La carpeta no existe: {carpetaPath}");

            var resultado = new Dictionary<string, string>();
            var archivos = Directory.GetFiles(carpetaPath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith("_HASH.txt", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var archivo in archivos)
            {
                try
                {
                    var nombreArchivo = Path.GetFileName(archivo);
                    var hash = CalcularSHA256(archivo);
                    resultado[nombreArchivo] = hash;
                }
                catch (Exception ex)
                {
                    // Si un archivo individual falla, lo registramos pero continuamos
                    System.Diagnostics.Debug.WriteLine($"Error calculando hash de {archivo}: {ex.Message}");
                }
            }

            return resultado;
        }

        /// <summary>
        /// Extrae el hash de un archivo .txt de hash
        /// </summary>
        /// <param name="hashFilePath">Ruta del archivo .txt que contiene el hash</param>
        /// <returns>Tupla con éxito y el hash extraído</returns>
        public static (bool ok, string hash) ExtraerHashDeArchivo(string hashFilePath)
        {
            if (string.IsNullOrWhiteSpace(hashFilePath))
                return (false, string.Empty);

            if (!File.Exists(hashFilePath))
                return (false, string.Empty);

            try
            {
                var lineas = File.ReadAllLines(hashFilePath);
                var lineaSHA256 = lineas.FirstOrDefault(l => l.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase));

                if (lineaSHA256 != null)
                {
                    var hash = lineaSHA256.Substring("SHA256:".Length).Trim();
                    return (true, hash);
                }

                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extrayendo hash de {hashFilePath}: {ex.Message}");
                return (false, string.Empty);
            }
        }

        /// <summary>
        /// Genera el nombre del archivo HASH con formato: nombreArchivo_AAAMMDDHHMMSS_HASH.txt
        /// </summary>
        /// <param name="archivoPath">Ruta del archivo para el cual generar el nombre de hash</param>
        /// <returns>Nombre completo del archivo hash</returns>
        public static string GenerarNombreArchivoHash(string archivoPath)
        {
            if (string.IsNullOrWhiteSpace(archivoPath))
                throw new ArgumentException("La ruta del archivo no puede estar vacía", nameof(archivoPath));

            var nombreBase = Path.GetFileNameWithoutExtension(archivoPath);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            return $"{nombreBase}_{timestamp}_HASH.txt";
        }

        /// <summary>
        /// Genera el nombre del archivo HASH para una carpeta
        /// </summary>
        /// <param name="carpetaPath">Ruta de la carpeta</param>
        /// <returns>Nombre del archivo hash</returns>
        public static string GenerarNombreArchivoHashCarpeta(string carpetaPath)
        {
            if (string.IsNullOrWhiteSpace(carpetaPath))
                throw new ArgumentException("La ruta de la carpeta no puede estar vacía", nameof(carpetaPath));

            var nombreCarpeta = Path.GetFileName(carpetaPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            return $"{nombreCarpeta}_{timestamp}_HASH.txt";
        }

        /// <summary>
        /// Genera el contenido del archivo HASH para un único archivo
        /// </summary>
        /// <param name="archivoPath">Ruta del archivo</param>
        /// <param name="sha256">Hash SHA256 del archivo</param>
        /// <returns>Contenido formateado del archivo hash</returns>
        public static string GenerarContenidoHashTxt(string archivoPath, string sha256)
        {
            var nombreArchivo = Path.GetFileName(archivoPath);
            var fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            var sb = new StringBuilder();
            sb.AppendLine($"Archivo: {nombreArchivo}");
            sb.AppendLine($"SHA256: {sha256}");
            sb.AppendLine($"Fecha: {fecha}");

            // Agregar tamaño si el archivo existe
            if (File.Exists(archivoPath))
            {
                var fileInfo = new FileInfo(archivoPath);
                var tamañoMB = fileInfo.Length / (1024.0 * 1024.0);
                sb.AppendLine($"Tamaño: {tamañoMB:F2} MB");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Genera el contenido del archivo HASH para múltiples archivos en una carpeta
        /// </summary>
        /// <param name="carpetaPath">Ruta de la carpeta</param>
        /// <param name="hashes">Diccionario con nombre de archivo y su hash</param>
        /// <returns>Contenido formateado del archivo hash</returns>
        public static string GenerarContenidoHashTxtCarpeta(string carpetaPath, Dictionary<string, string> hashes)
        {
            var fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            var sb = new StringBuilder();
            sb.AppendLine($"Carpeta: {carpetaPath}");
            sb.AppendLine($"Fecha: {fecha}");
            sb.AppendLine($"Total archivos: {hashes.Count}");
            sb.AppendLine();

            // Ordenar alfabéticamente por nombre de archivo
            foreach (var kvp in hashes.OrderBy(x => x.Key))
            {
                sb.AppendLine($"{kvp.Key,-40} | SHA256: {kvp.Value}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Busca automáticamente un archivo HASH en la misma carpeta que el archivo dado
        /// </summary>
        /// <param name="archivoPath">Ruta del archivo</param>
        /// <returns>Ruta del archivo hash si existe, null si no</returns>
        public static string BuscarArchivoHashEnCarpeta(string archivoPath)
        {
            if (string.IsNullOrWhiteSpace(archivoPath) || !File.Exists(archivoPath))
                return null;

            var directorio = Path.GetDirectoryName(archivoPath);
            if (string.IsNullOrWhiteSpace(directorio))
                return null;

            var nombreArchivo = Path.GetFileNameWithoutExtension(archivoPath);
            
            // Buscar archivos que coincidan con el patrón: nombreArchivo_*_HASH.txt
            var archivosHash = Directory.GetFiles(directorio, $"{nombreArchivo}_*_HASH.txt", SearchOption.TopDirectoryOnly);

            // Retornar el más reciente si hay varios
            return archivosHash
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();
        }

        /// <summary>
        /// Compara dos hashes SHA256
        /// </summary>
        /// <param name="hash1">Primer hash</param>
        /// <param name="hash2">Segundo hash</param>
        /// <returns>True si son iguales (ignorando mayúsculas)</returns>
        public static bool CompararHashes(string hash1, string hash2)
        {
            if (string.IsNullOrWhiteSpace(hash1) || string.IsNullOrWhiteSpace(hash2))
                return false;

            return string.Equals(hash1.Trim(), hash2.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
