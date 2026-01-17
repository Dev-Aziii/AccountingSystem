using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace AccountingSystem.API.Models
{
    public class Account
    {
        public int Id { get; set; }

        [Required]
        public string Code { get; set; } // e.g., "1001"

        [Required]
        public string Name { get; set; } // e.g., "Cash on Hand"

        [Required]
        public string Type { get; set; } // Asset, Liability, Equity, Revenue, Expense

        public decimal Balance { get; set; } = 0; // Cached balance
        public bool IsActive { get; set; } = true;
    }

    public class JournalEntry
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public string Reference { get; set; } // External Doc ID

        public bool IsPosted { get; set; } = false;

        public string CreatedBy { get; set; } // Username
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
    }

    public class JournalEntryLine
    {
        public int Id { get; set; }

        public int JournalEntryId { get; set; }
        [JsonIgnore]
        public JournalEntry JournalEntry { get; set; }

        public int AccountId { get; set; }
        public Account Account { get; set; }

        public decimal Debit { get; set; } = 0;
        public decimal Credit { get; set; } = 0;
    }
}