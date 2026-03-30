using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Identity
{
    public class IdentityAuthDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
    {
        public IdentityAuthDbContext(DbContextOptions<IdentityAuthDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(x => x.UserName).HasMaxLength(100);
                entity.Property(x => x.NormalizedUserName).HasMaxLength(100);
                entity.Property(x => x.Email).HasMaxLength(100);
                entity.Property(x => x.NormalizedEmail).HasMaxLength(100);
                entity.Property(x => x.CompanyId).IsRequired();
                entity.Property(x => x.FullName).IsRequired().HasMaxLength(100);
                entity.Property(x => x.Status).IsRequired().HasMaxLength(20);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.Property(x => x.IsDeleted).HasDefaultValue(false);
                entity.HasIndex(x => x.CompanyId);
                entity.HasIndex(x => x.LegacyUserId)
                    .IsUnique()
                    .HasFilter("[LegacyUserId] IS NOT NULL");
            });

            builder.Entity<ApplicationRole>().HasData(
                new ApplicationRole
                {
                    Id = 1,
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = "identity-role-admin"
                },
                new ApplicationRole
                {
                    Id = 2,
                    Name = "Accounting",
                    NormalizedName = "ACCOUNTING",
                    ConcurrencyStamp = "identity-role-accounting"
                },
                new ApplicationRole
                {
                    Id = 3,
                    Name = "Management",
                    NormalizedName = "MANAGEMENT",
                    ConcurrencyStamp = "identity-role-management"
                },
                new ApplicationRole
                {
                    Id = 4,
                    Name = "SuperAdmin",
                    NormalizedName = "SUPERADMIN",
                    ConcurrencyStamp = "identity-role-superadmin"
                });
        }
    }
}
