using AccountingSystem.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    public class DocumentSequenceDTO
    {
        public DocumentType DocumentType { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public int NextNumber { get; set; }
    }

    public class UpdateDocumentSequenceDTO
    {
        public DocumentType DocumentType { get; set; }

        [Required]
        [MaxLength(20)]
        public string Prefix { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int NextNumber { get; set; }
    }
}
