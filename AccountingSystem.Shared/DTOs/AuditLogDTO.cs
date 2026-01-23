namespace AccountingSystem.Shared.DTOs
{
    public class AuditLogDTO
    {
        public int Id { get; set; }
        public string UserEmail { get; set; } // The user who performed the action
        public string Action { get; set; }    // POST, PUT, DELETE
        public string EntityName { get; set; } // e.g., /api/invoices
        public string EntityId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Changes { get; set; }   // JSON Payload
    }
}