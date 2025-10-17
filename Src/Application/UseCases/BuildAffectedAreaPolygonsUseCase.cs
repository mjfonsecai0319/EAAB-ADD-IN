#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using EAABAddIn.Src.Application.Utils;

namespace EAABAddIn.Src.Application.UseCases;

public class BuildAffectedAreaPolygonsUseCase
{
    public static readonly int NeighborhoodFieldMaxLength = 4096;

    public async Task<(bool success, string message, int updatedCount)> Invoke(
        Feature feature,
        string neighborhoods,
        int clientsCount,
        string classID
    )
    {
        return await QueuedTask.Run(() => Internal(
            feature,
            neighborhoods,
            clientsCount,
            classID
        ));
    }

    private async Task<(bool success, string message, int updatedCount)> Internal(
        Feature f,
        string neighborhoods,
        int clientsCount,
        string classID
    )
    {
        var featureClass = f.GetTable();

        if (featureClass is null)
        {
            return (false, "La Feature Class no es válida.", 0);
        }
        try
        {
            EnsureFieldExists(featureClass.GetPath().ToString(), "identificador", "TEXT", 255);
            EnsureFieldExists(featureClass.GetPath().ToString(), "barrios", "TEXT", 4096);
            EnsureFieldExists(featureClass.GetPath().ToString(), "clientes", "LONG", 0);
        }
        catch (Exception)
        {
            return (false, "Error asegurando los campos necesarios en la Feature Class.", 0);
        }

        var oid = f.GetObjectID();
        var oidFld = featureClass.GetDefinition().GetObjectIDField();

        using (Row? row = SearchRowByOid(featureClass, oidFld, oid))
        {
            if (row is null)
            {
                return (false, $"OID {oid}: no encontrado en la Feature Class destino", 0);
            }

            var (existingNeighborhoods, existingClients) = GetExistingValues(row);
            string finalNeighborhoods = string.IsNullOrEmpty(neighborhoods) ? existingNeighborhoods : neighborhoods;
            int finalClients = clientsCount == 0 ? existingClients : clientsCount;

            row["identificador"] = classID;
            row["barrios"] = string.IsNullOrEmpty(finalNeighborhoods) ? null : finalNeighborhoods;
            row["clientes"] = (long)finalClients;
            row.Store();
        }

        return (true, "Registro actualizado correctamente.", 1);
    }

    private void EnsureFieldExists(string featureClassPath, string fieldName, string fieldType, int length)
    {
        try
        {
            using var fc = FeatureClassUtils.OpenFeatureClass(featureClassPath);

            if (fc != null)
            {
                var exists = fc.GetDefinition().GetFields().Any(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (exists) return;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error asegurando campo {fieldName} en {featureClassPath}: {ex.Message}");
        }

        try
        {
            if (string.Equals(fieldType, "TEXT", StringComparison.OrdinalIgnoreCase))
            {
                var addFieldParams = Geoprocessing.MakeValueArray(featureClassPath, fieldName, fieldType, "", "", length > 0 ? length : 255);
                Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams).GetAwaiter().GetResult();
            }
            else
            {
                var addFieldParams = Geoprocessing.MakeValueArray(featureClassPath, fieldName, fieldType);
                Geoprocessing.ExecuteToolAsync("management.AddField", addFieldParams).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error asegurando campo {fieldName} en {featureClassPath}: {ex.Message}");
        }
    }

    private Row? SearchRowByOid(FeatureClass fc, string oidFieldName, long oid)
    {
        var qf = new QueryFilter { WhereClause = $"{oidFieldName} = {oid}", SubFields = "*" };
        using var cursor = fc.Search(qf, false);
        if (cursor.MoveNext()) return cursor.Current;
        return null;
    }

    private (string neighborhoods, int clientsCount) GetExistingValues(Row row)
    {
        string neighborhoods = row["barrios"]?.ToString() ?? string.Empty;
        int clientsCount = Convert.ToInt32(row["clientes"] ?? 0);
        return (neighborhoods, clientsCount);
    }
}
