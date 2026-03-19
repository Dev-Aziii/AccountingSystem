using Microsoft.EntityFrameworkCore;
using AccountingSystem.API.Models;
using AccountingSystem.Shared.Enums;
using AccountingSystem.API.Services.Interfaces;

namespace AccountingSystem.API.Data
{
    public class AccountingDbContext : DbContext
    {
        private readonly ITenantService _tenantService;

        public AccountingDbContext(DbContextOptions<AccountingDbContext> options, ITenantService tenantService) : base(options)
        {
            _tenantService = tenantService;
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<FiscalYearClose> FiscalYearCloses { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<DocumentSequence> DocumentSequences { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SuperAdminAuditLog> SuperAdminAuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Multi-Tenancy Global Filters ---
            modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Account>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<JournalEntry>().HasQueryFilter(e => e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<JournalEntryLine>().HasQueryFilter(e =>
                e.JournalEntry.CompanyId == _tenantService.GetCurrentTenant() &&
                !e.Account.IsDeleted &&
                e.Account.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<FiscalYearClose>().HasQueryFilter(e => e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Vendor>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Bill>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Invoice>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted && e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<DocumentSequence>().HasQueryFilter(e => e.CompanyId == _tenantService.GetCurrentTenant());
            modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.CompanyId == _tenantService.GetCurrentTenant());

            // --- AuditLog Config ---
            modelBuilder.Entity<AuditLog>().Property(a => a.CompanyId).IsRequired();

            // --- Enums & Conversions ---
            modelBuilder.Entity<Bill>().Property(b => b.Status).HasConversion<string>();
            modelBuilder.Entity<Invoice>().Property(i => i.Status).HasConversion<string>();
            modelBuilder.Entity<Payment>().Property(p => p.PaymentMethod).HasConversion<string>();
            modelBuilder.Entity<Payment>().Property(p => p.Type).HasConversion<string>();
            modelBuilder.Entity<DocumentSequence>().Property(d => d.DocumentType).HasConversion<string>();
            modelBuilder.Entity<DocumentSequence>().Property(d => d.RowVersion).IsRowVersion();

            // --- Decimal Precision ---
            var decimalProps = modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));
            foreach (var property in decimalProps)
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            // --- Constraints ---
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Account>().HasIndex(a => new { a.Code, a.CompanyId }).IsUnique();
            modelBuilder.Entity<FiscalYearClose>().HasIndex(f => new { f.CompanyId, f.FiscalYear }).IsUnique();
            modelBuilder.Entity<JournalEntry>().HasIndex(j => new { j.CompanyId, j.Date });
            modelBuilder.Entity<DocumentSequence>().HasIndex(d => new { d.CompanyId, d.DocumentType }).IsUnique();
            modelBuilder.Entity<FiscalYearClose>()
                .HasOne(f => f.ClosingJournalEntry)
                .WithMany()
                .HasForeignKey(f => f.ClosingJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---  ROLES ---
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Accounting" },
                new Role { Id = 3, Name = "Management" },
                new Role { Id = 4, Name = "SuperAdmin" } 
            );
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var currentTenantId = _tenantService.GetCurrentTenant();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (currentTenantId != 0 && entry.Entity.CompanyId == 0)
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
