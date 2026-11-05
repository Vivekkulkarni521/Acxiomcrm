using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models
{
    public class Lead
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string Phone { get; set; } = string.Empty;

        public string Company { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "New";

        public string Source { get; set; } = "Website";

        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();

        public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();

        public Customer? ConvertedCustomer { get; set; }
    }
}
