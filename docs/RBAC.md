# RBAC & Access Control Documentation

> Role-Based Access Control model, authorization policies, and access control matrix for the AccSys accounting system.

---

## 1. Role Definitions

### 1.1 Role Inventory

| Role | Scope | Description |
|------|-------|-------------|
| **SuperAdmin** | Platform | Platform operator with cross-tenant access |
| **TenantOwner** | Tenant | Tenant owner/administrator with full tenant access |
| **Accounting** | Tenant | Accounting staff with transaction entry permissions |
| **Management** | Tenant | Read/report-oriented tenant role |

### 1.2 Role Details

#### SuperAdmin

- **Scope**: Platform-scoped (cross-tenant)
- **Purpose**: Platform operator over tenants and global users
- **Created via**: Bootstrap seeding at first API startup
- **Special privileges**:
  - Cross-tenant visibility via `IgnoreQueryFilters()`
  - Exempt from tenant suspension/blocking middleware
  - Exempt from email confirmation login requirement

#### TenantOwner

- **Scope**: Tenant-scoped
- **Purpose**: Tenant owner with full tenant administrative authority
- **Created via**: Self-registration (`/register`) or superadmin creation
- **Privileges**:
  - User management (invite, archive, restore)
  - Audit log access
  - Company settings
  - Document numbering configuration
  - Fiscal year close
  - All accounting and operational features

#### Accounting

- **Scope**: Tenant-scoped
- **Purpose**: Accounting staff with transaction permissions
- **Created via**: Tenant user invitation
- **Privileges**:
  - General Ledger operations (accounts, journal entries)
  - Accounts Payable operations (vendors, bills, payments)
  - Accounts Receivable operations (customers, invoices, receipts)
  - Reporting access

#### Management

- **Scope**: Tenant-scoped
- **Purpose**: Read/report-oriented business role
- **Created via**: Tenant user invitation
- **Privileges**:
  - Dashboard access
  - Financial reporting
  - Chart of accounts (read)
  - Trial balance (read)
  - Customer information (read)

---

## 2. Scope Model

### 2.1 Platform vs Tenant Scope

| Scope | Roles | Data Access | Middleware Behavior |
|-------|-------|-------------|---------------------|
| **Platform** | SuperAdmin | Cross-tenant via `IgnoreQueryFilters()` | Exempt from tenant blocking |
| **Tenant** | TenantOwner, Accounting, Management | Filtered by `CompanyId` | Subject to tenant status |

### 2.2 Tenant Isolation

Tenant-scoped authorization requires:
1. An allowed tenant role (TenantOwner, Accounting, or Management)
2. A valid positive `CompanyId` claim in the JWT

**Query filter application:**
- `AccountingDbContext` applies tenant filters to all tenant entities
- `CompanyId` extracted from authenticated user claims
- `SaveChangesAsync` auto-assigns `CompanyId` to new entities

### 2.3 SuperAdmin Exclusion

SuperAdmin is intentionally excluded from:
- Tenant business navigation
- Tenant operational pages (dashboard, GL, AP, AR)
- Tenant company settings

No tenant impersonation or cross-scope bypass exists in the current implementation.

---

## 3. Authorization Policies

### 3.1 Shared Policy Definitions

Policies defined in `AccountingSystem.Shared/Security/ApplicationAuthorizationPolicies.cs`:

| Policy | Requirements |
|--------|--------------|
| `RequireSuperAdmin` | Authenticated user with `SuperAdmin` role |
| `RequireTenantAccess` | Authenticated with TenantOwner/Accounting/Management + valid CompanyId |
| `RequireTenantOwner` | Authenticated `TenantOwner` + valid CompanyId |
| `RequireTenantAccountingAccess` | Authenticated TenantOwner/Accounting + valid CompanyId |
| `RequireTenantOperationalAccess` | Authenticated TenantOwner/Accounting/Management + valid CompanyId |

### 3.2 Policy Registration

**API side:**
- `AccountingSystem.Api/Configuration/ApplicationAuthorizationExtensions.cs`
- `AccountingSystem.Api/Program.cs`

**Client side:**
- `AccountingSystem.Client/Program.cs`

---

## 4. Access Control Matrix

### 4.1 Platform Administration

| Feature | SuperAdmin | TenantOwner | Accounting | Management |
|---------|:----------:|:-----------:|:----------:|:----------:|
| System Dashboard | ✅ | ❌ | ❌ | ❌ |
| Tenant Manager | ✅ | ❌ | ❌ | ❌ |
| Global User Manager | ✅ | ❌ | ❌ | ❌ |
| Platform Audit Logs | ✅ | ❌ | ❌ | ❌ |
| Platform Security Events | ✅ | ❌ | ❌ | ❌ |

### 4.2 Tenant Administration

| Feature | SuperAdmin | TenantOwner | Accounting | Management |
|---------|:----------:|:-----------:|:----------:|:----------:|
| User Management | ❌ | ✅ | ❌ | ❌ |
| Tenant Audit Logs | ❌ | ✅ | ❌ | ❌ |
| Company Settings | ❌ | ✅ | ❌ | ❌ |
| Document Numbering | ❌ | ✅ | ❌ | ❌ |
| Fiscal Year Close | ❌ | ✅ | ❌ | ❌ |

### 4.3 Business Operations

| Feature | SuperAdmin | TenantOwner | Accounting | Management |
|---------|:----------:|:-----------:|:----------:|:----------:|
| Dashboard | ❌ | ✅ | ✅ | ✅ |
| Financial Reports | ❌ | ✅ | ✅ | ✅ |
| Trial Balance | ❌ | ✅ | ✅ | ✅ |
| Chart of Accounts (view) | ❌ | ✅ | ✅ | ✅ |
| Chart of Accounts (edit) | ❌ | ✅ | ✅ | ❌ |
| Journal Entries | ❌ | ✅ | ✅ | ❌ |
| Vendors | ❌ | ✅ | ✅ | ❌ |
| Bills | ❌ | ✅ | ✅ | ❌ |
| Customers (view) | ❌ | ✅ | ✅ | ✅ |
| Customers (edit) | ❌ | ✅ | ✅ | ❌ |
| Invoices | ❌ | ✅ | ✅ | ❌ |
| Payments | ❌ | ✅ | ✅ | ❌ |

### 4.4 Self-Service

| Feature | SuperAdmin | TenantOwner | Accounting | Management |
|---------|:----------:|:-----------:|:----------:|:----------:|
| View Profile | ✅ | ✅ | ✅ | ✅ |
| Update Profile | ✅ | ✅ | ✅ | ✅ |
| Change Password | ✅ | ✅ | ✅ | ✅ |
| MFA Setup | ✅ | ✅ | ✅ | ✅ |

---

## 5. Role Assignment Rules

### 5.1 Assignment Matrix

| Actor | Can Assign |
|-------|------------|
| SuperAdmin | SuperAdmin, TenantOwner, Accounting, Management |
| TenantOwner | Accounting, Management |
| Accounting | None |
| Management | None |

### 5.2 Prohibited Assignments

- TenantOwner **cannot** assign SuperAdmin
- TenantOwner **cannot** assign another TenantOwner
- Accounting **cannot** assign any role
- Management **cannot** assign any role
- Tenant-scoped user management **cannot** operate on SuperAdmin accounts
- Tenant-scoped user management **cannot** archive/restore/resend-invite TenantOwner accounts

### 5.3 Validated Assignment Surfaces

| Endpoint | Validation |
|----------|------------|
| `POST /api/users` | Actor role and tenant scope checked server-side |
| `POST /api/users/{id}/resend-invite` | Blocked for SuperAdmin and TenantOwner targets |
| `DELETE /api/users/{id}` | Blocked for SuperAdmin and TenantOwner targets |
| `PUT /api/users/{id}/restore` | Blocked for SuperAdmin and TenantOwner targets |

---

## 6. API Authorization

### 6.1 Platform Administration

| Controller | Policy |
|------------|--------|
| `SuperAdminController` | `RequireSuperAdmin` |

### 6.2 Tenant Administration

| Controller/Action | Policy |
|-------------------|--------|
| `UsersController` | `RequireTenantOwner` |
| `AuditLogsController` | `RequireTenantOwner` |
| `DocumentNumberingController` | `RequireTenantOwner` |
| `CompaniesController` (update) | `RequireTenantOwner` |
| `GeneralLedgerController` (fiscal close) | `RequireTenantOwner` |

### 6.3 Tenant Accounting

| Controller/Action | Policy |
|-------------------|--------|
| `BusinessControllers` (AP/AR) | `RequireTenantAccountingAccess` |
| `PaymentController` | `RequireTenantAccountingAccess` |
| `GeneralLedgerController` (create/update/delete) | `RequireTenantAccountingAccess` |

### 6.4 Tenant Operational

| Controller/Action | Policy |
|-------------------|--------|
| `GeneralLedgerController` (chart, trial balance, fiscal years) | `RequireTenantOperationalAccess` |
| `ReportsController` | `RequireTenantOperationalAccess` |
| `CompaniesController` (read) | `RequireTenantAccess` |

---

## 7. Client Authorization

### 7.1 Navigation Visibility

**NavMenu.razor sections:**

| Section | Visible To |
|---------|------------|
| Super Admin | `SuperAdmin` only |
| Tenant Administration | `TenantOwner` only |
| Dashboard/Reporting | `TenantOwner`, `Accounting`, `Management` |
| GL/AP/AR | `TenantOwner`, `Accounting` |

### 7.2 Platform Pages

| Page | Policy |
|------|--------|
| `/superadmin/dashboard` | `RequireSuperAdmin` |
| `/superadmin/tenants` | `RequireSuperAdmin` |
| `/superadmin/users` | `RequireSuperAdmin` |
| `/superadmin/audit-logs` | `RequireSuperAdmin` |

### 7.3 Tenant Admin Pages

| Page | Policy |
|------|--------|
| `/admin/users` | `RequireTenantOwner` |
| `/admin/audit-logs` | `RequireTenantOwner` |
| `/admin/company-settings` | `RequireTenantOwner` |

### 7.4 Tenant Business Pages

| Page | Policy |
|------|--------|
| `/dashboard` | `RequireTenantOperationalAccess` |
| `/reports/*` | `RequireTenantOperationalAccess` |
| `/gl/accounts` | `RequireTenantOperationalAccess` (view), `RequireTenantAccountingAccess` (edit) |
| `/gl/journal` | `RequireTenantAccountingAccess` |
| `/ap/*` | `RequireTenantAccountingAccess` |
| `/ar/*` | `RequireTenantAccountingAccess` |

---

## 8. Audit Log Visibility

### 8.1 Platform Admin View

**SuperAdmin can access:**
- `/api/superadmin/audit-logs` - Platform admin action feed
- `/api/superadmin/security-events` - AUTH-* events across platform

**Hidden from superadmin:**
- `AUTH-EMAIL-CONFIRMATION-BYPASS` events

### 8.2 Tenant Admin View

**TenantOwner can access:**
- `/api/audit-logs` - Tenant-filtered activity

**Excluded from tenant logs:**
- `SUPERADMIN-*` actions
- `AUTH-*` rows tied to SuperAdmin
- `AUTH-*` rows marked with `reason = "SuperAdminRole"`
- `AUTH-*` rows sourced from `/api/superadmin/*`

### 8.3 Business Roles

**Accounting and Management cannot access:**
- `/api/audit-logs`
- `/api/superadmin/audit-logs`
- `/api/superadmin/security-events`

---

## 9. Middleware Authorization

### 9.1 Tenant Access Middleware

Evaluates scope from `HttpContext.User`:
- SuperAdmin bypass is explicit
- Tenant-scoped principals without valid `CompanyId` → `403`
- Blocked users → `403`
- Blocked/suspended tenants → `403`

### 9.2 Tenant Service

Reads tenant context through shared scope evaluator:
- Extracts `CompanyId` from authenticated user
- Returns null for SuperAdmin (platform scope)

---

## 10. Capability Classification

| Capability | Classification | Role Scope |
|------------|----------------|------------|
| System dashboard, tenant manager, global users | Platform admin | SuperAdmin |
| Platform admin audit logs | Platform admin | SuperAdmin |
| Platform security events (AUTH-*) | Platform admin | SuperAdmin |
| Tenant users, tenant audit logs, company settings | Tenant admin | TenantOwner |
| Document numbering, fiscal year close | Tenant admin | TenantOwner |
| Dashboard, financial statements | Business feature | TenantOwner, Accounting, Management |
| GL/AP/AR operational actions | Business feature | TenantOwner, Accounting |

---

## 11. User Creation Flows

### 11.1 Self-Registration

1. User submits `CompanyRegisterDTO` at `/register`
2. API creates new tenant company
3. API creates first user with `TenantOwner` role
4. Email confirmation required before login

### 11.2 Tenant User Invitation

1. TenantOwner opens `/admin/users`
2. Creates invitation with email, role, optional name
3. API creates user with `Status = "Invited"`, `IsActive = false`
4. Invitation email sent with confirmation link
5. User confirms email, sets password
6. Account activated when both complete

### 11.3 Seeded SuperAdmin

1. Startup migrates databases
2. `DataSeeder.SeedDataAsync` runs
3. Ensures host company exists
4. Creates first SuperAdmin from `BootstrapAdmin:*` config

---

## 12. Migration Notes

### 12.1 Role Rename History

The system underwent a role rename:
- **Old**: `Admin` (ambiguous tenant owner)
- **New**: `TenantOwner` (explicit tenant scope)

Migration handled by:
- `20260330040418_RenameAdminRoleToTenantOwner` (legacy store)
- `20260330040418_RenameIdentityAdminRoleToTenantOwner` (Identity store)

### 12.2 Runtime Behavior

- Runtime and stored values use `TenantOwner`
- UI display shows "Tenant Owner"
- SuperAdmin remains unchanged
- JWT `role` claim returns `TenantOwner`

---

## 13. Verification Checklist

### Platform Role (SuperAdmin)

- [ ] Can access `/superadmin/dashboard`
- [ ] Can access tenant manager
- [ ] Can access global users
- [ ] Can access platform audit logs
- [ ] Cannot see tenant business navigation
- [ ] Cannot access tenant GL/AP/AR

### Tenant Owner (TenantOwner)

- [ ] Can access user management
- [ ] Can access tenant audit logs
- [ ] Can access company settings
- [ ] Can access document numbering
- [ ] Can close fiscal year
- [ ] Can access all business features
- [ ] Cannot access platform admin pages
- [ ] Cannot see other tenants' data

### Accounting

- [ ] Can access GL/AP/AR
- [ ] Can create/edit transactions
- [ ] Can view reports
- [ ] Cannot access user management
- [ ] Cannot access audit logs
- [ ] Cannot access platform pages

### Management

- [ ] Can access dashboard
- [ ] Can access financial reports
- [ ] Can view chart of accounts
- [ ] Cannot create/edit transactions
- [ ] Cannot access user management
- [ ] Cannot access audit logs
