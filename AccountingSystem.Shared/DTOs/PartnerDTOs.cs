using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Shared.DTOs
{
    // --- VENDORS ---
    public class VendorDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string ContactPerson { get; set; }
    }

    public class CreateVendorDTO
    {
        [Required]
        public string Name { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        public string ContactPerson { get; set; }
    }

    public class UpdateVendorDTO : CreateVendorDTO
    {
        public int Id { get; set; }
    }

    // --- CUSTOMERS ---
    public class CustomerDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    public class CreateCustomerDTO
    {
        [Required]
        public string Name { get; set; }
        [EmailAddress]
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    public class UpdateCustomerDTO : CreateCustomerDTO
    {
        public int Id { get; set; }
    }
}