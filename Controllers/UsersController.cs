using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ACXIOMCRM.Security;

namespace ACXIOMCRM.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.AsNoTracking().Include(u => u.Team)
                .OrderBy(u => u.Email).ToListAsync();
            var rows = new List<UserListItemViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                rows.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email ?? string.Empty,
                    Role = roles.Count > 0 ? string.Join(", ", roles) : "—",
                    TeamName = user.Team?.Name,
                    IsActive = user.IsActive
                });
            }
            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel();
            await PopulateCreateOptionsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (model.TeamId.HasValue && !await _context.Teams.AnyAsync(t => t.Id == model.TeamId.Value))
                ModelState.AddModelError(nameof(model.TeamId), "Select a valid team.");

            if (!await _roleManager.RoleExistsAsync(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Select a valid role.");

            if (!ModelState.IsValid)
            {
                await PopulateCreateOptionsAsync(model);
                return View(model);
            }

            var email = model.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = model.DisplayName.Trim(),
                TeamId = model.TeamId,
                IsActive = model.IsActive,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                AddIdentityErrors(createResult);
                await PopulateCreateOptionsAsync(model);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                await PopulateCreateOptionsAsync(model);
                return View(model);
            }

            TempData["Success"] = "User created and assigned to the selected role.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.Users.Include(u => u.Team).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var model = new EditUserViewModel
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "SalesExecutive",
                TeamId = user.TeamId,
                IsActive = user.IsActive
            };
            await PopulateOptionsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return NotFound();

            if (!await _roleManager.RoleExistsAsync(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
            if (model.TeamId.HasValue && !await _context.Teams.AnyAsync(t => t.Id == model.TeamId.Value))
                ModelState.AddModelError(nameof(model.TeamId), "Select a valid team.");

            var isSelf = _userManager.GetUserId(User) == user.Id;
            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentlyAdmin = currentRoles.Contains("Admin");
            if (isSelf && (!model.IsActive || model.Role != "Admin"))
                ModelState.AddModelError(string.Empty, "You cannot deactivate yourself or remove your own Admin role.");

            if (currentlyAdmin && user.IsActive && (!model.IsActive || model.Role != "Admin"))
            {
                var otherActiveAdmins = await _userManager.GetUsersInRoleAsync("Admin");
                if (!otherActiveAdmins.Any(candidate => candidate.Id != user.Id && candidate.IsActive))
                    ModelState.AddModelError(string.Empty, "The last active Admin account cannot be deactivated or demoted.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateOptionsAsync(model);
                return View(model);
            }

            var previousTeamId = user.TeamId;
            var previousIsActive = user.IsActive;
            var oldRoles = currentRoles.ToArray();
            if (oldRoles.Length > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, oldRoles);
                if (!removeResult.Succeeded)
                {
                    AddIdentityErrors(removeResult);
                    await PopulateOptionsAsync(model);
                    return View(model);
                }
            }

            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                if (oldRoles.Length > 0)
                    await _userManager.AddToRolesAsync(user, oldRoles);
                AddIdentityErrors(roleResult);
                await PopulateOptionsAsync(model);
                return View(model);
            }

            user.TeamId = model.TeamId;
            user.IsActive = model.IsActive;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await _userManager.RemoveFromRoleAsync(user, model.Role);
                if (oldRoles.Length > 0)
                    await _userManager.AddToRolesAsync(user, oldRoles);
                user.TeamId = previousTeamId;
                user.IsActive = previousIsActive;
                AddIdentityErrors(updateResult);
                await PopulateOptionsAsync(model);
                return View(model);
            }

            await _userManager.UpdateSecurityStampAsync(user);
            TempData["Success"] = "User access settings updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.Users.AsNoTracking().Include(u => u.Team)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            return View(new UserListItemViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                Role = roles.Count > 0 ? string.Join(", ", roles) : "—",
                TeamName = user.Team?.Name,
                IsActive = user.IsActive
            });
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var actorId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(actorId) || actorId == user.Id)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (user.IsActive && roles.Contains("Admin"))
            {
                var activeAdmins = await _userManager.GetUsersInRoleAsync("Admin");
                if (!activeAdmins.Any(candidate => candidate.IsActive && candidate.Id != user.Id))
                {
                    TempData["Error"] = "The last active Admin account cannot be deleted.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Activities.Where(activity => activity.AssignedToUserId == user.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(activity => activity.AssignedToUserId, actorId));

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
                    return RedirectToAction(nameof(Index));
                }

                await transaction.CommitAsync();
                TempData["Success"] = "User deleted. Assigned activities were transferred to your account.";
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "The user could not be deleted because related records could not be reassigned.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateOptionsAsync(EditUserViewModel model)
        {
            model.Roles = await _roleManager.Roles.AsNoTracking().OrderBy(role => role.Name)
                .Select(role => new SelectListItem(role.Name!, role.Name!, role.Name == model.Role)).ToListAsync();
            model.Teams = await _context.Teams.AsNoTracking().OrderBy(t => t.Name)
                .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == model.TeamId))
                .ToListAsync();
        }

        private async Task PopulateCreateOptionsAsync(CreateUserViewModel model)
        {
            model.Roles = await _roleManager.Roles.AsNoTracking().OrderBy(role => role.Name)
                .Select(role => new SelectListItem(role.Name!, role.Name!, role.Name == model.Role)).ToListAsync();
            model.Teams = await _context.Teams.AsNoTracking().OrderBy(t => t.Name)
                .Select(t => new SelectListItem(t.Name, t.Id.ToString(), t.Id == model.TeamId))
                .ToListAsync();
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}

