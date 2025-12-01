using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EAABAddIn.Src.Application.Services;

namespace EAABAddIn.Src.Application.UseCases
{

    public class GenerarHashUseCase
    {

        /// <param name="gdbPath">Ruta de la carpeta o GDB a comprimir</param>
        public async Task<(bool ok, string zipPath, string hashPath, string message)> ComprimirGdbYGenerarHash(string gdbPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gdbPath))
                    return (false, string.Empty, string.Empty, "❌ La ruta no puede estar vacía");

                gdbPath = Path.GetFullPath(gdbPath);

                if (!Directory.Exists(gdbPath))
                    return (false, string.Empty, string.Empty, $"❌ La ruta no existe:\n{gdbPath}");

                var esGdb = CompressionService.EsGDB(gdbPath);
                var advertenciaGdb = !esGdb ? "⚠️  La carpeta no parece ser una GDB (.gdb). " : string.Empty;

                var (comprOk, zipPath, comprMsg) = await CompressionService.ComprimirEnZip(gdbPath);
                
                if (!comprOk)
                    return (false, string.Empty, string.Empty, $"❌ Error al comprimir: {comprMsg}");

                string sha256;
                try
                {
                    sha256 = HashService.CalcularSHA256(zipPath);
                }
                catch (Exception ex)
                {
                    return (false, zipPath, string.Empty, $"❌ Error al calcular hash: {ex.Message}");
                }

                var hashFileName = HashService.GenerarNombreArchivoHash(zipPath);
                var carpetaPadre = Path.GetDirectoryName(zipPath);
                var hashPath = Path.Combine(carpetaPadre ?? string.Empty, hashFileName);

                var contenidoHash = HashService.GenerarContenidoHashTxt(zipPath, sha256);
                
                try
                {
                    await File.WriteAllTextAsync(hashPath, contenidoHash);
                }
                catch (Exception ex)
                {
                    return (false, zipPath, string.Empty, $"❌ Error al crear archivo hash: {ex.Message}");
                }

                var mensaje = $"{advertenciaGdb}✅ Archivos generados exitosamente:\n\n" +
                             $"   • ZIP: {Path.GetFileName(zipPath)}\n" +
                             $"   • HASH: {Path.GetFileName(hashPath)}\n\n" +
                             $"SHA256: {sha256}";

                return (true, zipPath, hashPath, mensaje);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, string.Empty, $"❌ Error inesperado: {ex.Message}");
            }
        }


        /// <param name="carpetaPath">Ruta de la carpeta</param>
        public async Task<(bool ok, string resumenPath, Dictionary<string, string> hashes, string message)> GenerarHashArchivosEnCarpeta(string carpetaPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(carpetaPath))
                    return (false, string.Empty, new Dictionary<string, string>(), "❌ La ruta no puede estar vacía");

                if (!Directory.Exists(carpetaPath))
                    return (false, string.Empty, new Dictionary<string, string>(), $"❌ La carpeta no existe: {carpetaPath}");

                List<string> archivos;
                try
                {
                    archivos = CompressionService.ObtenerArchivosCarpeta(carpetaPath);
                }
                catch (Exception ex)
                {
                    return (false, string.Empty, new Dictionary<string, string>(), $"❌ Error al obtener archivos: {ex.Message}");
                }

                if (archivos.Count == 0)
                    return (false, string.Empty, new Dictionary<string, string>(), "⚠️  La carpeta no contiene archivos");

                var hashes = new Dictionary<string, string>();
                var errores = new List<string>();

                await Task.Run(() =>
                {
                    foreach (var archivoPath in archivos)
                    {
                        try
                        {
                            var nombreArchivo = Path.GetFileName(archivoPath);
                            var hash = HashService.CalcularSHA256(archivoPath);
                            hashes[nombreArchivo] = hash;
                        }
                        catch (Exception ex)
                        {
                            errores.Add($"{Path.GetFileName(archivoPath)}: {ex.Message}");
                        }
                    }
                });

                var hashFileName = HashService.GenerarNombreArchivoHashCarpeta(carpetaPath);
                var resumenPath = Path.Combine(carpetaPath, hashFileName);

                var contenidoResumen = HashService.GenerarContenidoHashTxtCarpeta(carpetaPath, hashes);

                try
                {
                    await File.WriteAllTextAsync(resumenPath, contenidoResumen);
                }
                catch (Exception ex)
                {
                    return (false, string.Empty, hashes, $"❌ Error al crear archivo resumen: {ex.Message}");
                }

                var mensaje = $"✅ Hashes generados ({hashes.Count} archivos):\n";
                
                var ejemplos = hashes.Take(5).ToList();
                foreach (var kvp in ejemplos)
                {
                    var hashCorto = kvp.Value.Length > 16 ? kvp.Value.Substring(0, 16) + "..." : kvp.Value;
                    mensaje += $"   • {kvp.Key}: {hashCorto}\n";
                }

                if (hashes.Count > 5)
                    mensaje += $"   ... y {hashes.Count - 5} más\n";

                mensaje += $"\n📁 Archivo resumen: {Path.GetFileName(resumenPath)}";

                if (errores.Count > 0)
                    mensaje += $"\n\n⚠️  {errores.Count} archivo(s) con errores (ver log)";

                return (true, resumenPath, hashes, mensaje);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, new Dictionary<string, string>(), $"❌ Error inesperado: {ex.Message}");
            }
        }

        /// <param name="archivoPath">Ruta del archivo a verificar</param>
        public async Task<(bool ok, bool coinciden, string hashEsperado, string hashActual, string message)> VerificarIntegridadArchivo(string archivoPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(archivoPath))
                    return (false, false, string.Empty, string.Empty, "❌ La ruta del archivo no puede estar vacía");

                if (!File.Exists(archivoPath))
                    return (false, false, string.Empty, string.Empty, $"❌ El archivo no existe: {archivoPath}");

                var hashFilePath = HashService.BuscarArchivoHashEnCarpeta(archivoPath);
                
                if (string.IsNullOrWhiteSpace(hashFilePath))
                {
                    var nombreArchivo = Path.GetFileNameWithoutExtension(archivoPath);
                    return (false, false, string.Empty, string.Empty, 
                        $"❌ No se encontró archivo HASH\n" +
                        $"   Se esperaba un archivo: {nombreArchivo}_*_HASH.txt\n" +
                        $"   en la misma carpeta del archivo");
                }

                var (extOk, hashEsperado) = HashService.ExtraerHashDeArchivo(hashFilePath);
                
                if (!extOk || string.IsNullOrWhiteSpace(hashEsperado))
                    return (false, false, string.Empty, string.Empty, 
                        $"❌ No se pudo extraer el hash del archivo: {Path.GetFileName(hashFilePath)}");

                string hashActual;
                try
                {
                    hashActual = await Task.Run(() => HashService.CalcularSHA256(archivoPath));
                }
                catch (Exception ex)
                {
                    return (false, false, hashEsperado, string.Empty, 
                        $"❌ Error al calcular hash del archivo: {ex.Message}");
                }

                var coinciden = HashService.CompararHashes(hashEsperado, hashActual);

                string mensaje;
                if (coinciden)
                {
                    mensaje = "✅ INTEGRIDAD VERIFICADA\n\n" +
                             $"   Archivo: {Path.GetFileName(archivoPath)}\n\n" +
                             $"   HASH esperado: {hashEsperado}\n\n" +
                             $"   HASH actual:   {hashActual}\n\n" +
                             $"   ✅ Los hashes coinciden - Archivo íntegro";
                }
                else
                {
                    mensaje = 
                             $"   Archivo: {Path.GetFileName(archivoPath)}\n\n" +
                             $"   HASH esperado: {hashEsperado}\n\n" +
                             $"   HASH actual:   {hashActual}\n\n" +
                             $"   ⚠️  Los hashes NO coinciden\n" +
                             $"   ⚠️  El archivo puede estar corrupto o modificado";
                }

                return (true, coinciden, hashEsperado, hashActual, mensaje);
            }
            catch (Exception ex)
            {
                return (false, false, string.Empty, string.Empty, $"❌ Error inesperado: {ex.Message}");
            }
        }
    }
}
