# Role Rename Notes

## Role Change
- Old canonical tenant-scoped role: `Admin`
- New canonical tenant-scoped role: `TenantOwner`
- Unchanged roles: `SuperAdmin`, `Accounting`, `Management`

## Scope And Contract Notes
- Runtime and stored role values now use `TenantOwner` for tenant owners.
- UI display text uses `Tenant Owner`.
- `SuperAdmin` remains the only platform-scoped role.
- `CompanyRegisterDTO.AdminEmail` and `CompanyRegisterDTO.AdminFullName` were intentionally left unchanged in this phase to avoid request DTO churn.
- `AuthResponseDTO.Role`, `CurrentProfileDTO.Role`, `UserDTO.RoleName`, and `GlobalUserDTO.Role` now return `TenantOwner` for tenant owners.

## Migration Behavior
- Legacy store migration: `20260330040418_RenameAdminRoleToTenantOwner`
  - Picks a canonical role row from `Roles` where the name is `Admin` or `TenantOwner`, preferring role id `1`.
  - Renames that canonical row to `TenantOwner`.
  - Repoints `Users.RoleId` from duplicate `Admin` or `TenantOwner` rows to the canonical row.
  - Deletes duplicate `Admin` or `TenantOwner` role rows.
  - Is idempotent for already-migrated databases.
- Identity store migration: `20260330040418_RenameIdentityAdminRoleToTenantOwner`
  - Picks a canonical role row from `AspNetRoles` where the name is `Admin` or `TenantOwner`, preferring role id `1`.
  - Renames that canonical row to `TenantOwner`, updates `NormalizedName` to `TENANTOWNER`, and updates `ConcurrencyStamp`.
  - Removes duplicate `AspNetUserRoles` entries that would conflict after remapping.
  - Repoints `AspNetUserRoles` and `AspNetRoleClaims` from duplicate `Admin` or `TenantOwner` rows to the canonical row.
  - Deletes duplicate `Admin` or `TenantOwner` role rows.
  - Is idempotent for already-migrated databases.
- Neither migration changes `SuperAdmin` rows or grants new cross-tenant access.

## Files Changed
- `AccountingSystem.Shared/Security/ApplicationRoles.cs`
- `AccountingSystem.Api/GlobalUsings.cs`
- `AccountingSystem.API.Tests/GlobalUsings.cs`
- `AccountingSystem.Api/Controllers/AuditLogsController.cs`
- `AccountingSystem.Api/Controllers/BusinessControllers.cs`
- `AccountingSystem.Api/Controllers/CompaniesController.cs`
- `AccountingSystem.Api/Controllers/DocumentNumberingController.cs`
- `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
- `AccountingSystem.Api/Controllers/PaymentController.cs`
- `AccountingSystem.Api/Controllers/SuperAdminController.cs`
- `AccountingSystem.Api/Controllers/UsersController.cs`
- `AccountingSystem.Api/Data/AccountingDbContext.cs`
- `AccountingSystem.Api/Data/DataSeeder.cs`
- `AccountingSystem.Api/Identity/IdentityAuthDbContext.cs`
- `AccountingSystem.Api/Identity/Migrations/20260330040418_RenameIdentityAdminRoleToTenantOwner.cs`
- `AccountingSystem.Api/Identity/Migrations/20260330040418_RenameIdentityAdminRoleToTenantOwner.Designer.cs`
- `AccountingSystem.Api/Identity/Migrations/IdentityAuthDbContextModelSnapshot.cs`
- `AccountingSystem.Api/Middleware/TenantAccessMiddleware.cs`
- `AccountingSystem.Api/Migrations/20260330040418_RenameAdminRoleToTenantOwner.cs`
- `AccountingSystem.Api/Migrations/20260330040418_RenameAdminRoleToTenantOwner.Designer.cs`
- `AccountingSystem.Api/Migrations/AccountingDbContextModelSnapshot.cs`
- `AccountingSystem.Api/Models/AuthModels.cs`
- `AccountingSystem.Api/Services/AuthService.cs`
- `AccountingSystem.Client/_Imports.razor`
- `AccountingSystem.Client/Layout/NavMenu.razor`
- `AccountingSystem.Client/Pages/AccountsPayable/BillList.razor`
- `AccountingSystem.Client/Pages/AccountsPayable/Bills.razor`
- `AccountingSystem.Client/Pages/AccountsPayable/Index.razor`
- `AccountingSystem.Client/Pages/AccountsPayable/Vendors.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/Customers.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/Index.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/InvoiceList.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/Invoices.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/ReceivePayment.razor`
- `AccountingSystem.Client/Pages/Admin/AuditLogs.razor`
- `AccountingSystem.Client/Pages/Admin/CompanySettings.razor`
- `AccountingSystem.Client/Pages/Admin/UserManagement.razor`
- `AccountingSystem.Client/Pages/Auth/Login.razor`
- `AccountingSystem.Client/Pages/Auth/MfaLogin.razor`
- `AccountingSystem.Client/Pages/Auth/RegisterCompany.razor`
- `AccountingSystem.Client/Pages/GeneralLedger/Accounts.razor`
- `AccountingSystem.Client/Pages/GeneralLedger/JournalEntries.razor`
- `AccountingSystem.Client/Pages/SuperAdmin/GlobalUserManager.razor`
- `AccountingSystem.Client/Shared/Components/CustomerSelect.razor`
- `AccountingSystem.Client/Shared/Components/VendorSelect.razor`
- `AccountingSystem.API.Tests/UnitTest1.cs`

## Manual Verification Steps
1. Apply the new migrations.
2. Self-register a new tenant from `/register` and confirm the first user is created with role `TenantOwner`.
3. Sign in as that migrated or newly-created tenant owner and confirm access still works for:
   - `/admin/users`
   - `/admin/audit-logs`
   - `/admin/company-settings`
   - general ledger and payables pages that previously allowed tenant `Admin`
4. Open tenant user management and confirm the role picker shows `Tenant Owner`, `Accounting`, and `Management`.
5. Confirm a tenant owner cannot be created through tenant user creation with role `SuperAdmin`.
6. Confirm an existing database with tenant `Admin` users now returns `TenantOwner` in:
   - JWT `role` claim
   - current profile response
   - user-management responses
   - super-admin global user list
7. Confirm `SuperAdmin` users still reach `/superadmin/dashboard` and tenant users do not gain cross-tenant access.

## Related Notes
- `RBAC_ASSESSMENT.md` was intentionally left untouched in this phase.
