using ACXIOMCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models.ViewModels;

public sealed class ManualAuditFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Department { get; set; } = string.Empty;

    [Required, Display(Name = "Auditor")]
    public string AuditorUserId { get; set; } = string.Empty;

    [Required, DataType(DataType.Date), Display(Name = "Audit date")]
    public DateTime AuditDate { get; set; } = DateTime.Today;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Findings { get; set; } = string.Empty;

    [Required, EnumDataType(typeof(ManualAuditStatus))]
    public ManualAuditStatus Status { get; set; } = ManualAuditStatus.Open;

    public IReadOnlyList<SelectListItem> Auditors { get; set; } = Array.Empty<SelectListItem>();
}

public sealed class ManualAuditListViewModel
{
    public IReadOnlyList<ManualAudit> Audits { get; set; } = Array.Empty<ManualAudit>();
    public string? Search { get; set; }
    public string? Department { get; set; }
    public string? AuditorUserId { get; set; }
    public ManualAuditStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public IReadOnlyList<string> Departments { get; set; } = Array.Empty<string>();
    public IReadOnlyList<SelectListItem> Auditors { get; set; } = Array.Empty<SelectListItem>();
}

public sealed class ManualAuditReportViewModel
{
    public IReadOnlyList<ManualAuditEvent> Events { get; set; } = Array.Empty<ManualAuditEvent>();
    public IReadOnlyList<string> Users { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Auditors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Actions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Statuses { get; set; } = Array.Empty<string>();
    public string? Search { get; set; }
    public string? UserName { get; set; }
    public string? AuditorName { get; set; }
    public string? Action { get; set; }
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Total { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
}
