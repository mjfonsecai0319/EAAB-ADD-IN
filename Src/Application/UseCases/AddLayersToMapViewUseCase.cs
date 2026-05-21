#nullable enable

namespace EAABAddIn.Src.Application.UseCases;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Application.Utils;

public class AddLayersToMapViewUseCase
{
    public string TargetGDBPath { get; private set; } = string.Empty;
    public string[] Layers { get; private set; } = Array.Empty<string>();

    private static readonly IEqualityComparer<string> _IgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;


    /// <summary>
    /// Ejecuta la adición de las capas especificadas al mapa activo.
    /// </summary>
    /// <param name="path">Ruta de la geodatabase.</param>
    /// <param name="layers">Nombres de las Feature Classes a agregar.</param>
    /// <returns>true si al menos una capa fue agregada exitosamente.</returns>
    public Task<bool> Invoke(string path, string[] layers)
    {
        TargetGDBPath = path ?? string.Empty;
        Layers = layers ?? Array.Empty<string>();

        return QueuedTask.Run(InternalExecute);
    }

    private bool InternalExecute()
    {
        try
        {
            ValidateInputs();

            var map = CurrentMapViewOrError();
            var layers = new List<string>();
            using var gdb = OpenGeodatabase();

            foreach (var className in Layers)
            {
                var added = AddFeatureLayer(map, gdb, className.Trim());

                if (!added)
                {
                    continue;
                }

                layers.Add(className);
            }

            if (layers.Count > 0)
            {
                return true;
            }

            throw new Errors.BusinessException("Ninguna de las capas especificadas pudo ser agregada.");
        }
        catch (Errors.BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Errors.BusinessException(ex.Message);
        }
    }

    private void ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(TargetGDBPath))
            throw new Errors.BusinessException("La ruta de la geodatabase es requerida.");

        if (!Directory.Exists(TargetGDBPath))
            throw new Errors.BusinessException($"La geodatabase no existe: {TargetGDBPath}");

        if (Layers.Length == 0)
            throw new Errors.BusinessException("Debe especificar al menos una capa.");
    }

    private Map CurrentMapViewOrError()
    {
        var map = MapView.Active?.Map;

        if (map is not null)
            return map;

        throw new Errors.BusinessException("No hay un mapa activo. Abra un mapa en ArcGIS Pro.");
    }

    private Geodatabase OpenGeodatabase()
    {
        var gdb = GeodatabaseUtils.OpenGeodatabase(TargetGDBPath);

        if (gdb is not null)
            return gdb;

        throw new Errors.BusinessException("No se pudo abrir la geodatabase de destino.");
    }

    private bool AddFeatureLayer(Map map, Geodatabase gdb, string className)
    {
        using var FC = FeatureClassUtils.TryOpenFromGeodatabase(gdb, className);

        if (FC != null && FC.GetCount() != 0)
        {
            RemoveExistingLayerIfDifferent(map, className, gdb, FC);

            var @params = new FeatureLayerCreationParams(FC)
            {
                Name = className,
                MapMemberPosition = MapMemberPosition.AddToTop
            };
            var layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(@params, map);

            if (layer != null)
            {
                EnsureLayerProperties(layer);
                return true;
            }
        }

        return false;
    }

    private void RemoveExistingLayerIfDifferent(Map map, string className, Geodatabase gdb, FeatureClass fc)
    {
        var existing = map.GetLayersAsFlattenedList()
            .OfType<FeatureLayer>()
            .FirstOrDefault(l => _IgnoreCaseComparer.Equals(l.Name, className));

        if (existing == null)
            return;

        bool isSameSource = false;
        try
        {
            using var existingFc = existing.GetFeatureClass();
            var existingGdb = existingFc?.GetDatastore() as Geodatabase;
            isSameSource = existingGdb != null &&
                          _IgnoreCaseComparer.Equals(existingGdb.GetPath().LocalPath, gdb.GetPath().LocalPath) &&
                          _IgnoreCaseComparer.Equals(existingFc.GetName(), fc.GetName());
        }
        catch { }

        if (!isSameSource)
            map.RemoveLayer(existing);
        // Si es la misma fuente, dejamos la capa existente (mejor UX)
    }

    private static void EnsureLayerProperties(FeatureLayer layer)
    {
        try
        {
            layer.SetVisibility(true);
            layer.SetDefinitionQuery("");

            if (layer.GetDefinition() is CIMFeatureLayer cim)
            {
                cim.MinScale = 0;
                cim.MaxScale = 0;
                layer.SetDefinition(cim);
            }
        }
        catch { /* Best effort */ }
    }
}