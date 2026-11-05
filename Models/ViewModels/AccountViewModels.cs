using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models.ViewModels
{
    public class CreateUserViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(12, ErrorMessage = "Password must be at least 12 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Admin|Manager|SalesExecutive)$", ErrorMessage = "Select a valid role.")]
        public string Role { get; set; } = "SalesExecutive";

        [Display(Name = "Team")]
        public int? TeamId { get; set; }

        [Display(Name = "Account active")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Roles { get; set; } = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
        public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Teams { get; set; } = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
    }

    public class RegisterViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 12, ErrorMessage = "Passwords must be at least 12 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class EditUserViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Admin|Manager|SalesExecutive)$", ErrorMessage = "Select a valid role.")]
        public string Role { get; set; } = "SalesExecutive";

        [Display(Name = "Team")]
        public int? TeamId { get; set; }

        [Display(Name = "Account active")]
        public bool IsActive { get; set; }

        public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Roles { get; set; } = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
        public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Teams { get; set; } = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
    }

    public class CreateTeamViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
namespace ACXIOMCRM.Models.ViewModels
{
    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? TeamName { get; set; }
        public bool IsActive { get; set; }
    }
}
