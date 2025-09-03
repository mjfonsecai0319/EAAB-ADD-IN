using ArcGIS.Core.Data;

using EAABAddIn.Src.Core.Entities;

namespace EAABAddIn.Src.Domain.Repositories;


public interface IAddressLexEntityRepository
{
    AddressLexEntity FindByWord(DatabaseConnectionProperties connectionProperties, string word);
}


public class AddressLexEntityOracleRepository : IAddressLexEntityRepository
{
    public AddressLexEntity FindByWord(DatabaseConnectionProperties connectionProperties, string word)
    {
        using (var geodatabase = new Geodatabase(connectionProperties))
        using (Table table = geodatabase.OpenDataset<Table>("sgo.sgo_t_address_lex"))
        {
            var queryFilter = new QueryFilter
            {
                WhereClause = $"word = '{word}'"
            };

            using (RowCursor cursor = table.Search(queryFilter, false))
            {
                if (cursor.MoveNext())
                {
                    using (Row row = cursor.Current)
                    {
                        return new AddressLexEntity
                        {
                            ID = row["OBJECTID"] is long id ? id : 0,
                            Seq = row["SEQ"] is long seq ? seq : 0,
                            Word = row["WORD"]?.ToString(),
                            StdWord = row["STDWORD"]?.ToString(),
                            Token = row["TOKEN"] as long?,
                            IsCustom = row["IS_CUSTOM"] is bool isCustom ? isCustom : false
                        };
                    }
                }
            }
        }
        return null;
    }
}

public class AddressLexEntityPostgresRepository : IAddressLexEntityRepository
{
    public AddressLexEntity FindByWord(DatabaseConnectionProperties connectionProperties, string word)
    {
        using (var geodatabase = new Geodatabase(connectionProperties))
        using (Table table = geodatabase.OpenDataset<Table>("public.eaab_lex"))
        {
            var queryFilter = new QueryFilter
            {
                WhereClause = $"word = '{word}'"
            };

            using (RowCursor cursor = table.Search(queryFilter, false))
            {
                if (cursor.MoveNext())
                {
                    using (Row row = cursor.Current)
                    {
                        return new AddressLexEntity
                        {
                            ID = row["id"] is long id ? id : 0,
                            Seq = row["seq"] is long seq ? seq : 0,
                            Word = row["word"]?.ToString(),
                            StdWord = row["stdword"]?.ToString(),
                            Token = row["token"] as long?,
                            IsCustom = row["is_custom"] is bool isCustom ? isCustom : false
                        };
                    }
                }
            }
        }
        return null;
    }
}