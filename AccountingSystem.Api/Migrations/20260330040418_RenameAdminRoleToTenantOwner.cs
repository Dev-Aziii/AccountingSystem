using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameAdminRoleToTenantOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @CanonicalRoleId INT;

SELECT TOP (1) @CanonicalRoleId = [Id]
FROM [Roles]
WHERE [Name] IN ('Admin', 'TenantOwner')
ORDER BY CASE WHEN [Id] = 1 THEN 0 ELSE 1 END,
         CASE WHEN [Name] = 'TenantOwner' THEN 0 ELSE 1 END,
         [Id];

IF @CanonicalRoleId IS NULL
BEGIN
    INSERT INTO [Roles] ([Name])
    VALUES ('TenantOwner');

    SET @CanonicalRoleId = CAST(SCOPE_IDENTITY() AS INT);
END
ELSE
BEGIN
    UPDATE [Roles]
    SET [Name] = 'TenantOwner'
    WHERE [Id] = @CanonicalRoleId;
END;

UPDATE [Users]
SET [RoleId] = @CanonicalRoleId
WHERE [RoleId] IN (
        SELECT [Id]
        FROM [Roles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    );

DELETE FROM [Roles]
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
FROM [Roles]
WHERE [Name] IN ('Admin', 'TenantOwner')
ORDER BY CASE WHEN [Id] = 1 THEN 0 ELSE 1 END,
         CASE WHEN [Name] = 'Admin' THEN 0 ELSE 1 END,
         [Id];

IF @CanonicalRoleId IS NULL
BEGIN
    INSERT INTO [Roles] ([Name])
    VALUES ('Admin');

    SET @CanonicalRoleId = CAST(SCOPE_IDENTITY() AS INT);
END
ELSE
BEGIN
    UPDATE [Roles]
    SET [Name] = 'Admin'
    WHERE [Id] = @CanonicalRoleId;
END;

UPDATE [Users]
SET [RoleId] = @CanonicalRoleId
WHERE [RoleId] IN (
        SELECT [Id]
        FROM [Roles]
        WHERE [Name] IN ('Admin', 'TenantOwner')
          AND [Id] <> @CanonicalRoleId
    );

DELETE FROM [Roles]
WHERE [Name] IN ('Admin', 'TenantOwner')
  AND [Id] <> @CanonicalRoleId;
");
        }
    }
}
