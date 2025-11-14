using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EAABAddIn.Src.Application.UseCases
{
    public class ClipFeatureDatasetUseCase
    {
        public async Task<(bool success, string message)> ExecuteAsync(
            string outputGdbPath,
            string acueductoPath,
            string alcantarilladoSanitarioPath,
            string alcantarilladoPluvalPath,
            Polygon clipPolygon,
            double bufferMeters)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputGdbPath))
                    return (false, "Ruta de geodatabase de salida inválida");

                if (clipPolygon == null || clipPolygon.IsEmpty)
                    return (false, "Polígono de corte inválido o vacío");

                // 1. Asegurar GDB de salida (crear con CreateFileGDB si no existe)
                var ensureOutput = await EnsureFileGeodatabaseAsync(outputGdbPath);
                if (!ensureOutput.success)
                    return (false, $"No se pudo crear GDB de salida: {ensureOutput.message}");

                // 2. Crear GDB temporal para máscara
                var tempGdbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tempClip_{Guid.NewGuid():N}.gdb");
                var ensureTemp = await EnsureFileGeodatabaseAsync(tempGdbPath);
                if (!ensureTemp.success)
                    return (false, $"No se pudo crear GDB temporal: {ensureTemp.message}");

                // 3. Crear feature class máscara con polígono (buffer si procede)
                var hasBuffer = bufferMeters > 0.000001;
                var bufferedPolygon = clipPolygon;
                if (hasBuffer)
                {
                    // Si el polígono es un rectángulo alineado al eje, expandir el envelope para esquinas rectas
                    var extentPoly = PolygonBuilderEx.CreatePolygon(clipPolygon.Extent);
                    var isAxisAlignedRect = GeometryEngine.Instance.Equals(clipPolygon, extentPoly);

                    if (isAxisAlignedRect)
                    {
                        var env = clipPolygon.Extent;
                        var eb = new EnvelopeBuilderEx(env);
                        eb.Expand(bufferMeters, bufferMeters, false); // expansión en unidades lineales
                        var expandedEnv = eb.ToGeometry();
                        bufferedPolygon = PolygonBuilderEx.CreatePolygon(expandedEnv);
                    }
                    else
                    {
                        // Intentar mantener esquinas rectas usando offset del contorno con unión Miter
                        bufferedPolygon = TryBuildMiterOffsetPolygon(clipPolygon, bufferMeters) 
                            ?? GeometryEngine.Instance.Buffer(clipPolygon, bufferMeters) as Polygon; // fallback redondeado
                    }
                }

                if (bufferedPolygon == null || bufferedPolygon.IsEmpty)
                    return (false, "Error generando polígono (buffer)");

                var maskFcPath = System.IO.Path.Combine(tempGdbPath, "ClipMask");
                var maskCreated = await CreateMaskFeatureClassAsync(tempGdbPath, "ClipMask", bufferedPolygon);
                if (!maskCreated.success)
                    return (false, $"No se pudo crear máscara: {maskCreated.message}");

                var results = new List<string>();

                // 4. Ejecutar Clip por cada red
                if (!string.IsNullOrWhiteSpace(acueductoPath))
                    results.Add(await ClipWithGeoprocessingAsync(acueductoPath, maskFcPath, outputGdbPath, "acd_recortada"));
                else
                    results.Add("❌ acd_recortada: ruta vacía");

                if (!string.IsNullOrWhiteSpace(alcantarilladoSanitarioPath))
                    results.Add(await ClipWithGeoprocessingAsync(alcantarilladoSanitarioPath, maskFcPath, outputGdbPath, "als_recortada"));
                else
                    results.Add("❌ als_recortada: ruta vacía");

                if (!string.IsNullOrWhiteSpace(alcantarilladoPluvalPath))
                    results.Add(await ClipWithGeoprocessingAsync(alcantarilladoPluvalPath, maskFcPath, outputGdbPath, "alp_recortada"));
                else
                    results.Add("❌ alp_recortada: ruta vacía");

                // 5. Limpiar GDB temporal
                try { if (System.IO.Directory.Exists(tempGdbPath)) System.IO.Directory.Delete(tempGdbPath, true); } catch { }

                var successCount = results.Count(r => r.StartsWith("✓"));
                var failureCount = results.Count(r => r.StartsWith("❌"));
                var outputInfo = $"\n📁 Ubicación: {outputGdbPath}\n📊 Archivos generados esperados: acd_recortada, als_recortada, alp_recortada";
                var bufferInfo = hasBuffer ? $"\n🛟 Buffer aplicado: {bufferMeters:N2} m" : "\n🛟 Sin buffer";
                var finalMessage = $"Clip completado: {successCount} exitosos, {failureCount} fallidos\n" + string.Join("\n", results) + outputInfo + bufferInfo;
                return (failureCount == 0, finalMessage);
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        private async Task<(bool success, string message)> EnsureFileGeodatabaseAsync(string gdbPath)
        {
            if (System.IO.Directory.Exists(gdbPath))
                return (true, "existente");

            var parent = System.IO.Path.GetDirectoryName(gdbPath);
            var name = System.IO.Path.GetFileName(gdbPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return (false, "Ruta inválida para GDB");

            if (name.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4); // sin sufijo

            try
            {
                var createParams = Geoprocessing.MakeValueArray(parent, name);
                var gpResult = await Geoprocessing.ExecuteToolAsync(
                    "management.CreateFileGDB",
                    createParams,
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.None);

                if (gpResult.IsFailed)
                {
                    var msg = gpResult.ErrorMessages != null && gpResult.ErrorMessages.Any()
                        ? string.Join("; ", gpResult.ErrorMessages.Select(m => m.Text))
                        : "CreateFileGDB falló sin detalles";
                    return (false, msg);
                }

                return (System.IO.Directory.Exists(gdbPath), "creada");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool success, string message)> CreateMaskFeatureClassAsync(string gdbPath, string fcName, Polygon polygon)
        {
            try
            {
                var sr = polygon.SpatialReference ?? SpatialReferences.WebMercator;
                var wkid = sr.Wkid > 0 ? sr.Wkid : 4326;

                // Habilitar M/Z según la geometría de entrada para evitar errores al insertar
                var hasM = (polygon.HasM) ? "ENABLED" : "DISABLED";
                var hasZ = (polygon.HasZ) ? "ENABLED" : "DISABLED";

                var createParams = Geoprocessing.MakeValueArray(
                    gdbPath,
                    fcName,
                    "POLYGON",
                    "",
                    hasM,
                    hasZ,
                    wkid);

                var result = await Geoprocessing.ExecuteToolAsync(
                    "management.CreateFeatureclass",
                    createParams,
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.None);

                if (result.IsFailed)
                {
                    var err = result.ErrorMessages != null && result.ErrorMessages.Any()
                        ? string.Join("; ", result.ErrorMessages.Select(m => m.Text))
                        : "CreateFeatureclass falló sin detalles";
                    return (false, err);
                }

                // Insertar polígono
                await QueuedTask.Run(() =>
                {
                    var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
                    using (gdb)
                    {
                        var fc = gdb.OpenDataset<FeatureClass>(fcName);
                        using (fc)
                        {
                            var rowBuffer = fc.CreateRowBuffer();
                            rowBuffer["SHAPE"] = polygon;
                            fc.CreateRow(rowBuffer);
                        }
                    }
                });

                return (true, "máscara creada");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<string> ClipWithGeoprocessingAsync(string sourceFCPath, string maskFCPath, string outputGdbPath, string outputName)
        {
            try
            {
                // Eliminar si existe
                var existingOut = System.IO.Path.Combine(outputGdbPath, outputName);
                if (System.IO.Directory.Exists(existingOut))
                {
                    try
                    {
                        var delParams = Geoprocessing.MakeValueArray(existingOut);
                        await Geoprocessing.ExecuteToolAsync("management.Delete", delParams);
                    }
                    catch { }
                }

                var clipParams = Geoprocessing.MakeValueArray(
                    sourceFCPath,
                    maskFCPath,
                    existingOut);

                var gpResult = await Geoprocessing.ExecuteToolAsync(
                    "analysis.Clip",
                    clipParams,
                    null,
                    CancelableProgressor.None,
                    GPExecuteToolFlags.AddToHistory);

                if (gpResult.IsFailed)
                {
                    var err = gpResult.ErrorMessages != null && gpResult.ErrorMessages.Any()
                        ? string.Join("; ", gpResult.ErrorMessages.Select(m => m.Text))
                        : "Clip falló sin detalles";
                    return $"❌ {outputName}: {err}";
                }
                return $"✓ {outputName}: clipeado";
            }
            catch (Exception ex)
            {
                return $"❌ {outputName}: {ex.Message}";
            }
        }

        /// <summary>
        /// Construye un polígono expandido preservando esquinas (unión Miter) usando offset del contorno.
        /// Si falla, devuelve null para que el llamador haga fallback al buffer estándar.
        /// </summary>
        private Polygon? TryBuildMiterOffsetPolygon(Polygon polygon, double distance)
        {
            try
            {
                if (polygon == null || polygon.IsEmpty || Math.Abs(distance) < 1e-6)
                    return polygon;

                // Tomar el contorno exterior como polilínea
                Polyline? outline = null;
                try
                {
                    // Boundary devuelve el contorno como polilínea
                    outline = GeometryEngine.Instance.Boundary(polygon) as Polyline;
                }
                catch { }

                if (outline == null)
                {
                    // Construir desde los puntos del primer anillo
                    var firstPart = polygon.Parts.FirstOrDefault();
                    if (firstPart != null)
                    {
                        var pts = firstPart.ToList();
                        outline = PolylineBuilderEx.CreatePolyline(pts, polygon.SpatialReference);
                    }
                }

                if (outline == null || outline.IsEmpty)
                    return null;

                // Offset con unión en Miter para mantener esquinas rectas
                // Intentar con el signo dado; si reduce el área, invertir el signo
                var offsetA = GeometryEngine.Instance.Offset(outline, distance, OffsetType.Miter, 1.0) as Polyline;
                Polygon? polyA = (offsetA == null || offsetA.IsEmpty) ? null : PolygonBuilderEx.CreatePolygon(offsetA, polygon.SpatialReference);

                if (polyA == null || polyA.IsEmpty || polyA.Area <= polygon.Area)
                {
                    var offsetB = GeometryEngine.Instance.Offset(outline, -Math.Abs(distance), OffsetType.Miter, 1.0) as Polyline;
                    var polyB = (offsetB == null || offsetB.IsEmpty) ? null : PolygonBuilderEx.CreatePolygon(offsetB, polygon.SpatialReference);
                    if (polyB != null && !polyB.IsEmpty && polyB.Area > polygon.Area)
                        return polyB;

                    return null;
                }

                return polyA;
            }
            catch
            {
                return null;
            }
        }
    }
}
