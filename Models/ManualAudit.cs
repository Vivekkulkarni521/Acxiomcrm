using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models;

public enum ManualAuditStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2
}

public sealed class ManualAudit
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Department { get; set; } = string.Empty;

    [StringLength(450)]
    [Display(Name = "Auditor")]
    public string? AuditorUserId { get; set; }
    [Required, StringLength(256)]
    public string AuditorName { get; set; } = string.Empty;
    public ApplicationUser? Auditor { get; set; }

    [Required, DataType(DataType.Date)]
    [Display(Name = "Audit date")]
    public DateTime AuditDate { get; set; }

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Findings { get; set; } = string.Empty;

    [Required, EnumDataType(typeof(ManualAuditStatus))]
    public ManualAuditStatus Status { get; set; } = ManualAuditStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Immutable snapshot of each manual audit create, update, or delete.</summary>
public sealed class ManualAuditEvent
{
    public long Id { get; set; }
    public int AuditRecordId { get; set; }

    [Required, StringLength(20)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Department { get; set; } = string.Empty;

    [StringLength(450)]
    public string AuditorUserId { get; set; } = string.Empty;

    [Required, StringLength(256)]
    public string AuditorName { get; set; } = string.Empty;

    public DateTime AuditDate { get; set; }

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Findings { get; set; } = string.Empty;

    [Required, StringLength(32)]
    public string Status { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
