#nullable enable

namespace EAABAddIn.Src.Application.UseCases;

#if RELEASE

using System;

public class ValidateDomain
{
    public static bool Invoke()
    {

        string? domain = Environment.UserDomainName;
        System.Diagnostics.Debug.WriteLine($"User domain: {domain}");
        return string.Equals(domain, "ACUEDUCTO", StringComparison.OrdinalIgnoreCase);
    }
}
#else
public class ValidateDomain
{
    public static bool Invoke() => true;
}
#endif
