using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using ArcGIS.Core.Data;

using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Domain.Repositories;

public interface IPtAddressGralEntityRepository
{
    List<PtAddressGralEntity> FindByCityCodeAndAddresses(string cityCode, string address);

    List<PtAddressGralEntity> GetAllCities();

    List<PtAddressGralEntity> FindByAddress(string address);
}

public abstract class PtAddressGralEntityRepositoryBase
{
    protected List<PtAddressGralEntity> Find(
        string tableName,
        string cityCode,
        string address,
        string fieldId,
        string fieldFullEAAB,
        string fieldFullCad,
        string fieldFullOld,
        string fieldLat,
        string fieldLon)
    {
        var result = new List<PtAddressGralEntity>();
        var geodatabase = Module1.DatabaseConnection.Geodatabase;
        string safeAddress = (address ?? string.Empty).Replace("'", "''").Trim();
        bool hasCity = !string.IsNullOrWhiteSpace(cityCode);

        string cityPart = hasCity ? $"CITY_CODE = '{cityCode.Replace("'", "''")}' AND " : string.Empty;
        string exactWhereCore =
            $"({fieldFullEAAB} = '{safeAddress}' OR {fieldFullCad} = '{safeAddress}' OR {fieldFullOld} = '{safeAddress}')";
        string whereClauseExact = cityPart + exactWhereCore;

        string postfixClause =
            "ORDER BY CASE " +
            $"WHEN {fieldFullEAAB} = '{safeAddress}' THEN 1 " +
            $"WHEN {fieldFullCad} = '{safeAddress}' THEN 2 " +
            $"WHEN {fieldFullOld} = '{safeAddress}' THEN 3 " +
            "END";

        QueryFilter BuildFilter(string wc) => new QueryFilter
        {
            SubFields = $"{fieldId}, {fieldFullEAAB}, {fieldFullCad}, {fieldFullOld}, {fieldLat}, {fieldLon}, CITY_CODE, CITY_DESC, SOURCE",
            WhereClause = wc,
            PostfixClause = postfixClause
        };

        void Execute(QueryFilter qf)
        {
            using var table = geodatabase.OpenDataset<Table>(tableName);
            using var cursor = table.Search(qf, false);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                result.Add(MapRowToEntity(row));
            }
        }

        var qfExact = BuildFilter(whereClauseExact);
        Execute(qfExact);

        if (result.Count == 0 && safeAddress.Length > 3)
        {
            string like = safeAddress.ToUpper();
            string likeWhereCore =
                $"(UPPER({fieldFullEAAB}) LIKE '%{like}%' OR UPPER({fieldFullCad}) LIKE '%{like}%' OR UPPER({fieldFullOld}) LIKE '%{like}%')";
            string whereClauseLike = cityPart + likeWhereCore;
            var qfLike = BuildFilter(whereClauseLike);
            Execute(qfLike);
        }

        return result;
    }

    protected abstract PtAddressGralEntity MapRowToEntity(Row row);

    protected decimal? ToDecimal(object value)
    {
        if (value == null || value is DBNull) return null;
        if (value is decimal d) return d;
        if (value is double db) return Convert.ToDecimal(db);
        if (value is float f) return Convert.ToDecimal(f);
        if (value is long l) return Convert.ToDecimal(l);
        if (value is int i) return Convert.ToDecimal(i);

        var s = value.ToString();
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }
}

public class PtAddressGralOracleRepository : PtAddressGralEntityRepositoryBase, IPtAddressGralEntityRepository
{
    public List<PtAddressGralEntity> FindByCityCodeAndAddresses(string cityCode, string address)
    {
        return Find(
            "sgo.sgo_pt_address_gral",
            cityCode,
            address,
            "OBJECTID",
            "FULL_ADDRESS_EAAB",
            "FULL_ADDRESS_CADASTRE",
            "FULL_ADDRESS_OLD",
            "LATITUD",
            "LONGITUD"
        );
    }

    public List<PtAddressGralEntity> GetAllCities()
    {
        var result = new List<PtAddressGralEntity>();
        var geodatabase = Module1.DatabaseConnection.Geodatabase;

        using (var table = geodatabase.OpenDataset<Table>("sgo.sgo_pt_address_gral"))
        {
            var queryFilter = new QueryFilter
            {
                SubFields = "CITY_CODE, CITY_DESC",
                WhereClause = "CITY_CODE IS NOT NULL AND CITY_DESC IS NOT NULL",
                PostfixClause = "GROUP BY CITY_CODE, CITY_DESC ORDER BY CITY_DESC"
            };

            using (var cursor = table.Search(queryFilter, false))
            {
                while (cursor.MoveNext())
                {
                    using (var row = cursor.Current)
                    {
                        result.Add(new PtAddressGralEntity
                        {
                            CityCode = row["CITY_CODE"]?.ToString(),
                            CityDesc = row["CITY_DESC"]?.ToString()
                        });
                    }
                }
            }
        }
        return result;
    }

    public List<PtAddressGralEntity> FindByAddress(string address)
    {
        return Find(
            "sgo.sgo_pt_address_gral",
            null, // sin filtro de ciudad
            address,
            "OBJECTID",
            "FULL_ADDRESS_EAAB",
            "FULL_ADDRESS_CADASTRE",
            "FULL_ADDRESS_OLD",
            "LATITUD",
            "LONGITUD"
        );
    }

    protected override PtAddressGralEntity MapRowToEntity(Row row)
    {
        var id = row["OBJECTID"];

        return new PtAddressGralEntity
        {
            ID = long.TryParse(id?.ToString(), out var parsedId) ? parsedId : 0,
            CityCode = row["CITY_CODE"]?.ToString(),
            CityDesc = row["CITY_DESC"]?.ToString(),
            LocaCode = row["LOCALITIE_CODE"]?.ToString(),
            LocaDesc = row["LOCALITIE_DESC"]?.ToString(),
            NeighborhoodCode = row["NEIGHBORHOOD_CODE"]?.ToString(),
            NeighborhoodDesc = row["NEIGHBORHOOD_DESC"]?.ToString(),
            MainStreet = row["MAIN_STREET"]?.ToString(),
            MainStreetSide = row["MAIN_STREET_SIDE"]?.ToString(),
            MainStreetName = row["MAIN_STREET_NAME"]?.ToString(),
            SecondStreet = row["SECOND_STREET"]?.ToString(),
            SecondStreetSide = row["SECOND_STREET_SIDE"]?.ToString(),
            SecondStreetName = row["SECOND_STREET_NAME"]?.ToString(),
            HouseNumber = row["HOUSE_NUMBER"]?.ToString(),
            RoadSection = row["ROAD_SECTION"]?.ToString(),
            PointType = row["POINT_TYPE"]?.ToString(),
            FullAddressEAAB = row["FULL_ADDRESS_EAAB"]?.ToString(),
            FullAddressCadastre = row["FULL_ADDRESS_CADASTRE"]?.ToString(),
            FullAddressOld = row["FULL_ADDRESS_OLD"]?.ToString(),
            Longitud = ToDecimal(row["LONGITUD"]),
            Latitud = ToDecimal(row["LATITUD"]),
            HydraulicDistrictCode = row["HYDRAULIC_DISTRICT_CODE"]?.ToString(),
            HydraulicDistrictDescription = row["HYDRAULIC_DISTRICT_DESC"]?.ToString(),
            ZoneCode = row["ZONE_CODE"]?.ToString(),
            ZoneDesc = row["ZONE_DESC"]?.ToString(),
            SanitaryUgaCode = row["SANITARY_UGA_CODE"]?.ToString(),
            SanitaryUgaDesc = row["SANITARY_UGA_DESC"]?.ToString(),
            StormUgaCode = row["STORM_UGA_CODE"]?.ToString(),
            StormUgaDesc = row["STORM_UGA_DESC"]?.ToString(),
            GridH3Index = row["GRID_H3_INDEX"]?.ToString(),
            ZipCode = row["ZIP_CODE"]?.ToString(),
            Source = "EAAB",
            Score = null,
            ScoreText = "Exacta"
        };
    }
}

public class PtAddressGralPostgresRepository : PtAddressGralEntityRepositoryBase, IPtAddressGralEntityRepository
{
    public List<PtAddressGralEntity> FindByCityCodeAndAddresses(string cityCode, string address)
    {
        return Find(
            "public.sgo_pt_address_gral",
            cityCode,
            address,
            "id",
            "full_address_eaab",
            "full_address_cadastre",
            "full_address_old",
            "latitud",
            "longitud"
        );
    }

    public List<PtAddressGralEntity> GetAllCities()
    {
        var result = new List<PtAddressGralEntity>();
        var geodatabase = Module1.DatabaseConnection.Geodatabase;

        using (var table = geodatabase.OpenDataset<Table>("public.sgo_pt_address_gral"))
        {
            var queryFilter = new QueryFilter
            {
                SubFields = "city_code, city_desc",
                WhereClause = "city_code IS NOT NULL AND city_desc IS NOT NULL",
                PostfixClause = "GROUP BY city_code, city_desc ORDER BY city_desc"
            };

            using (var cursor = table.Search(queryFilter, false))
            {
                while (cursor.MoveNext())
                {
                    using (var row = cursor.Current)
                    {
                        result.Add(new PtAddressGralEntity
                        {
                            CityCode = row["city_code"]?.ToString(),
                            CityDesc = row["city_desc"]?.ToString()
                        });
                    }
                }
            }
        }
        return result;
    }

    public List<PtAddressGralEntity> FindByAddress(string address)
    {
        return Find(
            "public.sgo_pt_address_gral",
            null,
            address,
            "id",
            "full_address_eaab",
            "full_address_cadastre",
            "full_address_old",
            "latitud",
            "longitud"
        );
    }

    protected override PtAddressGralEntity MapRowToEntity(Row row)
    {
        var id = row["id"];

        return new PtAddressGralEntity
        {
            ID = long.TryParse(id?.ToString(), out var parsedId) ? parsedId : 0,
            CityCode = row["city_code"]?.ToString(),
            CityDesc = row["city_desc"]?.ToString(),
            LocaCode = row["localitie_code"]?.ToString(),
            LocaDesc = row["localitie_desc"]?.ToString(),
            NeighborhoodCode = row["neighborhood_code"]?.ToString(),
            NeighborhoodDesc = row["neighborhood_desc"]?.ToString(),
            MainStreet = row["main_street"]?.ToString(),
            MainStreetSide = row["main_street_side"]?.ToString(),
            MainStreetName = row["main_street_name"]?.ToString(),
            SecondStreet = row["second_street"]?.ToString(),
            SecondStreetSide = row["second_street_side"]?.ToString(),
            SecondStreetName = row["second_street_name"]?.ToString(),
            HouseNumber = row["house_number"]?.ToString(),
            RoadSection = row["road_section"]?.ToString(),
            PointType = row["point_type"]?.ToString(),
            FullAddressEAAB = row["full_address_eaab"]?.ToString(),
            FullAddressCadastre = row["full_address_cadastre"]?.ToString(),
            FullAddressOld = row["full_address_old"]?.ToString(),
            Longitud = ToDecimal(row["longitud"]),
            Latitud = ToDecimal(row["latitud"]),
            HydraulicDistrictCode = row["hydraulic_district_code"]?.ToString(),
            HydraulicDistrictDescription = row["hydraulic_district_desc"]?.ToString(),
            ZoneCode = row["zone_code"]?.ToString(),
            ZoneDesc = row["zone_desc"]?.ToString(),
            SanitaryUgaCode = row["sanitary_uga_code"]?.ToString(),
            SanitaryUgaDesc = row["sanitary_uga_desc"]?.ToString(),
            StormUgaCode = row["storm_uga_code"]?.ToString(),
            StormUgaDesc = row["storm_uga_desc"]?.ToString(),
            GridH3Index = row["grid_h3_index"]?.ToString(),
            ZipCode = row["zip_code"]?.ToString(),
            Source = "EAAB",
            Score = null,
            ScoreText = "Exacta"
        };
    }
}
