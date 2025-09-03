using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ArcGIS.Core.Data;

using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Core.Data;

class OperacionesSQL
{
    public static List<AddressLexEntity> GetAddressLexEntitiesPostgres(DatabaseConnectionProperties connectionProperties)
        {
            var result = new List<AddressLexEntity>();
            using (var geodatabase = new Geodatabase(connectionProperties))
            using (Table table = geodatabase.OpenDataset<Table>("public.usuarios"))
            using (RowCursor cursor = table.Search(null, false))
            {
                while (cursor.MoveNext())
                {
                    using (Row row = cursor.Current)
                    {
                        var entity = new AddressLexEntity
                        {
                            ID = row["id_usuario"] is long id ? id : 0,
                            Seq = row["nombre"] is long seq ? seq : 0,
                            Word = row["tipo_documento"]?.ToString(),
                            StdWord = row["numero_documento"]?.ToString(),

                        };
                        result.Add(entity);
                    }
                }
            }
            return result;
        }
        public static List<AddressLexEntity> GetAddressLexEntitiesOracle(DatabaseConnectionProperties connectionProperties)
        {
            var result = new List<AddressLexEntity>();
            using (var geodatabase = new Geodatabase(connectionProperties))
            using (Table table = geodatabase.OpenDataset<Table>("sgo.sgo_t_address_lex"))
            using (RowCursor cursor = table.Search(null, false))
            {
                while (cursor.MoveNext())
                {
                    using (Row row = cursor.Current)
                    {
                        var entity = new AddressLexEntity
                        {
                            ID = row["OBJECTID"] is long id ? id : 0,
                            Seq = row["SEQ"] is long seq ? seq : 0,
                            Word = row["WORD"]?.ToString(),
                            StdWord = row["STDWORD"]?.ToString(),
                            Token = row["TOKEN"] as long?,
                            IsCustom = row["IS_CUSTOM"] is bool isCustom ? isCustom : false
                        };
                        result.Add(entity);
                    }
                }
            }
            return result;
        }
}