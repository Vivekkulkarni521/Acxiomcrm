using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int? TeamId { get; set; }
        public Team? Team { get; set; }
    }
}
