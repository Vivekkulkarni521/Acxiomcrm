using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models
{
    public enum OpportunityStage
    {
        Qualification = 0,
        Proposal = 1,
        Negotiation = 2,
        Won = 3,
        Lost = 4
    }

    public class Opportunity : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Select a lead.")]
        [Range(1, int.MaxValue, ErrorMessage = "Select a valid lead.")]
        [Display(Name = "Lead")]
        public int LeadId { get; set; }

        public Lead? Lead { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Amount must be greater than zero.")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required]
        [EnumDataType(typeof(OpportunityStage))]
        public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

        [Range(0, 100)]
        public int Probability { get; set; }

        [Required]
        [Display(Name = "Expected close date")]
        [DataType(DataType.Date)]
        public DateTime ExpectedCloseDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ExpectedCloseDate == default)
            {
                yield return new ValidationResult(
                    "Enter a valid expected close date.",
                    new[] { nameof(ExpectedCloseDate) });
            }
        }
    }
}
