using AccountingSystem.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Numerics;

namespace AccountingSystem.API.Data
{
    public class AccountingDbContext(DbContextOptions<AccountingDbContext> options) : DbContext(options)
    {

        // Auth
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }

        // General Ledger
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }

        // Partners & Operations
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Bill> Bills { get; set; } // AP
        public DbSet<Invoice> Invoices { get; set; } // AR
        public DbSet<Payment> Payments { get; set; }

        // System
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Accounting" },
                new Role { Id = 3, Name = "Management" }
            );

            // Configure Decimal Precision for Financials (18, 2)
            var decimalProps = modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

            foreach (var property in decimalProps)
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            // Enforce Unique Constraints
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<Account>().HasIndex(a => a.Code).IsUnique();
        }
    }
}