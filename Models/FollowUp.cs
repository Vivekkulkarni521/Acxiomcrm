using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models
{
    public enum FollowUpStatus
    {
        Planned = 0,
        Completed = 1,
        Missed = 2,
        Cancelled = 3
    }

    public class FollowUp : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Select a lead.")]
        [Range(1, int.MaxValue, ErrorMessage = "Select a valid lead.")]
        [Display(Name = "Lead")]
        public int LeadId { get; set; }

        public Lead? Lead { get; set; }

        [Required]
        [StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [StringLength(2000)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Scheduled date and time")]
        [DataType(DataType.DateTime)]
        public DateTime ScheduledDate { get; set; }

        [Required]
        [EnumDataType(typeof(FollowUpStatus))]
        public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ScheduledDate == default)
            {
                yield return new ValidationResult(
                    "Enter a valid scheduled date and time.",
                    new[] { nameof(ScheduledDate) });
            }
        }
    }
}
