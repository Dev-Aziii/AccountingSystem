# Identity Migration Plan

## Current Legacy Auth Schema Summary

The current authentication system is custom and remains the active path in this phase.

- `Users` table
  - `Id` `int` primary key
  - `CompanyId` `int`
  - `Email` `nvarchar(100)` unique
  - `PasswordHash` `nvarchar(max)` storing Base64 HMACSHA512 hash
  - `PasswordSalt` `nvarchar(max)` storing Base64 salt
  - `FullName` `nvarchar(100)`
  - `RoleId` `int` foreign key to `Roles`
  - `Status` `nvarchar(20)`
  - `IsActive` `bit`
  - `IsDeleted` `bit`
  - `CreatedAt`, `UpdatedAt`, `CreatedById`
  - `AccessFailedCount` `int`
  - `LockoutEndUtc` `datetime2`
- `Roles` table
  - int-keyed roles seeded as `Admin`, `Accounting`, `Management`, `SuperAdmin`
- JWT contract emitted today
  - `ClaimTypes.Name`
  - `ClaimTypes.Role`
  - custom `UserId`
  - custom `role`
  - custom `FullName`
  - custom `CompanyId`
  - custom `CompanyName`

## Chosen Migration Approach

Adopt ASP.NET Core Identity side-by-side in the same database using int-keyed Identity entities and a dedicated Identity EF Core context.

- New Identity user type: `ApplicationUser : IdentityUser<int>`
- New Identity role type: `ApplicationRole : IdentityRole<int>`
- New context: `IdentityAuthDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>`
- Legacy `AccountingDbContext`, `User`, and `Role` remain unchanged and continue to back the live login flow

## Why This Approach Was Chosen

Directly converting the current `User` entity to `IdentityUser<int>` is not the safest path for this codebase.

- The key type already aligns with Identity (`int`), so a side-by-side migration does not need key translation.
- The legacy `User` currently inherits `BaseEntity`, which carries tenant and lifecycle fields used by global query filters, soft delete, and existing business logic.
- Controllers, middleware, query filters, and audit paths are wired to the current `Users` and `Roles` tables.
- Replacing the active `User` model in this phase would turn groundwork into a breaking auth/data rewrite.

The side-by-side model keeps the current system stable while making later Identity cutover incremental.

## Schema Impact In This Phase

This phase introduces the standard Identity schema in the same database without altering the legacy auth tables.

- New Identity tables:
  - `AspNetUsers`
  - `AspNetRoles`
  - `AspNetUserRoles`
  - `AspNetUserClaims`
  - `AspNetUserLogins`
  - `AspNetUserTokens`
  - `AspNetRoleClaims`
- `AspNetRoles` is seeded with `Admin`, `Accounting`, `Management`, and `SuperAdmin`
- `AspNetUsers` includes preparatory bridge fields:
  - `LegacyUserId` nullable `int`
  - `CompanyId` `int`
  - `FullName`
  - `Status`
  - `IsActive`
  - `IsDeleted`
  - `CreatedAt`
  - `UpdatedAt`

No data is backfilled into Identity tables in this phase. The legacy `Users` and `Roles` tables remain the system of record.

## Legacy Password Handling

Legacy passwords are not migrated in bulk in this phase.

- Current users continue authenticating against the existing HMACSHA512 + salt fields in `Users`
- A dedicated legacy password verification service has been introduced so future hybrid login can verify old hashes without duplicating logic
- The future migration path is:
  - user attempts login after Identity cutover
  - system verifies the legacy password hash from `Users`
  - system creates or updates the linked `AspNetUsers` record
  - system writes an Identity-compatible password hash
  - system links the new Identity user to the legacy record via `LegacyUserId`

## Forced Password Reset

Forced password reset is **not** required in this phase.

- No forced reset is planned for normal accounts during initial groundwork
- A forced reset remains the fallback only for accounts whose legacy password data is missing, corrupted, or cannot be safely migrated later

## Role And Claim Mapping

Role names remain exactly the same across legacy auth and Identity.

- Legacy `Roles.Name` values map directly to `AspNetRoles.Name`
- The seeded Identity role IDs intentionally align to the legacy role IDs for simpler phased mapping
- Existing API and Blazor role checks continue to rely on the same role-name strings

JWT claims also remain unchanged in this phase.

- The active login flow still emits the current JWT claim set
- A dedicated token factory now owns JWT creation so later Identity-based login can emit the same claims and preserve API/client compatibility

## Rollback Considerations

Rollback is straightforward because the active auth path is unchanged.

- Revert the new Identity context, entity classes, DI registration, preparatory abstractions, and documentation
- Roll back the preparatory Identity migration if it has been applied
- No rollback is required for legacy JWT auth logic beyond reverting the new seams if desired
- Because no controllers, DTOs, or active login endpoints were switched to Identity, rollback does not require client or contract changes
