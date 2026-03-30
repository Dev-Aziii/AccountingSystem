# Authorization Scope Cleanup

## Scope Model

- `SuperAdmin` is the only platform-scoped role.
- `TenantOwner` is the highest tenant-scoped authority.
- `Accounting` and `Management` remain tenant-scoped roles only.
- Tenant-scoped authorization now requires both:
  - an allowed tenant role
  - a valid positive `CompanyId` claim
- `SuperAdmin` remains excluded from tenant operational/business pages and endpoints in this phase.
- No tenant impersonation or cross-scope bypass was introduced.

## Shared Policy Model

Shared policy names and claim/role evaluators now live in `AccountingSystem.Shared/Security`:

- `ApplicationAuthorizationPolicies.cs`
- `ApplicationAuthorizationScopeEvaluator.cs`

Defined policies:

- `RequireSuperAdmin`
  - Authenticated user with `SuperAdmin`
- `RequireTenantAccess`
  - Authenticated user with `TenantOwner`, `Accounting`, or `Management`
  - Must include a valid positive `CompanyId`
- `RequireTenantOwner`
  - Authenticated `TenantOwner`
  - Must include a valid positive `CompanyId`
- `RequireTenantAccountingAccess`
  - Authenticated `TenantOwner` or `Accounting`
  - Must include a valid positive `CompanyId`
- `RequireTenantOperationalAccess`
  - Authenticated `TenantOwner`, `Accounting`, or `Management`
  - Must include a valid positive `CompanyId`

## Updated API Checks

API policy registration is centralized in:

- `AccountingSystem.Api/Configuration/ApplicationAuthorizationExtensions.cs`
- `AccountingSystem.Api/Program.cs`

Updated platform administration checks:

- `AccountingSystem.Api/Controllers/SuperAdminController.cs`
  - `RequireSuperAdmin`

Updated tenant administration checks:

- `AccountingSystem.Api/Controllers/UsersController.cs`
  - `RequireTenantOwner`
- `AccountingSystem.Api/Controllers/AuditLogsController.cs`
  - `RequireTenantOwner`
- `AccountingSystem.Api/Controllers/DocumentNumberingController.cs`
  - `RequireTenantOwner`
- `AccountingSystem.Api/Controllers/CompaniesController.cs`
  - controller: `RequireTenantAccess`
  - update current company: `RequireTenantOwner`
- `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
  - fiscal year close: `RequireTenantOwner`

Updated tenant accounting checks:

- `AccountingSystem.Api/Controllers/BusinessControllers.cs`
  - `RequireTenantAccountingAccess`
- `AccountingSystem.Api/Controllers/PaymentController.cs`
  - `RequireTenantAccountingAccess`
- `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
  - create/update/delete/restore accounts
  - post journal entry
  - all moved to `RequireTenantAccountingAccess`

Updated tenant operational checks:

- `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
  - chart of accounts
  - trial balance
  - fiscal years
  - all moved to `RequireTenantOperationalAccess`
- `AccountingSystem.Api/Controllers/ReportsController.cs`
  - controller moved to `RequireTenantOperationalAccess`

Reviewed but intentionally unchanged self-service endpoints:

- `AccountingSystem.Api/Controllers/AuthController.cs`
  - `/api/auth/profile`
  - `/api/auth/password`
  - `/api/auth/mfa/*`
  - remain authenticated-user endpoints in this phase

## Middleware And Service Isolation

Reviewed middleware:

- `AccountingSystem.Api/Middleware/TenantAccessMiddleware.cs`
  - now evaluates scope from `HttpContext.User`
  - `SuperAdmin` bypass remains explicit
  - tenant-scoped principals without a valid `CompanyId` are denied with `403`
  - blocked users still denied
  - blocked or suspended tenants still denied
- `AccountingSystem.Api/Services/TenantService.cs`
  - now reads tenant context through the shared scope evaluator

Reviewed tenant filter bypass paths:

- `AccountingSystem.Api/Controllers/UsersController.cs`
  - archived list now reapplies `CompanyId == currentTenantId`
  - restore now reapplies `CompanyId == currentTenantId`
- `AccountingSystem.Api/Services/BusinessServices.cs`
  - archived vendors list now reapplies `CompanyId == currentTenantId`
  - restore vendor now reapplies `CompanyId == currentTenantId`
  - archived customers list now reapplies `CompanyId == currentTenantId`
  - restore customer now reapplies `CompanyId == currentTenantId`
- `AccountingSystem.Api/Services/LedgerService.cs`
  - archived accounts list now reapplies `CompanyId == currentTenantId`
  - restore account now reapplies `CompanyId == currentTenantId`
- `AccountingSystem.Api/Services/YearEndCloseService.cs`
  - retained earnings lookup now reapplies `CompanyId == currentTenantId`
  - invalid tenant context now throws
- `AccountingSystem.Api/Services/DocumentSequenceService.cs`
  - invalid `companyId <= 0` now throws before any `IgnoreQueryFilters()` access

Platform-scoped `IgnoreQueryFilters()` usage in `SuperAdminController` remains platform-only.

## Updated Client Checks

Client policy registration now mirrors the API in:

- `AccountingSystem.Client/Program.cs`

Updated platform-only client surfaces:

- `AccountingSystem.Client/Layout/NavMenu.razor`
  - super-admin section uses `RequireSuperAdmin`
- `AccountingSystem.Client/Pages/SuperAdmin/AdminAuditLogs.razor`
- `AccountingSystem.Client/Pages/SuperAdmin/GlobalUserManager.razor`
- `AccountingSystem.Client/Pages/SuperAdmin/SystemDashboard.razor`
- `AccountingSystem.Client/Pages/SuperAdmin/TenantManager.razor`
  - all use `RequireSuperAdmin`

Updated tenant admin client surfaces:

- `AccountingSystem.Client/Layout/NavMenu.razor`
  - tenant administration section uses `RequireTenantOwner`
- `AccountingSystem.Client/Pages/Admin/AuditLogs.razor`
- `AccountingSystem.Client/Pages/Admin/CompanySettings.razor`
- `AccountingSystem.Client/Pages/Admin/UserManagement.razor`
  - all use `RequireTenantOwner`

Updated tenant operational client surfaces:

- `AccountingSystem.Client/Layout/NavMenu.razor`
  - dashboard/reporting sections use `RequireTenantOperationalAccess`
- `AccountingSystem.Client/Pages/Dashboard.razor`
- `AccountingSystem.Client/Pages/Reports/FinancialReports.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/Customers.razor`
- `AccountingSystem.Client/Pages/GeneralLedger/Accounts.razor`
- `AccountingSystem.Client/Shared/Components/CustomerSelect.razor`

Updated tenant accounting client surfaces:

- `AccountingSystem.Client/Layout/NavMenu.razor`
  - GL/AP/AR section uses `RequireTenantAccountingAccess`
- `AccountingSystem.Client/Pages/AccountsPayable/BillList.razor`
- `AccountingSystem.Client/Pages/AccountsPayable/Bills.razor`
- `AccountingSystem.Client/Pages/AccountsPayable/Index.razor`
- `AccountingSystem.Client/Pages/AccountsPayable/Vendors.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/Index.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/InvoiceList.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/Invoices.razor`
- `AccountingSystem.Client/Pages/AccountsReceivable/ReceivePayment.razor`
- `AccountingSystem.Client/Pages/GeneralLedger/JournalEntries.razor`
- accounting-only action buttons and archived tabs inside:
  - `AccountingSystem.Client/Pages/AccountsReceivable/Customers.razor`
  - `AccountingSystem.Client/Pages/GeneralLedger/Accounts.razor`
- `AccountingSystem.Client/Shared/Components/VendorSelect.razor`

Reviewed but intentionally unchanged self-service page:

- `AccountingSystem.Client/Pages/Auth/UserProfile.razor`
  - remains authenticated-user only in this phase

## Places Reviewed

Shared:

- `AccountingSystem.Shared/Security/ApplicationRoles.cs`
- `AccountingSystem.Shared/Security/ApplicationAuthorizationPolicies.cs`
- `AccountingSystem.Shared/Security/ApplicationAuthorizationScopeEvaluator.cs`

API:

- `AccountingSystem.Api/Program.cs`
- `AccountingSystem.Api/Configuration/ApplicationAuthorizationExtensions.cs`
- `AccountingSystem.Api/Middleware/TenantAccessMiddleware.cs`
- `AccountingSystem.Api/Services/TenantService.cs`
- `AccountingSystem.Api/Controllers/AuditLogsController.cs`
- `AccountingSystem.Api/Controllers/BusinessControllers.cs`
- `AccountingSystem.Api/Controllers/CompaniesController.cs`
- `AccountingSystem.Api/Controllers/DocumentNumberingController.cs`
- `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
- `AccountingSystem.Api/Controllers/PaymentController.cs`
- `AccountingSystem.Api/Controllers/ReportsController.cs`
- `AccountingSystem.Api/Controllers/SuperAdminController.cs`
- `AccountingSystem.Api/Controllers/UsersController.cs`
- `AccountingSystem.Api/Services/BusinessServices.cs`
- `AccountingSystem.Api/Services/DocumentSequenceService.cs`
- `AccountingSystem.Api/Services/LedgerService.cs`
- `AccountingSystem.Api/Services/YearEndCloseService.cs`

Client:

- `AccountingSystem.Client/Program.cs`
- `AccountingSystem.Client/Layout/NavMenu.razor`
- all tenant/admin/super-admin pages listed above

Tests:

- `AccountingSystem.API.Tests/AuthorizationScopeTests.cs`
- `AccountingSystem.Client.Tests/NavMenuAuthorizationTests.cs`

## Verification Performed

Automated verification completed:

- `dotnet build AccountingSystem.sln`
- `dotnet test AccountingSystem.API.Tests/AccountingSystem.API.Tests.csproj`
- `dotnet test AccountingSystem.Client.Tests/AccountingSystem.Client.Tests.csproj`

Test coverage added for:

- shared scope evaluator role and tenant-context behavior
- tenant access middleware behavior
- archived/restore cross-tenant boundary regression checks
- invalid document sequence tenant context rejection
- nav visibility for `SuperAdmin`, `TenantOwner`, `Accounting`, and `Management`

## Manual Verification

1. Sign in as `SuperAdmin` and confirm only platform pages are visible:
   - system dashboard
   - tenant manager
   - global users
   - super-admin audit logs
2. Confirm `SuperAdmin` does not see tenant business navigation:
   - dashboard
   - company settings
   - GL/AP/AR
   - financial reports
3. Sign in as `TenantOwner` and confirm access is limited to the current tenant:
   - user management
   - tenant audit logs
   - company settings
   - document numbering
   - fiscal year close
4. Sign in as `Accounting` and confirm:
   - GL/AP/AR and related actions remain available
   - tenant administration pages remain hidden and inaccessible
5. Sign in as `Management` and confirm:
   - dashboard and financial reporting remain available
   - accounting-only edit/archive/restore actions remain hidden and inaccessible
6. On archived users/vendors/customers/accounts screens, confirm guessed IDs from another tenant are not returned or restorable.
7. Confirm a tenant-scoped token without a valid `CompanyId` claim is rejected with `403`.
8. Confirm blocked users and blocked/suspended tenant companies are still rejected.

## Migrations

- None added in this phase.
