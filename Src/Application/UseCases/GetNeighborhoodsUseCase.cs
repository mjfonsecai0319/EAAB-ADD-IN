using System;

namespace EAABAddIn.Src.Application.UseCases;

public class GetNeighborhoodsUseCase
{

}


// private static string GetNeighborhoodsForPolygon(FeatureClass neighborhoodsFc, string nameField, Polygon poly)
//     {
//         var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
//         var filter = new SpatialQueryFilter
//         {
//             WhereClause = "1=1",
//             SubFields = nameField,
//             SpatialRelationship = SpatialRelationship.Intersects,
//             FilterGeometry = poly
//         };
//         using var cursor = neighborhoodsFc.Search(filter, false);
//         while (cursor.MoveNext())
//         {
//             using var row = cursor.Current;
//             var val = row[nameField]?.ToString();
//             if (!string.IsNullOrWhiteSpace(val)) names.Add(val.Trim());
//         }
//         return string.Join(", ", names.OrderBy(n => n));
//     }

//     private static int GetClientsCountForPolygon(FeatureClass clientsFc, Polygon poly)
//     {
//         int count = 0;

//         var def = clientsFc.GetDefinition();
//         var fields = def.GetFields();
//         string oidFld = def.GetObjectIDField();

//         string tipoServicioFld = fields.FirstOrDefault(f => f.Name.Equals("TIPOSERVICIOAC", StringComparison.OrdinalIgnoreCase))?.Name ?? "TIPOSERVICIOAC";
//         string clasificacionFld = fields.FirstOrDefault(f => f.Name.Equals("DOMCLASIFICACIONPREDIO", StringComparison.OrdinalIgnoreCase))?.Name ?? "DOMCLASIFICACIONPREDIO";

//         bool tipoServicioEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(tipoServicioFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;
//         bool clasificacionEsNumerico = fields.FirstOrDefault(f => f.Name.Equals(clasificacionFld, StringComparison.OrdinalIgnoreCase))?.FieldType is FieldType.Integer or FieldType.SmallInteger or FieldType.BigInteger;

//         string whereTipo = tipoServicioEsNumerico ? $"{tipoServicioFld} = 10" : $"{tipoServicioFld} = '10'";
//         string whereClasif;
//         if (clasificacionEsNumerico)
//             whereClasif = $"{clasificacionFld} IN (1,4,6)";
//         else
//             whereClasif = $"{clasificacionFld} IN ('1','4','6')";

//         var filter = new SpatialQueryFilter
//         {
//             SpatialRelationship = SpatialRelationship.Intersects,
//             FilterGeometry = poly,
//             SubFields = oidFld,
//             WhereClause = $"{whereTipo} AND {whereClasif}"
//         };

//         using var cursor = clientsFc.Search(filter, true);
//         while (cursor.MoveNext())
//         {
//             count++;
//         }
//         return count;
//     }