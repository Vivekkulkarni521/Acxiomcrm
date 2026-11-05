using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.Api;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ACXIOMCRM.Controllers.Api;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/customers")]
[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public sealed class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CrmAccessService _access;
    private readonly UserManager<ApplicationUser> _users;

    public CustomersApiController(ApplicationDbContext db, CrmAccessService access, UserManager<ApplicationUser> users)
    {
        _db = db;
        _access = access;
        _users = users;
    }

    [HttpGet, CrmPermission("Customers", "View")]
    public async Task<ActionResult<IEnumerable<CustomerReadDto>>> GetAll()
    {
        var query = (await _access.CustomersForAsync(User)).AsNoTracking().Include(customer => customer.AssignedToUser);
        var customers = await query.OrderByDescending(customer => customer.CreatedAt).ToListAsync();
        return Ok(customers.Select(ToReadDto).ToList());
    }

    [HttpGet("{id:int}"), CrmPermission("Customers", "View")]
    public async Task<ActionResult<CustomerReadDto>> GetById(int id)
    {
        var customer = await (await _access.CustomersForAsync(User)).AsNoTracking()
            .Include(item => item.AssignedToUser).FirstOrDefaultAsync(item => item.Id == id);
        return customer == null ? NotFound() : Ok(ToReadDto(customer));
    }

    [HttpPost, CrmPermission("Customers", "Create")]
    public async Task<ActionResult<CustomerReadDto>> Create(CustomerWriteDto dto)
    {
        var owner = await ResolveOwnerAsync(dto.AssignedToUserId);
        if (owner.Invalid) return BadRequest(new ProblemDetails { Title = owner.Error });
        var customer = new Customer
        {
            Name = dto.Name.Trim(), Email = dto.Email.Trim(), Phone = dto.Phone.Trim(), Company = dto.Company.Trim(),
            Address = dto.Address.Trim(), City = dto.City.Trim(), State = dto.State.Trim(), Country = dto.Country.Trim(),
            AssignedToUserId = owner.UserId, CreatedAt = DateTime.UtcNow
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        await _db.Entry(customer).Reference(item => item.AssignedToUser).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, ToReadDto(customer));
    }

    [HttpPut("{id:int}"), CrmPermission("Customers", "Edit")]
    public async Task<IActionResult> Update(int id, CustomerWriteDto dto)
    {
        var customer = await (await _access.CustomersForAsync(User)).FirstOrDefaultAsync(item => item.Id == id);
        if (customer == null) return NotFound();
        var owner = await ResolveOwnerAsync(dto.AssignedToUserId);
        if (owner.Invalid) return BadRequest(new ProblemDetails { Title = owner.Error });
        customer.Name = dto.Name.Trim(); customer.Email = dto.Email.Trim(); customer.Phone = dto.Phone.Trim();
        customer.Company = dto.Company.Trim(); customer.Address = dto.Address.Trim(); customer.City = dto.City.Trim();
        customer.State = dto.State.Trim(); customer.Country = dto.Country.Trim();
        if (User.IsInRole("Admin")) customer.AssignedToUserId = owner.UserId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}"), CrmPermission("Customers", "Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await (await _access.CustomersForAsync(User)).FirstOrDefaultAsync(item => item.Id == id);
        if (customer == null) return NotFound();
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<(string? UserId, string? Error, bool Invalid)> ResolveOwnerAsync(string? requestedUserId)
    {
        if (!User.IsInRole("Admin"))
            return (User.FindFirstValue(ClaimTypes.NameIdentifier), null, false);
        if (string.IsNullOrWhiteSpace(requestedUserId)) return (null, null, false);
        var user = await _users.FindByIdAsync(requestedUserId);
        if (user is not { IsActive: true }) return (null, "Choose an active CRM assignee.", true);
        if (!(await _users.GetRolesAsync(user)).Any(role => role is "Admin" or "Manager" or "SalesExecutive"))
            return (null, "The selected user does not have a CRM role.", true);
        return (user.Id, null, false);
    }

    private static CustomerReadDto ToReadDto(Customer item) => new(item.Id, item.Name, item.Email, item.Phone,
        item.Company, item.Address, item.City, item.State, item.Country, item.AssignedToUserId,
        item.AssignedToUser?.DisplayName, item.CreatedAt);
}
