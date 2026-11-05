using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACXIOMCRM.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TeamsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TeamsController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var teams = await _context.Teams.AsNoTracking().Include(t => t.Members)
                .OrderBy(t => t.Name).ToListAsync();
            return View(teams);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTeamViewModel model)
        {
            if (!ModelState.IsValid)
                return await IndexWithErrorsAsync(model);

            if (await _context.Teams.AnyAsync(t => t.Name.ToLower() == model.Name.Trim().ToLower()))
            {
                ModelState.AddModelError(nameof(model.Name), "A team with this name already exists.");
                return await IndexWithErrorsAsync(model);
            }

            _context.Teams.Add(new Team { Name = model.Name.Trim() });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Team created.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> IndexWithErrorsAsync(CreateTeamViewModel createModel)
        {
            ViewBag.CreateTeam = createModel;
            var teams = await _context.Teams.AsNoTracking().Include(t => t.Members)
                .OrderBy(t => t.Name).ToListAsync();
            return View("Index", teams);
        }
    }
}
