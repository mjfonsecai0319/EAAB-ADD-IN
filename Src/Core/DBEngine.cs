using System;

namespace EAABAddIn.Src.Core;

public enum DBEngine
{
    Oracle, PostgreSQL, Unknown
}

public static class DBEngineExtensions
{
    public static DBEngine ToDBEngine(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DBEngine.Unknown;
            
        return value.Trim().ToUpper() switch
        {
            "ORACLE" => DBEngine.Oracle,
            "POSTGRESQL" => DBEngine.PostgreSQL,
            _ => DBEngine.Unknown
        };
    }

    public static string ToDisplayString(this DBEngine engine) => engine switch
    {
        DBEngine.Oracle => "Oracle",
        DBEngine.PostgreSQL => "PostgreSQL",
        _ => "Unknown"
    };
}