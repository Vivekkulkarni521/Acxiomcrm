using ACXIOMCRM.Models.ViewModels;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ACXIOMCRM.Controllers;

[Authorize(Roles = "Admin")]
public class RolesController : Controller
{
    private static readonly HashSet<string> BuiltInRoles = new(StringComparer.Ordinal) { "Admin", "Manager", "SalesExecutive" };
    private readonly RoleManager<IdentityRole> _roles;
    private readonly UserManager<ACXIOMCRM.Models.ApplicationUser> _users;

    public RolesController(RoleManager<IdentityRole> roles, UserManager<ACXIOMCRM.Models.ApplicationUser> users)
    {
        _roles = roles;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var rows = new List<RoleManagementListItemViewModel>();
        foreach (var role in await _roles.Roles.OrderBy(role => role.Name).ToListAsync())
        {
            var members = await _users.GetUsersInRoleAsync(role.Name!);
            rows.Add(new RoleManagementListItemViewModel
            {
                Id = role.Id,
                Name = role.Name!,
                UserCount = members.Count,
                IsBuiltIn = BuiltInRoles.Contains(role.Name!)
            });
        }
        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View(BuildModel(new RolePermissionEditViewModel()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RolePermissionEditViewModel model)
    {
        ValidatePermissionSelections(model);
        var name = model.Name.Trim();
        if (await _roles.RoleExistsAsync(name))
            ModelState.AddModelError(nameof(model.Name), "A role with this name already exists.");
        if (!ModelState.IsValid)
            return View(BuildModel(model));

        var role = new IdentityRole(name);
        var created = await _roles.CreateAsync(role);
        if (!created.Succeeded)
        {
            AddErrors(created);
            return View(BuildModel(model));
        }

        var result = await ReplacePermissionsAsync(role, model.Permissions);
        if (!result.Succeeded)
        {
            await _roles.DeleteAsync(role);
            AddErrors(result);
            return View(BuildModel(model));
        }

        TempData["Success"] = "Role created with the selected permissions.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var role = await _roles.FindByIdAsync(id);
        if (role == null) return NotFound();
        var claims = await _roles.GetClaimsAsync(role);
        var granted = claims.Where(claim => claim.Type == CrmPermissionCatalog.ClaimType)
            .Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        var model = new RolePermissionEditViewModel
        {
            RoleId = role.Id,
            Name = role.Name ?? string.Empty,
            Permissions = BuildPermissions(granted)
        };
        return View(BuildModel(model));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RolePermissionEditViewModel model)
    {
        var role = string.IsNullOrWhiteSpace(model.RoleId) ? null : await _roles.FindByIdAsync(model.RoleId);
        if (role == null) return NotFound();
        ValidatePermissionSelections(model);
        if (!string.Equals(model.Name.Trim(), role.Name, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.Name), "Role names cannot be changed here.");
        if (!ModelState.IsValid)
            return View(BuildModel(model));

        var result = await ReplacePermissionsAsync(role, model.Permissions);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(BuildModel(model));
        }

        TempData["Success"] = "Role permissions updated. Changes take effect on the next request.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roles.FindByIdAsync(id);
        if (role == null) return NotFound();
        if (BuiltInRoles.Contains(role.Name ?? string.Empty))
        {
            TempData["Error"] = "Built-in roles cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }
        if ((await _users.GetUsersInRoleAsync(role.Name!)).Count > 0)
        {
            TempData["Error"] = "Remove this role from its users before deleting it.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _roles.DeleteAsync(role);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
            AddErrors(result);
        }
        else
        {
            TempData["Success"] = "Role deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IdentityResult> ReplacePermissionsAsync(IdentityRole role, IEnumerable<RolePermissionSelection> selected)
    {
        foreach (var claim in await _roles.GetClaimsAsync(role))
        {
            if (claim.Type == CrmPermissionCatalog.ClaimType)
            {
                var removed = await _roles.RemoveClaimAsync(role, claim);
                if (!removed.Succeeded) return removed;
            }
        }
        foreach (var item in selected.Where(item => item.Granted))
        {
            var added = await _roles.AddClaimAsync(role, new Claim(CrmPermissionCatalog.ClaimType,
                CrmPermissionCatalog.Value(item.Module, item.Operation)));
            if (!added.Succeeded) return added;
        }
        return IdentityResult.Success;
    }

    private void ValidatePermissionSelections(RolePermissionEditViewModel model)
    {
        foreach (var item in model.Permissions)
        {
            if (!CrmPermissionCatalog.IsValid(item.Module, item.Operation))
            {
                ModelState.AddModelError(nameof(model.Permissions), "The submitted permission list contains an invalid item.");
                return;
            }
        }
        if (model.Permissions.Select(item => CrmPermissionCatalog.Value(item.Module, item.Operation)).Distinct().Count() != model.Permissions.Count)
            ModelState.AddModelError(nameof(model.Permissions), "The submitted permission list contains duplicates.");
    }

    private static RolePermissionEditViewModel BuildModel(RolePermissionEditViewModel model)
    {
        var granted = model.Permissions.Where(item => item.Granted)
            .Select(item => CrmPermissionCatalog.Value(item.Module, item.Operation)).ToHashSet(StringComparer.Ordinal);
        model.Permissions = BuildPermissions(granted);
        return model;
    }

    private static List<RolePermissionSelection> BuildPermissions(HashSet<string> granted) =>
        CrmPermissionCatalog.Modules.SelectMany(module => CrmPermissionCatalog.Operations.Select(operation =>
            new RolePermissionSelection
            {
                Module = module,
                Operation = operation,
                Granted = granted.Contains(CrmPermissionCatalog.Value(module, operation))
            })).ToList();

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }
}
