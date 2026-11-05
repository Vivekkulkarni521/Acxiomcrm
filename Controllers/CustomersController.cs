using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ACXIOMCRM.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CrmAccessService _access;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomersController(ApplicationDbContext context, CrmAccessService access, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _access = access;
            _userManager = userManager;
        }

        [CrmPermission("Customers", "View")]
        public async Task<IActionResult> Index(string? search, string? company, string? assignedToUserId)
        {
            var accessibleCustomers = (await _access.CustomersForAsync(User)).AsNoTracking();
            var companies = await accessibleCustomers
                .Where(c => c.Company != null && c.Company != string.Empty)
                .Select(c => c.Company).Distinct().OrderBy(value => value).ToListAsync();
            var assignees = await accessibleCustomers
                .Where(c => c.AssignedToUserId != null)
                .Select(c => new { c.AssignedToUserId, c.AssignedToUser!.DisplayName })
                .Distinct().OrderBy(item => item.DisplayName)
                .Select(item => new SelectListItem(item.DisplayName, item.AssignedToUserId))
                .ToListAsync();

            var query = accessibleCustomers.Include(c => c.AssignedToUser).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c => c.Name.Contains(term) || c.Email.Contains(term) ||
                    c.Phone.Contains(term) || c.Company.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(company))
                query = query.Where(c => c.Company == company);
            if (!string.IsNullOrWhiteSpace(assignedToUserId))
                query = query.Where(c => c.AssignedToUserId == assignedToUserId);

            var model = new CustomerListViewModel
            {
                Customers = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(),
                Search = search,
                Company = company,
                AssignedToUserId = assignedToUserId,
                Companies = companies,
                Assignees = assignees.Select(item =>
                {
                    item.Selected = item.Value == assignedToUserId;
                    return item;
                }).ToList()
            };
            return View(model);
        }

        [HttpGet]
        [CrmPermission("Customers", "View")]
        public async Task<IActionResult> Details(int id)
        {
            var customer = await (await _access.CustomersForAsync(User))
                .AsNoTracking().Include(c => c.AssignedToUser)
                .FirstOrDefaultAsync(c => c.Id == id);
            return customer == null ? NotFound() : View(customer);
        }

        [HttpGet]
        [CrmPermission("Customers", "Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateAssigneesAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CrmPermission("Customers", "Create")]
        public async Task<IActionResult> Create(Customer customer)
        {
            await SetOrValidateOwnerAsync(customer);
            if (!ModelState.IsValid)
            {
                await PopulateAssigneesAsync(customer.AssignedToUserId);
                return View(customer);
            }

            customer.CreatedAt = DateTime.UtcNow;
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Customer added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [CrmPermission("Customers", "Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await (await _access.CustomersForAsync(User)).FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null)
                return NotFound();
            await PopulateAssigneesAsync(customer.AssignedToUserId);
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CrmPermission("Customers", "Edit")]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
                return BadRequest();
            await SetOrValidateOwnerAsync(customer);
            if (!ModelState.IsValid)
            {
                await PopulateAssigneesAsync(customer.AssignedToUserId);
                return View(customer);
            }

            var existing = await (await _access.CustomersForAsync(User)).FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null)
                return NotFound();

            existing.Name = customer.Name;
            existing.Email = customer.Email;
            existing.Phone = customer.Phone;
            existing.Company = customer.Company;
            existing.Address = customer.Address;
            existing.City = customer.City;
            existing.State = customer.State;
            existing.Country = customer.Country;
            if (User.IsInRole("Admin"))
                existing.AssignedToUserId = customer.AssignedToUserId;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Customer updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [CrmPermission("Customers", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await (await _access.CustomersForAsync(User)).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            return customer == null ? NotFound() : View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [CrmPermission("Customers", "Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await (await _access.CustomersForAsync(User)).FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null)
                return NotFound();

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Customer deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task SetOrValidateOwnerAsync(Customer customer)
        {
            if (!User.IsInRole("Admin"))
            {
                customer.AssignedToUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return;
            }

            if (string.IsNullOrWhiteSpace(customer.AssignedToUserId))
                return;

            var assignee = await _userManager.FindByIdAsync(customer.AssignedToUserId);
            if (assignee == null || !assignee.IsActive)
                ModelState.AddModelError(nameof(customer.AssignedToUserId), "Select an active user.");
            else if (!(await _userManager.GetRolesAsync(assignee)).Any(role => role is "Admin" or "Manager" or "SalesExecutive"))
                ModelState.AddModelError(nameof(customer.AssignedToUserId), "The selected user does not have a CRM role.");
        }

        private async Task PopulateAssigneesAsync(string? selectedUserId = null)
        {
            if (!User.IsInRole("Admin"))
                return;

            var activeUsers = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.DisplayName).ToListAsync();
            var assignees = new List<SelectListItem>();
            foreach (var user in activeUsers)
            {
                if ((await _userManager.GetRolesAsync(user)).Any(role => role is "Admin" or "Manager" or "SalesExecutive"))
                    assignees.Add(new SelectListItem($"{user.DisplayName} ({user.Email})", user.Id, user.Id == selectedUserId));
            }
            ViewBag.Assignees = assignees;
        }
    }
}
