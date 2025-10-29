#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace EAABAddIn.Src.Application.UseCases
{
    /// <summary>
    /// Caso de uso para migrar líneas y puntos de alcantarillado desde origen a la GDB de migración
    /// Basado en el script Python migra_l_alc y migra_p_alc
    /// </summary>
    public class MigrateAlcantarilladoUseCase
    {
        public async Task<(bool ok, string message)> MigrateLines(string sourceLineasPath, string targetGdbPath)
        {
            return await QueuedTask.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(sourceLineasPath) || string.IsNullOrWhiteSpace(targetGdbPath))
                        return (false, "Parámetros inválidos");

                    if (!Directory.Exists(targetGdbPath))
                        return (false, "La GDB de destino no existe");

                    using var sourceGdb = OpenGeodatabase(sourceLineasPath, out var sourceFcName);
                    if (sourceGdb == null)
                        return (false, $"No se pudo abrir la fuente: {sourceLineasPath}");

                    using var sourceFC = sourceGdb.OpenDataset<FeatureClass>(sourceFcName);
                    if (sourceFC == null)
                        return (false, $"No se pudo abrir el feature class: {sourceFcName}");

                    using var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath)));
                    
                    int migrated = 0;

                    // Leer los features de origen
                    using var cursor = sourceFC.Search();
                    while (cursor.MoveNext())
                    {
                        using var feature = cursor.Current as Feature;
                        if (feature == null) continue;

                        // Obtener el subtipo para determinar la capa destino
                        var subtipo = GetFieldValue<int>(feature, "SUBTIPO");
                        var tipoSistema = GetFieldValue<string>(feature, "DOMTIPOSISTEMA");
                        
                        // Determinar la capa destino según el subtipo
                        string targetClassName = GetTargetLineClassName(subtipo, tipoSistema);
                        if (string.IsNullOrEmpty(targetClassName))
                            continue;

                        // Migrar el feature
                        if (MigrateLineFeature(feature, targetGdb, targetClassName, subtipo))
                            migrated++;
                    }

                    return (true, $"Se migraron {migrated} líneas de alcantarillado");
                }
                catch (Exception ex)
                {
                    return (false, $"Error: {ex.Message}");
                }
            });
        }

        public async Task<(bool ok, string message)> MigratePoints(string sourcePuntosPath, string targetGdbPath)
        {
            return await QueuedTask.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(sourcePuntosPath) || string.IsNullOrWhiteSpace(targetGdbPath))
                        return (false, "Parámetros inválidos");

                    if (!Directory.Exists(targetGdbPath))
                        return (false, "La GDB de destino no existe");

                    using var sourceGdb = OpenGeodatabase(sourcePuntosPath, out var sourceFcName);
                    if (sourceGdb == null)
                        return (false, $"No se pudo abrir la fuente: {sourcePuntosPath}");

                    using var sourceFC = sourceGdb.OpenDataset<FeatureClass>(sourceFcName);
                    if (sourceFC == null)
                        return (false, $"No se pudo abrir el feature class: {sourceFcName}");

                    using var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath)));
                    
                    int migrated = 0;

                    // Leer los features de origen
                    using var cursor = sourceFC.Search();
                    while (cursor.MoveNext())
                    {
                        using var feature = cursor.Current as Feature;
                        if (feature == null) continue;

                        // Obtener el subtipo para determinar la capa destino
                        var subtipo = GetFieldValue<int>(feature, "SUBTIPO");
                        var tipoSistema = GetFieldValue<string>(feature, "DOMTIPOSISTEMA");
                        
                        // Determinar la capa destino según el subtipo
                        string targetClassName = GetTargetPointClassName(subtipo, tipoSistema);
                        if (string.IsNullOrEmpty(targetClassName))
                            continue;

                        // Migrar el feature
                        if (MigratePointFeature(feature, targetGdb, targetClassName, subtipo))
                            migrated++;
                    }

                    return (true, $"Se migraron {migrated} puntos de alcantarillado");
                }
                catch (Exception ex)
                {
                    return (false, $"Error: {ex.Message}");
                }
            });
        }

        #region Helpers

        private Geodatabase? OpenGeodatabase(string featureClassPath, out string fcName)
        {
            fcName = string.Empty;
            try
            {
                // Extraer la ruta de la GDB y el nombre del feature class
                var parts = featureClassPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                var gdbIndex = Array.FindIndex(parts, p => p.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase));
                
                if (gdbIndex < 0)
                    return null;

                var gdbPath = string.Join("\\", parts.Take(gdbIndex + 1));
                fcName = parts.Length > gdbIndex + 1 ? parts[gdbIndex + 1] : "";

                return new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(Path.GetFullPath(gdbPath))));
            }
            catch
            {
                return null;
            }
        }

        private T? GetFieldValue<T>(Feature feature, string fieldName)
        {
            try
            {
                var fieldIndex = feature.FindField(fieldName);
                if (fieldIndex < 0)
                    return default;

                var value = feature[fieldIndex];
                if (value == null || value is DBNull)
                    return default;

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        private string GetTargetLineClassName(int subtipo, string? tipoSistema)
        {
            // Según el script Python:
            // redLocal_1, redTroncal_2, linLat_3
            // Determinar prefijo según tipoSistema: 'als_' para sanitario/combinado, 'alp_' para pluvial
            string prefix = (tipoSistema == "0" || tipoSistema == "2") ? "als_" : "alp_";

            return subtipo switch
            {
                1 => prefix + "RedLocal",
                2 => prefix + "RedTroncal",
                3 => prefix + "LineaLateral",
                _ => string.Empty
            };
        }

        private string GetTargetPointClassName(int subtipo, string? tipoSistema)
        {
            // Según el script Python:
            // ESTRUCTURA_RED_1, POZO_2, SUMIDERO_3, CAJA_DOMICILIARIA_4, SECCION_TRANSVERSAL_5
            string prefix = (tipoSistema == "0" || tipoSistema == "2") ? "als_" : "alp_";

            return subtipo switch
            {
                1 => prefix + "EstructuraRed",
                2 => prefix + "Pozo",
                3 => prefix + "Sumidero",
                4 => prefix + "CajaDomiciliaria",
                5 => prefix + "SeccionTransversal",
                _ => string.Empty
            };
        }

        private bool MigrateLineFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo)
        {
            try
            {
                using var targetFC = targetGdb.OpenDataset<FeatureClass>(targetClassName);
                if (targetFC == null)
                    return false;

                var geometry = sourceFeature.GetShape();
                if (geometry == null || geometry.IsEmpty)
                    return false;

                // Crear el feature en destino
                using var featureClassDef = targetFC.GetDefinition();
                var attributes = BuildLineAttributes(sourceFeature, featureClassDef, subtipo);

                using var rowBuffer = targetFC.CreateRowBuffer();
                
                // Setear geometría
                rowBuffer[featureClassDef.GetShapeField()] = geometry;

                // Setear atributos
                foreach (var attr in attributes)
                {
                    var fieldIndex = rowBuffer.FindField(attr.Key);
                    if (fieldIndex >= 0)
                        rowBuffer[fieldIndex] = attr.Value ?? DBNull.Value;
                }

                using var row = targetFC.CreateRow(rowBuffer);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool MigratePointFeature(Feature sourceFeature, Geodatabase targetGdb, string targetClassName, int subtipo)
        {
            try
            {
                using var targetFC = targetGdb.OpenDataset<FeatureClass>(targetClassName);
                if (targetFC == null)
                    return false;

                var geometry = sourceFeature.GetShape();
                if (geometry == null || geometry.IsEmpty)
                    return false;

                // Crear el feature en destino
                using var featureClassDef = targetFC.GetDefinition();
                var attributes = BuildPointAttributes(sourceFeature, featureClassDef, subtipo);

                using var rowBuffer = targetFC.CreateRowBuffer();
                
                // Setear geometría
                rowBuffer[featureClassDef.GetShapeField()] = geometry;

                // Setear atributos
                foreach (var attr in attributes)
                {
                    var fieldIndex = rowBuffer.FindField(attr.Key);
                    if (fieldIndex >= 0)
                        rowBuffer[fieldIndex] = attr.Value ?? DBNull.Value;
                }

                using var row = targetFC.CreateRow(rowBuffer);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Dictionary<string, object?> BuildLineAttributes(Feature source, FeatureClassDefinition targetDef, int subtipo)
        {
            // Mapear campos según el script Python
            var attrs = new Dictionary<string, object?>();

            // Campos comunes para todas las líneas de alcantarillado
            attrs["SUBTIPO"] = subtipo;
            attrs["DOMDIAMETRONOMINAL"] = GetFieldValue<string>(source, "DOMDIAMETRONOMINAL");
            attrs["DOMMATERIAL"] = GetFieldValue<string>(source, "DOMMATERIAL");
            attrs["DOMMATERIALESPPUBLICO"] = GetFieldValue<string>(source, "DOMMATERIALESPPUBLICO");
            attrs["DOMTIPOSISTEMA"] = GetFieldValue<string>(source, "DOMTIPOSISTEMA");
            attrs["COTARASANTEINICIAL"] = GetFieldValue<double?>(source, "COTARASANTEINICIAL");
            attrs["COTACLAVEINICIAL"] = GetFieldValue<double?>(source, "COTACLAVEINICIAL");
            attrs["COTABATEAINICIAL"] = GetFieldValue<double?>(source, "COTABATEAINICIAL");
            attrs["COTARASANTEFINAL"] = GetFieldValue<double?>(source, "COTARASANTEFINAL");
            attrs["COTACLAVEFINAL"] = GetFieldValue<double?>(source, "COTACLAVEFINAL");
            attrs["COTABATEAFINAL"] = GetFieldValue<double?>(source, "COTABATEAFINAL");
            attrs["FECHAINSTALACION"] = GetFieldValue<DateTime?>(source, "FECHAINSTALACION");
            attrs["DOMESTADOENRED"] = GetFieldValue<string>(source, "DOMESTADOENRED");
            attrs["DOMCALIDADDATO"] = GetFieldValue<string>(source, "DOMCALIDADDATO");
            attrs["DOMESTADOLEGAL"] = GetFieldValue<string>(source, "DOMESTADOLEGAL");
            attrs["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERVACIONES");
            attrs["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_ID");
            attrs["LONGITUD_M"] = GetFieldValue<double?>(source, "LONGITUD_M");
            attrs["DISENO_ID"] = GetFieldValue<string>(source, "DISENO_ID");

            // Campos adicionales según el subtipo
            if (subtipo == 1 || subtipo == 2) // RedLocal o RedTroncal
            {
                attrs["DOMMATERIAL2"] = GetFieldValue<string>(source, "DOMMATERIAL2");
                attrs["NUMEROCONDUCTOS"] = GetFieldValue<int?>(source, "NUMEROCONDUCTOS");
                attrs["DOMTIPOSECCION"] = GetFieldValue<string>(source, "DOMTIPOSECCION");
                attrs["DOMCAMARACAIDA"] = GetFieldValue<string>(source, "DOMCAMARACAIDA");
                attrs["BASE"] = GetFieldValue<double?>(source, "BASE");
                attrs["ALTURA1"] = GetFieldValue<double?>(source, "ALTURA1");
                attrs["DOMMETODOINSTALACION"] = GetFieldValue<string>(source, "DOMMETODOINSTALACION");
                attrs["PROFUNDIDADMEDIA"] = GetFieldValue<double?>(source, "PROFUNDIDADMEDIA");
                attrs["PENDIENTE"] = GetFieldValue<double?>(source, "PENDIENTE");
            }

            if (subtipo == 2) // Solo RedTroncal
            {
                attrs["ALTURA2"] = GetFieldValue<double?>(source, "ALTURA2");
                attrs["TALUD1"] = GetFieldValue<double?>(source, "TALUD1");
                attrs["TALUD2"] = GetFieldValue<double?>(source, "TALUD2");
                attrs["ANCHOBERMA"] = GetFieldValue<double?>(source, "ANCHOBERMA");
                attrs["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
            }

            return attrs;
        }

        private Dictionary<string, object?> BuildPointAttributes(Feature source, FeatureClassDefinition targetDef, int subtipo)
        {
            // Mapear campos según el script Python
            var attrs = new Dictionary<string, object?>();

            // Campos según el subtipo
            if (subtipo == 1) // ESTRUCTURA_RED
            {
                attrs["SUBTIPO"] = subtipo;
                attrs["DOMTIPOSISTEMA"] = GetFieldValue<string>(source, "DOMTIPOSISTEMA");
                attrs["COTARASANTE"] = GetFieldValue<double?>(source, "COTARASANTE");
                attrs["DOMMATERIAL"] = GetFieldValue<string>(source, "DOMMATERIAL");
                attrs["FECHAINSTALACION"] = GetFieldValue<DateTime?>(source, "FECHAINSTALACION");
                attrs["DOMESTADOENRED"] = GetFieldValue<string>(source, "DOMESTADOENRED");
                attrs["DOMCALIDADDATO"] = GetFieldValue<string>(source, "DOMCALIDADDATO");
                attrs["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERVACIONES");
                attrs["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_ID");
                attrs["DIRECCION"] = GetFieldValue<string>(source, "DIRECCION");
                attrs["LOCALIZACIONRELATIVA"] = GetFieldValue<string>(source, "LOCALIZACIONRELATIVA");
                attrs["ROTACIONSIMBOLO"] = GetFieldValue<double?>(source, "ROTACIONSIMBOLO");
                attrs["DOMTIENECABEZAL"] = GetFieldValue<string>(source, "DOMTIENECABEZAL");
                attrs["DOMESTADOFISICO"] = GetFieldValue<string>(source, "DOMESTADOFISICO");
                attrs["DOMTIPOVALVULAANTIRREFLUJO"] = GetFieldValue<string>(source, "DOMTIPOVALVULAANTIRREFLUJO");
                attrs["COTAFONDO"] = GetFieldValue<double?>(source, "COTAFONDO");
                attrs["COTACRESTA"] = GetFieldValue<double?>(source, "COTACRESTA");
                attrs["COTATECHOVERTEDERO"] = GetFieldValue<double?>(source, "COTATECHOVERTEDERO");
                attrs["LONGVERTEDERO"] = GetFieldValue<double?>(source, "LONGVERTEDERO");
                attrs["LARGOESTRUCTURA"] = GetFieldValue<double?>(source, "LARGOESTRUCTURA");
                attrs["ANCHOESTRUCTURA"] = GetFieldValue<double?>(source, "ANCHOESTRUCTURA");
                attrs["ALTOESTRUCTURA"] = GetFieldValue<double?>(source, "ALTOESTRUCTURA");
                attrs["CAUDALBOMBEO"] = GetFieldValue<double?>(source, "CAUDALBOMBEO");
                attrs["DOMTIPOBOMBEO"] = GetFieldValue<string>(source, "DOMTIPOBOMBEO");
                attrs["UNIDADESBOMBEO"] = GetFieldValue<int?>(source, "UNIDADESBOMBEO");
                attrs["ALTURABOMBEO"] = GetFieldValue<double?>(source, "ALTURABOMBEO");
                attrs["COTABOMBEO"] = GetFieldValue<double?>(source, "COTABOMBEO");
                attrs["VOLUMENBOMBEO"] = GetFieldValue<double?>(source, "VOLUMENBOMBEO");
                attrs["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
                attrs["DISENO_ID"] = GetFieldValue<string>(source, "DISENO_ID");
            }
            else if (subtipo == 2) // POZO
            {
                attrs["SUBTIPO"] = subtipo;
                attrs["DOMTIPOSISTEMA"] = GetFieldValue<string>(source, "DOMTIPOSISTEMA");
                attrs["COTARASANTE"] = GetFieldValue<double?>(source, "COTARASANTE");
                attrs["FECHAINSTALACION"] = GetFieldValue<DateTime?>(source, "FECHAINSTALACION");
                attrs["DOMESTADOENRED"] = GetFieldValue<string>(source, "DOMESTADOENRED");
                attrs["DOMCALIDADDATO"] = GetFieldValue<string>(source, "DOMCALIDADDATO");
                attrs["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERVACIONES");
                attrs["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_ID");
                attrs["DIRECCION"] = GetFieldValue<string>(source, "DIRECCION");
                attrs["DOMESTADOFISICO"] = GetFieldValue<string>(source, "DOMESTADOFISICO");
                attrs["COTATERRENO"] = GetFieldValue<double?>(source, "COTATERRENO");
                attrs["COTAFONDO"] = GetFieldValue<double?>(source, "COTAFONDO");
                attrs["PROFUNDIDAD"] = GetFieldValue<double?>(source, "PROFUNDIDAD");
                attrs["DOMINICIALVARIASCUENCAS"] = GetFieldValue<string>(source, "DOMINICIALVARIASCUENCAS");
                attrs["DOMCAMARASIFON"] = GetFieldValue<string>(source, "DOMCAMARASIFON");
                attrs["DOMESTADOPOZO"] = GetFieldValue<string>(source, "DOMESTADOPOZO");
                attrs["DOMTIPOALMACENAMIENTO"] = GetFieldValue<string>(source, "DOMTIPOALMACENAMIENTO");
                attrs["DISENO_ID"] = GetFieldValue<string>(source, "DISENO_ID");
            }
            else if (subtipo == 3 || subtipo == 4) // SUMIDERO o CAJA_DOMICILIARIA
            {
                attrs["SUBTIPO"] = subtipo;
                attrs["DOMTIPOSISTEMA"] = GetFieldValue<string>(source, "DOMTIPOSISTEMA");
                attrs["COTARASANTE"] = GetFieldValue<double?>(source, "COTARASANTE");
                attrs["DOMMATERIAL"] = GetFieldValue<string>(source, "DOMMATERIAL");
                attrs["FECHAINSTALACION"] = GetFieldValue<DateTime?>(source, "FECHAINSTALACION");
                attrs["DOMESTADOENRED"] = GetFieldValue<string>(source, "DOMESTADOENRED");
                attrs["DOMCALIDADDATO"] = GetFieldValue<string>(source, "DOMCALIDADDATO");
                attrs["OBSERVACIONES"] = GetFieldValue<string>(source, "OBSERVACIONES");
                attrs["CONTRATO_ID"] = GetFieldValue<string>(source, "CONTRATO_ID");
                attrs["DIRECCION"] = GetFieldValue<string>(source, "DIRECCION");
                attrs["LOCALIZACIONRELATIVA"] = GetFieldValue<string>(source, "LOCALIZACIONRELATIVA");
                attrs["ROTACIONSIMBOLO"] = GetFieldValue<double?>(source, "ROTACIONSIMBOLO");
                attrs["DISENO_ID"] = GetFieldValue<string>(source, "DISENO_ID");
            }
            else if (subtipo == 5) // SECCION_TRANSVERSAL
            {
                attrs["NOMBRE"] = GetFieldValue<string>(source, "NOMBRE");
                attrs["ABSCISA"] = GetFieldValue<double?>(source, "ABSCISA");
                attrs["DISTANCIADESDEORIGEN"] = GetFieldValue<double?>(source, "DISTANCIADESDEORIGEN");
                attrs["DOMORIGENSECCION"] = GetFieldValue<string>(source, "DOMORIGENSECCION");
            }

            return attrs;
        }

        #endregion
    }
}
