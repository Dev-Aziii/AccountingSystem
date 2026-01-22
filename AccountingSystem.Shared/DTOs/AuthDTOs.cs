using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    // --- NEW: Company Registration DTO ---
    public class CompanyRegisterDTO
    {
        [Required]
        public string CompanyName { get; set; }

        [Required]
        [EmailAddress]
        public string AdminEmail { get; set; }

        [Required]
        public string AdminFullName { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class RegisterDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        public string RoleName { get; set; }
    }

    public class LoginDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class AuthResponseDTO
    {
        public string Token { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public int CompanyId { get; set; } // NEW: Context for Client
        public string CompanyName { get; set; } // NEW: Context for Client
        public DateTime ExpiresAt { get; set; }
    }
}