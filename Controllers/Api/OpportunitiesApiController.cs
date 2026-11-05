using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.Api;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACXIOMCRM.Controllers.Api;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/opportunities")]
[Authorize(Roles = "Admin,Manager,SalesExecutive")]
public sealed class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CrmAccessService _access;

    public OpportunitiesApiController(ApplicationDbContext db, CrmAccessService access)
    {
        _db = db;
        _access = access;
    }

    [HttpGet, CrmPermission("Opportunities", "View")]
    public async Task<ActionResult<IEnumerable<OpportunityReadDto>>> GetAll()
    {
        var query = (await _access.OpportunitiesForAsync(User)).AsNoTracking().Include(item => item.Lead);
        var opportunities = await query.OrderByDescending(item => item.CreatedAt).ToListAsync();
        return Ok(opportunities.Select(ToReadDto).ToList());
    }

    [HttpGet("{id:int}"), CrmPermission("Opportunities", "View")]
    public async Task<ActionResult<OpportunityReadDto>> GetById(int id)
    {
        var item = await (await _access.OpportunitiesForAsync(User)).AsNoTracking().Include(item => item.Lead)
            .FirstOrDefaultAsync(item => item.Id == id);
        return item == null ? NotFound() : Ok(ToReadDto(item));
    }

    [HttpPost, CrmPermission("Opportunities", "Create")]
    public async Task<ActionResult<OpportunityReadDto>> Create(OpportunityWriteDto dto)
    {
        var lead = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(item => item.Id == dto.LeadId);
        if (lead == null) return BadRequest(new ProblemDetails { Title = "Select a lead you are allowed to access." });
        var item = new Opportunity
        {
            LeadId = dto.LeadId, Title = dto.Title.Trim(), Amount = dto.Amount, Stage = dto.Stage,
            Probability = dto.Probability, ExpectedCloseDate = dto.ExpectedCloseDate, CreatedAt = DateTime.UtcNow, Lead = lead
        };
        _db.Opportunities.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToReadDto(item));
    }

    [HttpPut("{id:int}"), CrmPermission("Opportunities", "Edit")]
    public async Task<IActionResult> Update(int id, OpportunityWriteDto dto)
    {
        var item = await (await _access.OpportunitiesForAsync(User)).FirstOrDefaultAsync(value => value.Id == id);
        if (item == null) return NotFound();
        var lead = await (await _access.LeadsForAsync(User)).AnyAsync(value => value.Id == dto.LeadId);
        if (!lead) return BadRequest(new ProblemDetails { Title = "Select a lead you are allowed to access." });
        item.LeadId = dto.LeadId; item.Title = dto.Title.Trim(); item.Amount = dto.Amount;
        item.Stage = dto.Stage; item.Probability = dto.Probability; item.ExpectedCloseDate = dto.ExpectedCloseDate;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}"), CrmPermission("Opportunities", "Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await (await _access.OpportunitiesForAsync(User)).FirstOrDefaultAsync(value => value.Id == id);
        if (item == null) return NotFound();
        _db.Opportunities.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static OpportunityReadDto ToReadDto(Opportunity item) => new(item.Id, item.LeadId,
        item.Lead?.Name ?? string.Empty, item.Title, item.Amount, item.Stage, item.Probability,
        item.ExpectedCloseDate, item.CreatedAt);
}
