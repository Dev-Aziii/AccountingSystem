namespace AccountingSystem.Shared.DTOs
{
    public class UserDTO
    {
        public int Id { get; set; }
        public string? Email { get; set; }      // Nullable to avoid nullable reference type warnings
        public string? FullName { get; set; }
        public string? RoleName { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
    }
}