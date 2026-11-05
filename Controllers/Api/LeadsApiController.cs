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
[Route("api/leads")]
[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public sealed class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CrmAccessService _access;
    private readonly UserManager<ApplicationUser> _users;

    public LeadsApiController(ApplicationDbContext db, CrmAccessService access, UserManager<ApplicationUser> users)
    {
        _db = db;
        _access = access;
        _users = users;
    }

    [HttpGet, CrmPermission("Leads", "View")]
    public async Task<ActionResult<IEnumerable<LeadReadDto>>> GetAll()
    {
        var query = (await _access.LeadsForAsync(User)).AsNoTracking();
        return Ok(await query.OrderByDescending(lead => lead.CreatedAt)
            .Select(lead => new LeadReadDto(lead.Id, lead.Name, lead.Email, lead.Phone, lead.Company, lead.Source,
                lead.Status, lead.AssignedToUserId, lead.AssignedToUser == null ? null : lead.AssignedToUser.DisplayName,
                lead.CreatedAt, lead.FollowUps.Count, lead.Opportunities.Count)).ToListAsync());
    }

    [HttpGet("{id:int}"), CrmPermission("Leads", "View")]
    public async Task<ActionResult<LeadReadDto>> GetById(int id)
    {
        var lead = await (await _access.LeadsForAsync(User)).AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new LeadReadDto(item.Id, item.Name, item.Email, item.Phone, item.Company, item.Source,
                item.Status, item.AssignedToUserId, item.AssignedToUser == null ? null : item.AssignedToUser.DisplayName,
                item.CreatedAt, item.FollowUps.Count, item.Opportunities.Count)).FirstOrDefaultAsync();
        return lead == null ? NotFound() : Ok(lead);
    }

    [HttpPost, CrmPermission("Leads", "Create")]
    public async Task<ActionResult<LeadReadDto>> Create(LeadWriteDto dto)
    {
        var owner = await ResolveOwnerAsync(dto.AssignedToUserId);
        if (owner.Invalid) return BadRequest(new ProblemDetails { Title = owner.Error });
        var lead = new Lead
        {
            Name = dto.Name.Trim(), Email = dto.Email.Trim(), Phone = dto.Phone.Trim(), Company = dto.Company.Trim(),
            Status = dto.Status, Source = dto.Source.Trim(), AssignedToUserId = owner.UserId, CreatedAt = DateTime.UtcNow
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = lead.Id }, await MapAsync(lead));
    }

    [HttpPut("{id:int}"), CrmPermission("Leads", "Edit")]
    public async Task<IActionResult> Update(int id, LeadWriteDto dto)
    {
        var lead = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(item => item.Id == id);
        if (lead == null) return NotFound();
        var owner = await ResolveOwnerAsync(dto.AssignedToUserId);
        if (owner.Invalid) return BadRequest(new ProblemDetails { Title = owner.Error });
        lead.Name = dto.Name.Trim(); lead.Email = dto.Email.Trim(); lead.Phone = dto.Phone.Trim();
        lead.Company = dto.Company.Trim(); lead.Source = dto.Source.Trim();
        if (lead.Status != LeadStatuses.Converted) lead.Status = dto.Status;
        if (User.IsInRole("Admin")) lead.AssignedToUserId = owner.UserId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}"), CrmPermission("Leads", "Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(item => item.Id == id);
        if (lead == null) return NotFound();
        _db.Leads.Remove(lead);
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

    private async Task<LeadReadDto> MapAsync(Lead lead)
    {
        var user = string.IsNullOrWhiteSpace(lead.AssignedToUserId) ? null : await _users.FindByIdAsync(lead.AssignedToUserId);
        return new LeadReadDto(lead.Id, lead.Name, lead.Email, lead.Phone, lead.Company, lead.Source, lead.Status,
            lead.AssignedToUserId, user?.DisplayName, lead.CreatedAt, 0, 0);
    }
}
