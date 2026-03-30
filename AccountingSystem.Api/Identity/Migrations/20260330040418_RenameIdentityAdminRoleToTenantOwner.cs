using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Api.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RenameIdentityAdminRoleToTenantOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @CanonicalRoleId INT;

SELECT TOP (1) @CanonicalRoleId = [Id]
FROM [AspNetRoles]
WHERE [Name] IN ('Admin', 'TenantOwner')
ORDER BY CASE WHEN [Id] = 1 THEN 0 ELSE 1 END,
         CASE WHEN [Name] = 'TenantOwner' THEN 0 ELSE 1 END,
         [Id];

IF @CanonicalRoleId IS NULL
BEGIN
    INSERT INTO [AspNetRoles] ([Name], [NormalizedName], [ConcurrencyStamp])
    VALUES ('TenantOwner', 'TENANTOWNER', 'identity-role-tenantowner');

    SET @CanonicalRoleId = CAST(SCOPE_IDENTITY() AS INT);
END
ELSE
BEGIN
    UPDATE [AspNetRoles]
    SET [Name] = 'TenantOwner',
        [NormalizedName] = 'TENANTOWNER',
        [ConcurrencyStamp] = 'identity-role-tenantowner'
    WHERE [Id] = @CanonicalRoleId;
END;

DELETE [DuplicateUserRole]
FROM [AspNetUserRoles] AS [DuplicateUserRole]
WHERE [DuplicateUserRole].[RoleId] IN (
        SELECT [Id]
        FROM [AspNetRoles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    )
  AND EXISTS (
        SELECT 1
        FROM [AspNetUserRoles] AS [CanonicalUserRole]
        WHERE [CanonicalUserRole].[UserId] = [DuplicateUserRole].[UserId]
          AND [CanonicalUserRole].[RoleId] = @CanonicalRoleId
    );

UPDATE [AspNetUserRoles]
SET [RoleId] = @CanonicalRoleId
WHERE [RoleId] IN (
        SELECT [Id]
        FROM [AspNetRoles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    );

UPDATE [AspNetRoleClaims]
SET [RoleId] = @CanonicalRoleId
WHERE [RoleId] IN (
        SELECT [Id]
        FROM [AspNetRoles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    );

DELETE FROM [AspNetRoles]
WHERE [Name] IN ('Admin', 'TenantOwner')
  AND [Id] <> @CanonicalRoleId;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @CanonicalRoleId INT;

SELECT TOP (1) @CanonicalRoleId = [Id]
FROM [AspNetRoles]
WHERE [Name] IN ('Admin', 'TenantOwner')
ORDER BY CASE WHEN [Id] = 1 THEN 0 ELSE 1 END,
         CASE WHEN [Name] = 'Admin' THEN 0 ELSE 1 END,
         [Id];

IF @CanonicalRoleId IS NULL
BEGIN
    INSERT INTO [AspNetRoles] ([Name], [NormalizedName], [ConcurrencyStamp])
    VALUES ('Admin', 'ADMIN', 'identity-role-admin');

    SET @CanonicalRoleId = CAST(SCOPE_IDENTITY() AS INT);
END
ELSE
BEGIN
    UPDATE [AspNetRoles]
    SET [Name] = 'Admin',
        [NormalizedName] = 'ADMIN',
        [ConcurrencyStamp] = 'identity-role-admin'
    WHERE [Id] = @CanonicalRoleId;
END;

DELETE [DuplicateUserRole]
FROM [AspNetUserRoles] AS [DuplicateUserRole]
WHERE [DuplicateUserRole].[RoleId] IN (
        SELECT [Id]
        FROM [AspNetRoles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    )
  AND EXISTS (
        SELECT 1
        FROM [AspNetUserRoles] AS [CanonicalUserRole]
        WHERE [CanonicalUserRole].[UserId] = [DuplicateUserRole].[UserId]
          AND [CanonicalUserRole].[RoleId] = @CanonicalRoleId
    );

UPDATE [AspNetUserRoles]
SET [RoleId] = @CanonicalRoleId
WHERE [RoleId] IN (
        SELECT [Id]
        FROM [AspNetRoles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    );

UPDATE [AspNetRoleClaims]
SET [RoleId] = @CanonicalRoleId
WHERE [RoleId] IN (
        SELECT [Id]
        FROM [AspNetRoles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    );

DELETE FROM [AspNetRoles]
WHERE [Name] IN ('Admin', 'TenantOwner')
  AND [Id] <> @CanonicalRoleId;
");
        }
    }
}
