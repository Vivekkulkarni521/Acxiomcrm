using ACXIOMCRM.Models;
using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models.Api;

public sealed class CustomerWriteDto
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, Phone, StringLength(32)] public string Phone { get; set; } = string.Empty;
    [StringLength(150)] public string Company { get; set; } = string.Empty;
    [StringLength(250)] public string Address { get; set; } = string.Empty;
    [StringLength(100)] public string City { get; set; } = string.Empty;
    [StringLength(100)] public string State { get; set; } = string.Empty;
    [StringLength(100)] public string Country { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
}

public sealed record CustomerReadDto(int Id, string Name, string Email, string Phone, string Company,
    string Address, string City, string State, string Country, string? AssignedToUserId, string? AssignedTo,
    DateTime CreatedAt);

public sealed class LeadWriteDto
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Phone, StringLength(32)] public string Phone { get; set; } = string.Empty;
    [StringLength(150)] public string Company { get; set; } = string.Empty;
    [Required, ValidLeadStatus] public string Status { get; set; } = LeadStatuses.New;
    [Required, StringLength(100)] public string Source { get; set; } = "Website";
    public string? AssignedToUserId { get; set; }
}

public sealed record LeadReadDto(int Id, string Name, string Email, string Phone, string Company,
    string Source, string Status, string? AssignedToUserId, string? AssignedTo, DateTime CreatedAt,
    int FollowUpCount, int OpportunityCount);

public sealed class ValidLeadStatusAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is string status &&
        LeadStatuses.Selectable.Contains(status, StringComparer.Ordinal);

    public override string FormatErrorMessage(string name) => "Select a valid lead status.";
}

public sealed class OpportunityWriteDto : IValidatableObject
{
    [Range(1, int.MaxValue)] public int LeadId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal Amount { get; set; }
    [EnumDataType(typeof(OpportunityStage))] public OpportunityStage Stage { get; set; }
    [Range(0, 100)] public int Probability { get; set; }
    [Required, DataType(DataType.Date)] public DateTime ExpectedCloseDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpectedCloseDate == default)
            yield return new ValidationResult("Enter a valid expected close date.", new[] { nameof(ExpectedCloseDate) });
    }
}

public sealed record OpportunityReadDto(int Id, int LeadId, string LeadName, string Title, decimal Amount,
    OpportunityStage Stage, int Probability, DateTime ExpectedCloseDate, DateTime CreatedAt);
