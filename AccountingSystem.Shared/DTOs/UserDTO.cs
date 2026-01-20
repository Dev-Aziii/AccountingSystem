namespace AccountingSystem.Shared.DTOs
{
    public class UserDTO
    {
        public int Id { get; set; }
        public string Email { get; set; } // Changed from Username
        public string FullName { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
    }
}