using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models
{
    public class Team
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
    }
}
