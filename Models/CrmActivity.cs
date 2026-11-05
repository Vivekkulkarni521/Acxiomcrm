using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models;

public enum ActivityType
{
    Call = 0,
    Meeting = 1,
    Email = 2,
    Task = 3
}

public enum ActivityStatus
{
    Planned = 0,
    Completed = 1,
    Cancelled = 2,
    Overdue = 3
}

public enum ActivityPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public class CrmActivity : IValidatableObject
{
    public int Id { get; set; }

    [Required]
    [EnumDataType(typeof(ActivityType))]
    public ActivityType Type { get; set; }

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Date and time")]
    [DataType(DataType.DateTime)]
    public DateTime ScheduledAt { get; set; }

    [Required]
    [EnumDataType(typeof(ActivityStatus))]
    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    [Required]
    [EnumDataType(typeof(ActivityPriority))]
    public ActivityPriority Priority { get; set; } = ActivityPriority.Medium;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    [Required]
    public string AssignedToUserId { get; set; } = string.Empty;
    public ApplicationUser? AssignedToUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScheduledAt == default)
            yield return new ValidationResult("Enter a valid activity date and time.", new[] { nameof(ScheduledAt) });
        if (Status == ActivityStatus.Overdue && ScheduledAt >= DateTime.Now)
            yield return new ValidationResult("An activity can only be marked overdue after its scheduled time has passed.", new[] { nameof(Status), nameof(ScheduledAt) });
        if (!CustomerId.HasValue && !LeadId.HasValue && !OpportunityId.HasValue)
            yield return new ValidationResult("Link this activity to a customer, lead, or opportunity.", new[] { nameof(CustomerId), nameof(LeadId), nameof(OpportunityId) });
    }
}
