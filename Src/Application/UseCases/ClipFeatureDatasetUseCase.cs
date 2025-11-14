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
            string sourceGdbPath,
            List<(string datasetName, List<string> featureClasses)> featureDatasets,
            Polygon clipPolygon,
            double bufferMeters)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputGdbPath))
                    return (false, "Ruta de geodatabase de salida inválida");

                if (string.IsNullOrWhiteSpace(sourceGdbPath))
                    return (false, "Ruta de geodatabase de origen inválida");

                if (featureDatasets == null || !featureDatasets.Any())
                    return (false, "No hay Feature Datasets para procesar");

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
                    var originalArea = clipPolygon.Area;
                    System.Diagnostics.Debug.WriteLine($"═══════════════════════════════════════════════════════");
                    System.Diagnostics.Debug.WriteLine($"Aplicando buffer de {bufferMeters} unidades al polígono");
                    System.Diagnostics.Debug.WriteLine($"Área original: {originalArea:N2}");
                    System.Diagnostics.Debug.WriteLine($"Sistema de coordenadas: {clipPolygon.SpatialReference?.Name ?? "desconocido"}");
                    System.Diagnostics.Debug.WriteLine($"WKID: {clipPolygon.SpatialReference?.Wkid ?? 0}");
                    
                    // Aplicar buffer estándar (más confiable)
                    try
                    {
                        bufferedPolygon = GeometryEngine.Instance.Buffer(clipPolygon, bufferMeters) as Polygon;
                        
                        if (bufferedPolygon != null && !bufferedPolygon.IsEmpty)
                        {
                            var newArea = bufferedPolygon.Area;
                            var areaIncrease = ((newArea - originalArea) / originalArea) * 100;
                            
                            System.Diagnostics.Debug.WriteLine($"✓ Buffer aplicado exitosamente");
                            System.Diagnostics.Debug.WriteLine($"  Área con buffer: {newArea:N2}");
                            System.Diagnostics.Debug.WriteLine($"  Incremento: {areaIncrease:F2}%");
                            
                            // Validación: si el área aumentó más del 500%, puede haber un problema de unidades
                            if (areaIncrease > 500)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ ADVERTENCIA: El buffer aumentó el área más del 500%");
                                System.Diagnostics.Debug.WriteLine($"⚠️ Esto puede indicar un problema con las unidades del buffer");
                                System.Diagnostics.Debug.WriteLine($"⚠️ Considera verificar que el buffer en metros sea el correcto");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Error aplicando buffer: {ex.Message}");
                        bufferedPolygon = clipPolygon; // Usar polígono original si falla
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"═══════════════════════════════════════════════════════");
                }

                if (bufferedPolygon == null || bufferedPolygon.IsEmpty)
                    return (false, "Error generando polígono (buffer)");

                var maskFcPath = System.IO.Path.Combine(tempGdbPath, "ClipMask");
                var maskCreated = await CreateMaskFeatureClassAsync(tempGdbPath, "ClipMask", bufferedPolygon);
                if (!maskCreated.success)
                    return (false, $"No se pudo crear máscara: {maskCreated.message}");

                var results = new List<string>();
                var datasetsCreated = new List<string>();
                int totalFeatureClasses = 0;

                // 4. Procesar cada Feature Dataset
                foreach (var (datasetName, featureClasses) in featureDatasets)
                {
                    var outputDatasetName = $"clip_{datasetName}";
                    
                    // Crear el Feature Dataset en la GDB de salida
                    var datasetCreated = await CreateFeatureDatasetAsync(outputGdbPath, outputDatasetName, sourceGdbPath, datasetName);
                    if (!datasetCreated.success)
                    {
                        results.Add($"❌ {outputDatasetName}: no se pudo crear el dataset - {datasetCreated.message}");
                        continue;
                    }

                    datasetsCreated.Add(outputDatasetName);
                    int fcSuccessCount = 0;

                    // Clipear cada Feature Class dentro del dataset
                    foreach (var fcName in featureClasses)
                    {
                        var sourceFcPath = System.IO.Path.Combine(sourceGdbPath, datasetName, fcName);
                        var outputFcPath = System.IO.Path.Combine(outputGdbPath, outputDatasetName, fcName);
                        
                        var clipResult = await ClipFeatureClassAsync(sourceFcPath, maskFcPath, outputFcPath);
                        
                        if (clipResult.StartsWith("✓"))
                        {
                            fcSuccessCount++;
                            totalFeatureClasses++;
                        }
                        
                        results.Add($"  {clipResult}");
                    }

                    results.Add($"✓ {outputDatasetName}: {fcSuccessCount}/{featureClasses.Count} feature classes procesadas");
                }

                // 5. Limpiar GDB temporal
                try { if (System.IO.Directory.Exists(tempGdbPath)) System.IO.Directory.Delete(tempGdbPath, true); } catch { }

                var successCount = results.Count(r => r.TrimStart().StartsWith("✓"));
                var failureCount = results.Count(r => r.TrimStart().StartsWith("❌"));
                var outputInfo = $"\n📁 Ubicación: {outputGdbPath}\n📊 Feature Datasets creados: {string.Join(", ", datasetsCreated)}\n📊 Total Feature Classes procesadas: {totalFeatureClasses}";
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

        private async Task<(bool success, string message)> CreateFeatureDatasetAsync(string outputGdbPath, string datasetName, string sourceGdbPath, string sourceDatasetName)
        {
            try
            {
                // Obtener la referencia espacial del dataset de origen
                await QueuedTask.Run(() =>
                {
                    var sourceGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(sourceGdbPath)));
                    using (sourceGdb)
                    {
                        using var sourceDataset = sourceGdb.OpenDataset<FeatureDataset>(sourceDatasetName);
                        var spatialRef = sourceDataset.GetDefinition().GetSpatialReference();
                        var wkid = spatialRef?.Wkid ?? 4326;

                        // Crear el Feature Dataset en la GDB de salida
                        var createParams = Geoprocessing.MakeValueArray(
                            outputGdbPath,
                            datasetName,
                            wkid);

                        var result = Geoprocessing.ExecuteToolAsync(
                            "management.CreateFeatureDataset",
                            createParams,
                            null,
                            CancelableProgressor.None,
                            GPExecuteToolFlags.None).Result;

                        if (result.IsFailed)
                        {
                            var err = result.ErrorMessages != null && result.ErrorMessages.Any()
                                ? string.Join("; ", result.ErrorMessages.Select(m => m.Text))
                                : "CreateFeatureDataset falló sin detalles";
                            throw new Exception(err);
                        }
                    }
                });

                return (true, "Feature Dataset creado");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<string> ClipFeatureClassAsync(string sourceFcPath, string maskFcPath, string outputFcPath)
        {
            var fcName = System.IO.Path.GetFileName(outputFcPath);
            
            try
            {
                // Eliminar si existe
                if (System.IO.File.Exists(outputFcPath) || System.IO.Directory.Exists(outputFcPath))
                {
                    try
                    {
                        var delParams = Geoprocessing.MakeValueArray(outputFcPath);
                        await Geoprocessing.ExecuteToolAsync("management.Delete", delParams);
                    }
                    catch { }
                }

                var clipParams = Geoprocessing.MakeValueArray(
                    sourceFcPath,
                    maskFcPath,
                    outputFcPath);

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
                    return $"❌ {fcName}: {err}";
                }
                return $"✓ {fcName}";
            }
            catch (Exception ex)
            {
                return $"❌ {fcName}: {ex.Message}";
            }
        }
    }
}
