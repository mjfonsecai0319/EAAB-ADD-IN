using System;
using System.Collections.Generic;
using System.Globalization;
using ArcGIS.Core.Data;
using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Domain.Repositories
{
    public interface IPtAddressGralEntityRepository
    {
        List<PtAddressGralEntity> FindByCityCodeAndAddresses(
            DatabaseConnectionProperties props, string cityCode, string address
        );

        List<PtAddressGralEntity> GetAllCities(DatabaseConnectionProperties props);

        List<PtAddressGralEntity> FindByAddress(DatabaseConnectionProperties props, string address);

    }

    public abstract class PtAddressGralEntityRepositoryBase
    {
        protected List<PtAddressGralEntity> Find(DatabaseConnectionProperties props, string tableName, string whereClause)
        {
            var result = new List<PtAddressGralEntity>();
            var geodatabase = Module1.DatabaseConnection.Geodatabase;

            using (var table = geodatabase.OpenDataset<Table>(tableName))
            {
                var queryFilter = new QueryFilter { WhereClause = whereClause };
                using (var cursor = table.Search(queryFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        using (var row = cursor.Current)
                        {
                            result.Add(MapRowToEntity(row));
                        }
                    }
                }
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
        public List<PtAddressGralEntity> FindByCityCodeAndAddresses(
            DatabaseConnectionProperties props, string cityCode, string address
        )
        {
            string whereClause =
                $"city_code = '{cityCode}' AND (full_address_cadastre = '{address}' OR full_address_eaab = '{address}' OR full_address_old = '{address}')";
            return Find(props, "sgo.sgo_pt_address_gral", whereClause);
        }

        public List<PtAddressGralEntity> GetAllCities(DatabaseConnectionProperties props)
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
        public List<PtAddressGralEntity> FindByAddress(DatabaseConnectionProperties props, string address)
        {
            string whereClause =
                $"full_address_cadastre = '{address}' OR full_address_eaab = '{address}' OR full_address_old = '{address}'";
            return Find(props, "sgo.sgo_pt_address_gral", whereClause);
        }

        protected override PtAddressGralEntity MapRowToEntity(Row row)
        {
            return new PtAddressGralEntity
            {
                ID = row["OBJECTID"] is long id ? id : 0,
                Source = row["SOURCE"]?.ToString(),
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
                ZipCode = row["ZIP_CODE"]?.ToString()
            };
        }
    }

    public class PtAddressGralPostgresRepository : PtAddressGralEntityRepositoryBase, IPtAddressGralEntityRepository
    {
        public List<PtAddressGralEntity> FindByCityCodeAndAddresses(
            DatabaseConnectionProperties props, string cityCode, string address
        )
        {
            string whereClause =
                $"city_code = '{cityCode}' AND (full_address_cadastre = '{address}' OR full_address_eaab = '{address}' OR full_address_old = '{address}')";
            return Find(props, "public.sgo_pt_address_gral", whereClause);
        }

        public List<PtAddressGralEntity> GetAllCities(DatabaseConnectionProperties props)
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
        public List<PtAddressGralEntity> FindByAddress(DatabaseConnectionProperties props, string address)
        {
            string whereClause =
                $"full_address_cadastre = '{address}' OR full_address_eaab = '{address}' OR full_address_old = '{address}'";
            return Find(props, "public.sgo_pt_address_gral", whereClause);
        }

        protected override PtAddressGralEntity MapRowToEntity(Row row)
        {
            return new PtAddressGralEntity
            {
                ID = row["id"] is long id ? id : 0,
                Source = row["source"]?.ToString(),
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
                ZipCode = row["zip_code"]?.ToString()
            };
        }
    }
}
