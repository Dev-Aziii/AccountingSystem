using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    public class AccountDTO
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class CreateAccountDTO
    {
        [Required]
        [StringLength(10, ErrorMessage = "Code is too long.")]
        public string Code { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Type { get; set; } // Asset, Liability, Equity, Revenue, Expense
    }

    public class UpdateAccountDTO : CreateAccountDTO
    {
        public int Id { get; set; }
    }
}