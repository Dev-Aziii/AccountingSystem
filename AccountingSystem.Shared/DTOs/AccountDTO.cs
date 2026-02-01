using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    public class AccountDTO
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class CreateAccountDTO
    {
        [Required]
        [StringLength(10, ErrorMessage = "Code is too long.")]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty; // Asset, Liability, Equity, Revenue, Expense
    }

    public class UpdateAccountDTO : CreateAccountDTO
    {
        public int Id { get; set; }
    }
}