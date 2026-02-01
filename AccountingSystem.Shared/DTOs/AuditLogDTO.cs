namespace AccountingSystem.Shared.DTOs
{
    public class AuditLogDTO
    {
        public int Id { get; set; }
        public string UserEmail { get; set; } = string.Empty; // The user who performed the action
        public string Action { get; set; } = string.Empty;    // POST, PUT, DELETE
        public string EntityName { get; set; } = string.Empty; // e.g., /api/invoices
        public string EntityId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Changes { get; set; } = string.Empty;   // JSON Payload
    }
}