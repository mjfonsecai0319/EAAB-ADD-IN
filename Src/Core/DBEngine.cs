using System;

namespace EAABAddIn.Src.Core;

public enum DBEngine
{
    Oracle, PostgreSQL, Unknown
}

public static class DBEngineExtensions
{
    public static DBEngine ToDBEngine(this string value) => value.ToUpper() switch
    {
        "ORACLE" => DBEngine.Oracle,
        "POSTGRESQL" => DBEngine.PostgreSQL,
        _ => DBEngine.Unknown
    };

}