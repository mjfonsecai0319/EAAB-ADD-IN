#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

using EAABAddIn.Src.Application.Utils;

namespace EAABAddIn.Src.Application.UseCases;

public class GetClientsCountUseCase
{
    public async Task<int> Invoke(Feature feature, string? classPath)
    {
        if (feature == null || string.IsNullOrWhiteSpace(classPath))
            return 0;

        var result = await QueuedTask.Run(() => InvokeInternal(feature, classPath));
        return result;
    }

    private int InvokeInternal(Feature feature, string classPath)
    {
        var map = MapView.Active?.Map;
        if (map == null) return 0;

        var geo = feature.GetShape();
        if (geo == null) return 0;

        using var clientsFc = FeatureClassUtils.OpenFeatureClass(classPath);
        if (clientsFc == null) return 0;

        var clientsTable = clientsFc as Table;
        if (clientsTable == null) return 0;

        int totalCount = 0;

        try
        {
            var def = clientsTable.GetDefinition();
            var fields = def.GetFields();
            string oidFld = def.GetObjectIDField();
            string tipoServicioFld = fields.FirstOrDefault(f => f.Name.Equals("TIPOSERVICIOAC", StringComparison.OrdinalIgnoreCase))?.Name ?? "TIPOSERVICIOAC";
            string clasificacionFld = fields.FirstOrDefault(f => f.Name.Equals("DOMCLASIFICACIONPREDIO", StringComparison.OrdinalIgnoreCase))?.Name ?? "DOMCLASIFICACIONPREDIO";

            bool tipoServicioEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(tipoServicioFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;
            bool clasificacionEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(clasificacionFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;

            string whereTipo = tipoServicioEsNumerico ? $"{tipoServicioFld} = 10" : $"{tipoServicioFld} = '10'";
            string whereClasif;
            if (clasificacionEsNumerico)
                whereClasif = $"{clasificacionFld} IN (1,4,6)";
            else
                whereClasif = $"{clasificacionFld} IN ('1','4','6')";

            var filter = new SpatialQueryFilter
            {
                SpatialRelationship = SpatialRelationship.Intersects,
                FilterGeometry = geo,
                SubFields = oidFld,
                WhereClause = $"{whereTipo} AND {whereClasif}"
            };

            using var cursor = clientsTable.Search(filter, true);
            while (cursor.MoveNext())
            {
                totalCount++;
            }
        }
        catch
        {
            return totalCount;
        }

        return totalCount;
    }
}