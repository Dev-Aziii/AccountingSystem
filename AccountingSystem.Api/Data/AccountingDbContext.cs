using Microsoft.EntityFrameworkCore;
using AccountingSystem.API.Models;
using AccountingSystem.Shared.Enums;
using AccountingSystem.API.Services.Interfaces;

namespace AccountingSystem.API.Data
{
    public class AccountingDbContext : DbContext
    {
        private readonly ITenantService _tenantService;

        // Constructor Injection for Tenant Service
        public AccountingDbContext(DbContextOptions<AccountingDbContext> options, ITenantService tenantService) : base(options)
        {
            _tenantService = tenantService;
        }

        // Tenants
        public DbSet<Company> Companies { get; set; }

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
        public DbSet<Bill> Bills { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Multi-Tenancy Global Filters ---
            // Automatically filter data by the current CompanyId
            // We skip this check if CompanyId is 0 (System Admin / Registration context)

            // Expression to filter: (e.CompanyId == _tenantService.GetCurrentTenant())

            modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Account>().HasQueryFilter(e => e.CompanyId == _tenantService.GetCurrentTenant()); // Accounts usually hard deleted, no IsDeleted check needed unless added
            modelBuilder.Entity<JournalEntry>().HasQueryFilter(e => e.CompanyId == _tenantService.GetCurrentTenant());

            modelBuilder.Entity<Vendor>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Bill>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Invoice>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());

            // --- Enums & Conversions (Existing) ---
            modelBuilder.Entity<Bill>().Property(b => b.Status).HasConversion<string>();
            modelBuilder.Entity<Invoice>().Property(i => i.Status).HasConversion<string>();
            modelBuilder.Entity<Payment>().Property(p => p.PaymentMethod).HasConversion<string>();
            modelBuilder.Entity<Payment>().Property(p => p.Type).HasConversion<string>();

            // --- Decimal Precision (Existing) ---
            var decimalProps = modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));
            foreach (var property in decimalProps)
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            // --- Constraints ---
            // Emails are now unique PER COMPANY, not globally? 
            // Ideally unique globally for login, but let's keep simple index for now.
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            // Account Codes unique PER COMPANY
            modelBuilder.Entity<Account>().HasIndex(a => new { a.Code, a.CompanyId }).IsUnique();

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Accounting" },
                new Role { Id = 3, Name = "Management" }
            );
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var currentTenantId = _tenantService.GetCurrentTenant();

            foreach (var entry in entries)
            {
                // Auto-assign CompanyId on creation
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (currentTenantId != 0)
                    {
                        entry.Entity.CompanyId = currentTenantId;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}