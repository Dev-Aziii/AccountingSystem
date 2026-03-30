# RBAC Assessment

Assessment date: 2026-03-30

This document inventories the current RBAC implementation in the `.NET 8` multi-tenant `AccountingSystem` solution before any role renames or behavior changes. It is based on the current code in:

- `AccountingSystem.Api`
- `AccountingSystem.Client`
- `AccountingSystem.Shared`

## 1. Executive Summary

- Current role strings are `SuperAdmin`, `Admin`, `Accounting`, and `Management`.
- `SuperAdmin` is the only platform-scoped role in the current implementation.
- `Admin`, `Accounting`, and `Management` are tenant-scoped by the data model and normal request flow, but `Admin` is overloaded:
  - initial tenant owner during self-registration
  - tenant administration role
  - participant in mixed business permissions such as `Admin,Accounting`
- There is no centralized role constants class, role enum, or named authorization policy for RBAC. Role names are scattered string literals across API controllers, services, Identity sync, Razor pages, navigation, DTOs, tests, and docs.
- Authorization is primarily enforced through:
  - server-side `[Authorize]` and `[Authorize(Roles = "...")]`
  - client-side `@attribute [Authorize(...)]` and `<AuthorizeView Roles="...">`
  - tenant scoping through `CompanyId` claims, `TenantService`, and `AccountingDbContext` query filters
- Current unsafe scope findings already exist:
  - several archived/restore flows use `IgnoreQueryFilters()` without re-applying tenant predicates
  - `YearEndCloseService.EnsureRetainedEarningsAccountAsync()` looks up account code `3100` across all tenants
  - these are current-state findings only; they are not changed in this phase

## 2. Current Role Sources And Propagation

| Area | Current implementation | Notes |
| --- | --- | --- |
| Legacy role store | `AccountingSystem.Api/Data/AccountingDbContext.cs` seeds `Role` rows for `Admin`, `Accounting`, `Management`, `SuperAdmin` | Legacy application data still carries role IDs and role names |
| Identity role store | `AccountingSystem.Api/Identity/IdentityAuthDbContext.cs` seeds `ApplicationRole` rows for the same 4 role names | Identity is the live auth store for passwords, MFA, email confirmation, and role membership sync |
| Bootstrap platform user | `AccountingSystem.Api/Data/DataSeeder.cs` ensures a host company and first `SuperAdmin` user | Uses `BootstrapAdmin:*` settings; bootstraps the platform user only |
| Identity role sync | `AccountingSystem.Api/Services/IdentityAccountService.cs` calls `EnsureRoleExistsAsync`, `RemoveFromRolesAsync`, and `AddToRoleAsync` | Identity users are normalized to one active role name |
| JWT emission | `AccountingSystem.Api/Services/JwtAuthTokenFactory.cs` writes both `ClaimTypes.Role` and `"role"` claims | Role string is copied verbatim into tokens |
| JWT request context | `AccountingSystem.Api/Middleware/JwtMiddleware.cs` copies `"role"`, `UserId`, and `CompanyId` into `HttpContext.Items` | `TenantAccessMiddleware` depends on this |
| Client auth parsing | `AccountingSystem.Client/Auth/CustomAuthStateProvider.cs` parses token claims and uses `"role"` as the role claim type | Client-side `AuthorizeView` and page authorization consume the parsed role |
| Authorization model | No custom named authorization policies were found for RBAC | Role checks are string literals; rate-limiting and CORS policies exist but are not RBAC policies |

Current role identifiers are not centralized. No `RoleNames`, `Roles`, or equivalent constants class exists in the repository.

## 3. Current Role Inventory

| Role | Effective scope today | Current meaning | Where created or seeded | Main checks | Notable behavior |
| --- | --- | --- | --- | --- | --- |
| `SuperAdmin` | Platform-scoped | Platform operator over tenants and global users | Seeded in `AccountingDbContext` and `IdentityAuthDbContext`; first user created in `DataSeeder.SeedDataAsync` | `SuperAdminController`, `TenantAccessMiddleware`, `AuthService.IsSuperAdminRole`, client super-admin pages and nav | Cross-tenant surfaces use `IgnoreQueryFilters()`; exempt from tenant suspension/blocking middleware and email-confirmation login requirement |
| `Admin` | Tenant-scoped by intent and normal flow | Tenant owner / tenant admin / privileged tenant operator | Seeded in both role stores; assigned during self-registration in `AuthService.RegisterCompanyAsync`; assignable from tenant user creation UI and API | `UsersController`, `AuditLogsController`, `CompaniesController` update, `DocumentNumberingController`, `GeneralLedgerController` close year, mixed business endpoints/pages | Name is ambiguous in a multi-tenant app; also used as the sentinel "cannot archive admin account" role |
| `Accounting` | Tenant-scoped | Accounting staff with transaction entry permissions | Seeded in both role stores; assignable in `AuthService.RegisterAsync` and tenant admin UI | Mixed tenant business controllers/pages, shared selects, reporting access | Usually paired with `Admin`; not used for platform access |
| `Management` | Tenant-scoped | Read/report-oriented tenant role | Seeded in both role stores; assignable in `AuthService.RegisterAsync` and tenant admin UI | GL read paths, AR customer access, reporting pages, nav | Read-oriented compared with `Admin` and `Accounting`; not used for platform access |

No `TenantOwner` role exists today.

## 4. Per-Role Usage Map

### 4.1 `SuperAdmin`

Current touchpoints:

- Legacy and Identity seeds:
  - `AccountingSystem.Api/Data/AccountingDbContext.cs`
  - `AccountingSystem.Api/Identity/IdentityAuthDbContext.cs`
- Bootstrap creation:
  - `AccountingSystem.Api/Data/DataSeeder.cs`
  - `AccountingSystem.Api/Configuration/BootstrapAdminSettings.cs`
  - `AccountingSystem.Api/.env.example`
- API authorization and behavior:
  - `AccountingSystem.Api/Controllers/SuperAdminController.cs`
  - `AccountingSystem.Api/Middleware/TenantAccessMiddleware.cs`
  - `AccountingSystem.Api/Services/AuthService.cs`
- Client authorization and navigation:
  - `AccountingSystem.Client/Layout/NavMenu.razor`
  - `AccountingSystem.Client/Pages/SuperAdmin/SystemDashboard.razor`
  - `AccountingSystem.Client/Pages/SuperAdmin/TenantManager.razor`
  - `AccountingSystem.Client/Pages/SuperAdmin/GlobalUserManager.razor`
  - `AccountingSystem.Client/Pages/SuperAdmin/AdminAuditLogs.razor`
  - `AccountingSystem.Client/Pages/Auth/Login.razor`
  - `AccountingSystem.Client/Pages/Auth/MfaLogin.razor`
- Tests and docs encode this behavior:
  - `AccountingSystem.API.Tests/UnitTest1.cs`
  - `EMAIL_CONFIRMATION_ENFORCEMENT.md`
  - `SECURITY_ASSESSMENT.md`
  - `DOCUMENTATION.md`

Scope conclusion:

- Platform-scoped.
- The only role with explicit cross-tenant controller surfaces and middleware exemptions.

### 4.2 `Admin`

Current touchpoints:

- Legacy and Identity seeds:
  - `AccountingSystem.Api/Data/AccountingDbContext.cs`
  - `AccountingSystem.Api/Identity/IdentityAuthDbContext.cs`
- Tenant owner creation:
  - `AccountingSystem.Api/Controllers/AuthController.cs`
  - `AccountingSystem.Api/Services/AuthService.cs`
  - `AccountingSystem.Shared/DTOs/AuthDTOs.cs`
  - `AccountingSystem.Client/Pages/Auth/RegisterCompany.razor`
- Tenant user creation and assignment:
  - `AccountingSystem.Api/Controllers/UsersController.cs`
  - `AccountingSystem.Api/Services/AuthService.cs`
  - `AccountingSystem.Client/Pages/Admin/UserManagement.razor`
- API authorization:
  - `AccountingSystem.Api/Controllers/AuditLogsController.cs`
  - `AccountingSystem.Api/Controllers/UsersController.cs`
  - `AccountingSystem.Api/Controllers/CompaniesController.cs`
  - `AccountingSystem.Api/Controllers/DocumentNumberingController.cs`
  - `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
  - `AccountingSystem.Api/Controllers/BusinessControllers.cs`
  - `AccountingSystem.Api/Controllers/PaymentController.cs`
- Non-authorization string uses:
  - `AccountingSystem.Api/Controllers/BusinessControllers.cs` fallback actor string `User.Identity?.Name ?? "Admin"`
  - `AccountingSystem.Api/Models/AuthModels.cs` role comments
- Client authorization and navigation:
  - `AccountingSystem.Client/Layout/NavMenu.razor`
  - `AccountingSystem.Client/Pages/Admin/AuditLogs.razor`
  - `AccountingSystem.Client/Pages/Admin/CompanySettings.razor`
  - `AccountingSystem.Client/Pages/Admin/UserManagement.razor`
  - `AccountingSystem.Client/Pages/AccountsPayable/*.razor`
  - `AccountingSystem.Client/Pages/AccountsReceivable/*.razor`
  - `AccountingSystem.Client/Pages/GeneralLedger/*.razor`
  - `AccountingSystem.Client/Shared/Components/CustomerSelect.razor`
  - `AccountingSystem.Client/Shared/Components/VendorSelect.razor`
  - `AccountingSystem.Client/Pages/SuperAdmin/GlobalUserManager.razor` role filter and badge class
- DTOs/contracts:
  - `AccountingSystem.Shared/DTOs/AuthDTOs.cs`
  - `AccountingSystem.Shared/DTOs/UserDTO.cs`
  - `AccountingSystem.Shared/DTOs/SuperAdminDTOs.cs`
- Tests and docs encode current `Admin` semantics:
  - `AccountingSystem.API.Tests/UnitTest1.cs`
  - `README.md`
  - `DOCUMENTATION.md`
  - `SECURITY_ASSESSMENT.md`
  - `IDENTITY_INTRODUCTION_NOTES.md`
  - `IDENTITY_MIGRATION_PLAN.md`

Scope conclusion:

- Tenant-scoped by current data model and ordinary request flow.
- Name is overloaded enough that it reads like a platform role even though it is usually treated as the tenant owner / tenant admin role.

### 4.3 `Accounting`

Current touchpoints:

- Seeds in both role stores:
  - `AccountingSystem.Api/Data/AccountingDbContext.cs`
  - `AccountingSystem.Api/Identity/IdentityAuthDbContext.cs`
- Assignable through tenant user creation:
  - `AccountingSystem.Api/Services/AuthService.cs`
  - `AccountingSystem.Client/Pages/Admin/UserManagement.razor`
- API authorization:
  - `AccountingSystem.Api/Controllers/BusinessControllers.cs`
  - `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
  - `AccountingSystem.Api/Controllers/PaymentController.cs`
- Client authorization:
  - `AccountingSystem.Client/Layout/NavMenu.razor`
  - `AccountingSystem.Client/Pages/AccountsPayable/*.razor`
  - `AccountingSystem.Client/Pages/AccountsReceivable/*.razor`
  - `AccountingSystem.Client/Pages/GeneralLedger/*.razor`
  - `AccountingSystem.Client/Shared/Components/CustomerSelect.razor`
  - `AccountingSystem.Client/Shared/Components/VendorSelect.razor`

Scope conclusion:

- Tenant-scoped.
- Operates inside the same tenant boundary as `Admin`; no platform surfaces use it.

### 4.4 `Management`

Current touchpoints:

- Seeds in both role stores:
  - `AccountingSystem.Api/Data/AccountingDbContext.cs`
  - `AccountingSystem.Api/Identity/IdentityAuthDbContext.cs`
- Assignable through tenant user creation:
  - `AccountingSystem.Api/Services/AuthService.cs`
  - `AccountingSystem.Client/Pages/Admin/UserManagement.razor`
- API authorization:
  - `AccountingSystem.Api/Controllers/GeneralLedgerController.cs`
- Client authorization:
  - `AccountingSystem.Client/Layout/NavMenu.razor`
  - `AccountingSystem.Client/Pages/AccountsReceivable/Customers.razor`
  - `AccountingSystem.Client/Pages/GeneralLedger/Accounts.razor`
  - `AccountingSystem.Client/Shared/Components/CustomerSelect.razor`

Scope conclusion:

- Tenant-scoped.
- Primarily read/report-oriented and not used for platform operations.

## 5. `Admin` Touchpoints And Ambiguity Inventory

### 5.1 Controllers

| File | Current `Admin` usage | Assessment |
| --- | --- | --- |
| `AccountingSystem.Api/Controllers/AuditLogsController.cs` | `[Authorize(Roles = "Admin")]` for tenant audit log viewing | Treats `Admin` as tenant operator, not platform admin |
| `AccountingSystem.Api/Controllers/UsersController.cs` | `[Authorize(Roles = "Admin")]` for list/create/archive/restore users | Strongest tenant-admin surface; current tenant owner/admin behavior lives here |
| `AccountingSystem.Api/Controllers/CompaniesController.cs` | `PUT /api/companies/current` requires `Admin` | `Admin` controls tenant company settings |
| `AccountingSystem.Api/Controllers/DocumentNumberingController.cs` | Controller requires `Admin` | `Admin` owns tenant document sequence configuration |
| `AccountingSystem.Api/Controllers/GeneralLedgerController.cs` | `Admin` appears in read/write GL permissions and is the only role allowed to close fiscal years | `Admin` mixes tenant-owner authority with accounting operations |
| `AccountingSystem.Api/Controllers/BusinessControllers.cs` | `Admin` participates in AP and AR module access; fallback actor string defaults to `"Admin"` | Mixed functional/operator meaning |
| `AccountingSystem.Api/Controllers/PaymentController.cs` | `CreateSource` allows `Admin,Accounting` | `Admin` is treated as a business operator, not just a tenant owner |

### 5.2 Services

| File | Current `Admin` usage | Assessment |
| --- | --- | --- |
| `AccountingSystem.Api/Services/AuthService.cs` | Self-registration loads the `Admin` role for the first tenant user and returns `Role = "Admin"` | This is the clearest current "tenant owner" meaning of `Admin` |
| `AccountingSystem.Api/Services/AuthService.cs` | Tenant-created users accept arbitrary `RoleName` and only reject `SuperAdmin` | `Admin` remains assignable inside tenants, which reinforces the overloaded naming |
| `AccountingSystem.Api/Services/IdentityAccountService.cs` | Syncs role names verbatim into Identity | No abstraction layer exists between business meaning and stored role name |
| `AccountingSystem.Api/Services/BusinessServices.cs` | Archived vendor/customer reads and restore operations can bypass tenant filters | Existing unsafe scope behavior is reachable by `Admin` because `Admin` can call those controllers |
| `AccountingSystem.Api/Services/LedgerService.cs` | Archived account reads and restore operations can bypass tenant filters | Existing unsafe scope behavior is reachable by `Admin` |
| `AccountingSystem.Api/Services/YearEndCloseService.cs` | Retained earnings lookup uses `IgnoreQueryFilters()` and account code only | `Admin`-only fiscal year close path has a cross-tenant lookup risk |

### 5.3 Middleware And Tenant Boundary

| File | Current `Admin` usage | Assessment |
| --- | --- | --- |
| `AccountingSystem.Api/Middleware/JwtMiddleware.cs` | Copies `"role"` into `HttpContext.Items["Role"]` | Raw string role propagation |
| `AccountingSystem.Api/Middleware/TenantAccessMiddleware.cs` | Only `SuperAdmin` is exempt; `Admin` is still tenant-blocked/suspended | Confirms `Admin` is intended to be tenant-scoped |
| `AccountingSystem.Api/Services/TenantService.cs` | Reads `CompanyId` claim for tenant context | `Admin` requests are normally bounded to the tenant claim |
| `AccountingSystem.Api/Data/AccountingDbContext.cs` | Global filters scope `User`, `Account`, `Vendor`, `Customer`, `Bill`, `Invoice`, `Payment`, `DocumentSequence`, `AuditLog`, `FiscalYearClose`, `JournalEntry`, `JournalEntryLine` by current tenant | `Admin` depends on tenant filters for isolation; bypasses become high-risk |

### 5.4 Client Navigation And UI Labels

| File | Current `Admin` usage | Assessment |
| --- | --- | --- |
| `AccountingSystem.Client/Layout/NavMenu.razor` | Separate `Administration` section shown only to `Admin` | Label reads like a platform admin area even though it is tenant-only |
| `AccountingSystem.Client/Pages/Admin/UserManagement.razor` | Admin-only page title, role badge styling, and role picker includes `Admin` | Reinforces that `Admin` is the tenant owner/admin role |
| `AccountingSystem.Client/Pages/Admin/AuditLogs.razor` | Admin-only tenant audit log page | Easy to confuse with platform audit features |
| `AccountingSystem.Client/Pages/Admin/CompanySettings.razor` | Admin-only tenant company settings page | Another tenant-admin meaning |
| `AccountingSystem.Client/Pages/Auth/RegisterCompany.razor` | Labels `Administrator Full Name` and `AdminEmail`-backed registration fields | Most user-facing place where tenant owner creation is named "Administrator" |
| `AccountingSystem.Client/Pages/SuperAdmin/GlobalUserManager.razor` | Role filter still displays `Admin` among tenant roles | Super-admin tooling exposes the ambiguous label globally |

### 5.5 DTOs And Contracts

| File | Current `Admin` usage | Assessment |
| --- | --- | --- |
| `AccountingSystem.Shared/DTOs/AuthDTOs.cs` | `CompanyRegisterDTO.AdminEmail`, `CompanyRegisterDTO.AdminFullName` | DTO contract encodes the old tenant-owner name |
| `AccountingSystem.Shared/DTOs/AuthDTOs.cs` | `RegisterDTO.RoleName`, `AuthResponseDTO.Role`, `CurrentProfileDTO.Role` | Role contract is raw string-based |
| `AccountingSystem.Shared/DTOs/UserDTO.cs` | `RoleName` is string | No scope metadata exists |
| `AccountingSystem.Shared/DTOs/SuperAdminDTOs.cs` | `GlobalUserDTO.Role` and `SuperAdminAuditLogDTO.AdminEmail` | Platform pages surface raw tenant role strings and use "Admin" terminology in audit models |

### 5.6 Policies And Authorization Model

- No named RBAC policies were found.
- No role constants or role enum were found.
- API authorization is mostly string-literal role lists in attributes.
- Client authorization is mostly string-literal role lists in Razor.
- This makes future renaming high-touch and error-prone.

## 6. Ambiguous And Unsafe Current Usages

### 6.1 Ambiguous / overloaded `Admin`

1. Self-registration uses `Admin` for the first tenant user.
   - `AuthService.RegisterCompanyAsync()` looks up the legacy `Admin` role, creates the first tenant user with that role, provisions Identity with that role, and returns `Role = "Admin"`.
   - This is effectively the current tenant owner flow.

2. Tenant administration surfaces use the same `Admin` string.
   - Tenant user management, tenant audit log access, company settings, document numbering, and year-end close all require `Admin`.
   - The name reads like a platform-wide administrator, but the features are tenant-local.

3. Business operation surfaces also include `Admin`.
   - `Admin,Accounting` and `Admin,Accounting,Management` appear across AP, AR, GL, payments, and shared UI components.
   - This makes `Admin` mean both "tenant owner/admin" and "general business operator".

4. DTOs and labels still encode `Admin`.
   - `AdminEmail`, `AdminFullName`, `Administration`, `Administrator Full Name`, and role pickers with `Admin` all preserve the old terminology.

5. Tenant user deletion logic treats `Admin` as a special sentinel.
   - `UsersController.DeleteUser()` blocks archiving any user whose role name is `Admin`.
   - This is another sign that `Admin` currently stands in for tenant owner / protected tenant admin.

### 6.2 Unsafe scope-related findings already present

| Area | Current implementation | Why it matters |
| --- | --- | --- |
| `UsersController.GetAllUsers(includeArchived = true)` | Calls `IgnoreQueryFilters()` on `User` without re-applying `CompanyId` | Archived user listing can bypass tenant isolation |
| `UsersController.RestoreUser()` | Uses `IgnoreQueryFilters()` and loads by `id` only | Restore path can cross tenant boundaries if an ID is known |
| `PayableService.GetVendorsAsync(includeArchived = true)` | Uses `IgnoreQueryFilters()` on `Vendor` | Archived vendor listing can cross tenants |
| `PayableService.RestoreVendorAsync()` | Uses `IgnoreQueryFilters()` and loads by `id` only | Restore path can cross tenants |
| `ReceivableService.GetCustomersAsync(includeArchived = true)` | Uses `IgnoreQueryFilters()` on `Customer` | Archived customer listing can cross tenants |
| `ReceivableService.RestoreCustomerAsync()` | Uses `IgnoreQueryFilters()` and loads by `id` only | Restore path can cross tenants |
| `LedgerService.GetChartOfAccountsAsync(includeArchived = true)` | Uses `IgnoreQueryFilters()` on `Account` | Archived account listing can cross tenants |
| `LedgerService.RestoreAccountAsync()` | Uses `IgnoreQueryFilters()` and loads by `id` only | Restore path can cross tenants |
| `YearEndCloseService.EnsureRetainedEarningsAccountAsync()` | Uses `IgnoreQueryFilters()` and loads retained earnings account by code `3100` only | Admin-only year-end close can bind to the wrong tenant account |

These are existing scope-boundary issues, not Phase 1 changes.

## 7. Current User-Management Flows

### 7.1 Self-registration

Flow:

1. Client submits `CompanyRegisterDTO` from `AccountingSystem.Client/Pages/Auth/RegisterCompany.razor`.
2. API receives `POST /api/auth/register-company` in `AccountingSystem.Api/Controllers/AuthController.cs`.
3. `AuthService.RegisterCompanyAsync()`:
   - validates captcha
   - creates a new tenant `Company`
   - loads the legacy `Admin` role
   - creates the first legacy `User` for that tenant with role `Admin`
   - provisions the linked Identity user with role `Admin`
   - seeds tenant company data
   - sends email confirmation
   - returns `AuthResponseDTO` with `Role = "Admin"` and `RequiresEmailConfirmation = true`

Current role meaning:

- This is the tenant owner bootstrap flow, but it is named `Admin`.

### 7.2 Tenant-created users

Flow:

1. Tenant admin opens `AccountingSystem.Client/Pages/Admin/UserManagement.razor`.
2. Client posts `RegisterDTO` to `POST /api/users`.
3. `UsersController.CreateUser()` calls `AuthService.RegisterAsync()`.
4. `AuthService.RegisterAsync()`:
   - takes raw `RoleName`
   - resolves the legacy `Role` by name
   - rejects only `SuperAdmin`
   - creates the legacy `User`
   - provisions the Identity user with the same role name
   - sends confirmation email

Current role meaning:

- Tenant admins can assign `Admin`, `Accounting`, or `Management`.
- The current system lets a tenant admin create another tenant `Admin`.

### 7.3 Seeded `SuperAdmin`

Flow:

1. Startup in `AccountingSystem.Api/Program.cs` migrates `AccountingDbContext` and `IdentityAuthDbContext`.
2. Startup calls `DataSeeder.SeedDataAsync(...)`.
3. `DataSeeder`:
   - ensures host company `SaaS Operations` exists
   - ensures legacy `SuperAdmin` role exists
   - ensures the first platform `SuperAdmin` user exists in the legacy store
   - provisions the linked Identity user if needed

Current role meaning:

- This is the only platform bootstrap flow.
- `AuthService` treats `SuperAdmin` differently from tenant roles for email-confirmation login enforcement.

## 8. Scope Boundary Analysis

### 8.1 Why `SuperAdmin` is platform-scoped today

- `SuperAdminController` is the only dedicated cross-tenant controller surface.
- `SuperAdminController` explicitly uses `IgnoreQueryFilters()` for companies, users, and audit views.
- `TenantAccessMiddleware` exempts `SuperAdmin` from blocked/suspended company enforcement.
- `AuthService.ValidateLoginEligibilityAsync()` exempts `SuperAdmin` from the normal email-confirmation login gate.
- Client navigation has a separate `Super Admin` section and separate `/superadmin/*` pages.

### 8.2 Why `Admin`, `Accounting`, and `Management` are tenant-scoped today

- Tokens include `CompanyId`.
- `TenantService.GetCurrentTenant()` reads `CompanyId` from the authenticated user.
- `AccountingDbContext` applies tenant query filters to most tenant-owned entities.
- Tenant controllers normally operate against filtered sets or "current tenant" lookups.
- `TenantAccessMiddleware` blocks non-`SuperAdmin` users when the company is suspended/blocked.

### 8.3 Important nuance

- The intended model is tenant-scoped for `Admin`, `Accounting`, and `Management`.
- The current code does not always preserve that isolation once `IgnoreQueryFilters()` is used.
- Any future RBAC rename must preserve tenant isolation and should address the existing bypass paths separately and explicitly.

## 9. Current `Admin` Assumptions

| Assumption | Current answer | Evidence |
| --- | --- | --- |
| `Admin` can view logs | Yes, tenant audit logs | `AccountingSystem.Api/Controllers/AuditLogsController.cs`, `AccountingSystem.Client/Pages/Admin/AuditLogs.razor` |
| `Admin` can create users | Yes | `AccountingSystem.Api/Controllers/UsersController.cs`, `AccountingSystem.Api/Services/AuthService.cs`, `AccountingSystem.Client/Pages/Admin/UserManagement.razor` |
| `Admin` can assign roles | Yes, any non-`SuperAdmin` role | `AuthService.RegisterAsync()` blocks only `SuperAdmin`; client role picker offers `Admin`, `Accounting`, `Management` |
| `Admin` can access cross-tenant resources | By intended design, no. By current implementation, partially yes through unsafe archived/restore and retained-earnings paths | `UsersController`, `PayableService`, `ReceivableService`, `LedgerService`, `YearEndCloseService` |
| `Admin` is a platform-wide administrator | No in intended behavior, but the name is ambiguous enough to suggest it | Platform surfaces are restricted to `SuperAdmin`; tenant-admin flows still use the `Admin` name |

## 10. Recommended Migration Sequence

This section is forward guidance only. No steps below are implemented in Phase 1.

1. Centralize role identifiers and scope terminology.
2. Introduce explicit platform-vs-tenant naming in code and assessment artifacts.
3. Replace tenant `Admin` references with `TenantOwner` semantics in incremental code, UI, and docs updates.
4. Update both role stores and existing user-role data in a coordinated data migration.
5. Update self-registration, tenant user-management flows, and role assignment UI to reflect tenant ownership explicitly.
6. Update tests and documentation that currently encode `Admin` as the tenant owner/admin role.
7. Address the existing `IgnoreQueryFilters()` scope-bypass paths as a separate hardening step if they are not handled by the role-migration phases.

## 11. Future Data Migration And Seed Impact

The following areas will need attention in later phases:

- Legacy role seed data:
  - `AccountingSystem.Api/Data/AccountingDbContext.cs`
- Identity role seed data:
  - `AccountingSystem.Api/Identity/IdentityAuthDbContext.cs`
- Existing legacy users whose `Role.Name == "Admin"`
- Existing Identity user-role memberships that currently point to `Admin`
- Self-registration logic that currently creates the first tenant user as `Admin`
- Tenant user creation flows that still allow `Admin` to be assigned as a tenant role
- DTOs and field names that still encode `Admin` terminology:
  - `CompanyRegisterDTO.AdminEmail`
  - `CompanyRegisterDTO.AdminFullName`
  - `SuperAdminAuditLogDTO.AdminEmail`
- Client labels, page titles, nav labels, and role filters that still display `Admin`
- Tests that assert raw role strings in tokens, auth responses, and seeded flows
- Existing docs and assessments that describe the current seeded role set and tenant-admin behavior

## 12. Appendix: Tests And Documentation That Encode Current RBAC

### 12.1 Tests

- `AccountingSystem.API.Tests/UnitTest1.cs`
  - JWT claim contract tests assert `"Admin"` is emitted as the role
  - registration tests assert self-registration produces `Admin`
  - profile and MFA tests assert current role strings remain unchanged
  - login tests assert `SuperAdmin` bypass behavior

There are no client tests in the current repository that materially redefine RBAC semantics.

### 12.2 Existing documents

- `DOCUMENTATION.md`
  - documents seeded roles and role-gated endpoints/pages
- `README.md`
  - describes "Administrator", "Accounting Staff", and "Management"
- `SECURITY_ASSESSMENT.md`
  - lists role-gated endpoints and currently treats `Admin` as a tenant-level administrative role
- `IDENTITY_INTRODUCTION_NOTES.md`
  - states current role strings remain `Admin`, `Accounting`, `Management`, and `SuperAdmin`
- `IDENTITY_MIGRATION_PLAN.md`
  - documents current Identity seeding with the same role names
- `EMAIL_CONFIRMATION_ENFORCEMENT.md`
  - documents the `SuperAdmin` email-confirmation login exemption

## 13. Phase 1 Conclusion

- Current live role list: `SuperAdmin`, `Admin`, `Accounting`, `Management`
- Platform-scoped role today: `SuperAdmin`
- Tenant-scoped roles today by intent: `Admin`, `Accounting`, `Management`
- Most ambiguous role today: `Admin`
- Highest-risk current scope findings:
  - scattered string-literal role usage
  - tenant owner semantics hidden behind the `Admin` name
  - existing `IgnoreQueryFilters()` paths that can bypass tenant isolation
