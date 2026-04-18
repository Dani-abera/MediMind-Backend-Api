using Microsoft.AspNetCore.Authorization;

namespace MediMind.API.Attributes;

/// <summary>
/// Declarative role requirement attribute that maps to JWT role claims.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireRoleAttribute : AuthorizeAttribute
{
    public RequireRoleAttribute(string role)
    {
        Roles = role;
    }

    public RequireRoleAttribute(params string[] roles)
    {
        Roles = string.Join(",", roles);
    }
}
