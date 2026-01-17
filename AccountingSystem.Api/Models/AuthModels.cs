using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AccountingSystem.API.Models
{
    public class Role
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } // Admin, Accounting, Management
    }

    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; }

        [JsonIgnore]
        public string PasswordHash { get; set; }

        public string FullName { get; set; }

        public int RoleId { get; set; }
        public Role Role { get; set; }
    }
}