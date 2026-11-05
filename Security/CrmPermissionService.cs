using ACXIOMCRM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ACXIOMCRM.Security;

public static class CrmPermissionCatalog
{
    public const string ClaimType = "crm.permission";
    public static readonly string[] Modules = { "Customers", "Leads", "FollowUps", "Opportunities", "Activities", "Reports" };
    public static readonly string[] Operations = { "View", "Create", "Edit", "Delete" };

    public static string Value(string module, string operation) => $"{module}.{operation}";

    public static bool IsValid(string module, string operation) =>
        Modules.Contains(module, StringComparer.Ordinal) && Operations.Contains(operation, StringComparer.Ordinal);

    public static IReadOnlyList<string> ManagerDefaults => Modules
        .Where(module => module != "Reports")
        .SelectMany(module => Operations.Where(operation => operation != "Delete")
            .Select(operation => Value(module, operation)))
        .Append(Value("Reports", "View"))
        .ToArray();

    public static IReadOnlyList<string> SalesExecutiveDefaults => Modules
        .Where(module => module != "Reports")
        .SelectMany(module => new[] { Value(module, "View"), Value(module, "Create"), Value(module, "Edit") })
        .ToArray();
}

public sealed class CrmPermissionService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly Dictionary<string, HashSet<string>> _cache = new(StringComparer.Ordinal);

    public CrmPermissionService(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string module, string operation)
    {
        if (principal.Identity?.IsAuthenticated != true || !CrmPermissionCatalog.IsValid(module, operation))
            return false;

        var user = await _users.GetUserAsync(principal);
        if (user is not { IsActive: true })
            return false;

        if (await _users.IsInRoleAsync(user, "Admin"))
            return true;

        var requested = CrmPermissionCatalog.Value(module, operation);
        var userRoles = await _users.GetRolesAsync(user);
        foreach (var roleName in userRoles)
        {
            if (!_cache.TryGetValue(roleName, out var rolePermissions))
            {
                rolePermissions = new HashSet<string>(StringComparer.Ordinal);
                var role = await _roles.FindByNameAsync(roleName);
                if (role != null)
                {
                    foreach (var claim in await _roles.GetClaimsAsync(role))
                    {
                        if (claim.Type == CrmPermissionCatalog.ClaimType)
                            rolePermissions.Add(claim.Value);
                    }
                }
                _cache[roleName] = rolePermissions;
            }

            if (rolePermissions.Contains(requested))
                return true;
        }

        return false;
    }
}

public sealed class CrmPermissionAttribute : TypeFilterAttribute
{
    public CrmPermissionAttribute(string module, string operation) : base(typeof(CrmPermissionFilter))
    {
        Arguments = new object[] { module, operation };
    }
}

public sealed class CrmPermissionFilter : IAsyncAuthorizationFilter
{
    private readonly CrmPermissionService _permissions;
    private readonly string _module;
    private readonly string _operation;

    public CrmPermissionFilter(CrmPermissionService permissions, string module, string operation)
    {
        _permissions = permissions;
        _module = module;
        _operation = operation;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (!await _permissions.HasPermissionAsync(context.HttpContext.User, _module, _operation))
            context.Result = new ForbidResult();
    }
}
