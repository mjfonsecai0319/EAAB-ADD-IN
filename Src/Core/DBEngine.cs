using System;

namespace EAABAddIn.Src.Core;

public enum DBEngine
{
    Oracle,
    PostgreSQL,
    OracleSDE,
    PostgreSQLSDE,
    Unknown
}

public static class DBEngineExtensions
{
    public static DBEngine ToDBEngine(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DBEngine.Unknown;
            
        var v = value.Trim().ToUpper();
        return v switch
        {
            "ORACLE" => DBEngine.Oracle,
            "POSTGRESQL" => DBEngine.PostgreSQL,
            "ORACLESDE" => DBEngine.OracleSDE,
            "ORACLE SDE" => DBEngine.OracleSDE,
            "ORACLE (ARCHIVO DE CREDENCIALES)" => DBEngine.OracleSDE,
            "POSTGRESQLSDE" => DBEngine.PostgreSQLSDE,
            "POSTGRESQL SDE" => DBEngine.PostgreSQLSDE,
            _ => DBEngine.Unknown
        };
    }

    public static string ToDisplayString(this DBEngine engine) => engine switch
    {
        DBEngine.Oracle => "Oracle",
        DBEngine.PostgreSQL => "PostgreSQL",
        DBEngine.OracleSDE => "Oracle SDE",
        DBEngine.PostgreSQLSDE => "PostgreSQL SDE",
        _ => "Unknown"
    };
}