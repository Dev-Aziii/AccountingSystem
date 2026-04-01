# API Documentation

> Complete reference for the AccSys REST API endpoints, architecture, and data contracts.

---

## 1. API Architecture

### 1.1 Overview

The API is built with ASP.NET Core 8 Web API and follows a layered architecture:

- **Controllers**: REST endpoints with role-gated access
- **Services**: Business logic (Auth, Ledger, Payables, Receivables, Payments, PDF, Tenant)
- **Persistence**: Entity Framework Core via `AccountingDbContext` and `IdentityAuthDbContext`
- **Middleware**: JWT parsing, tenant access enforcement, audit logging

### 1.2 Program.cs Configuration

Key configurations:

- SQL Server DbContext with `DefaultConnection`
- Scoped service registrations for domain services
- JWT Bearer authentication and validation
- CORS policy `AllowBlazorClient`
- Swagger with Bearer security definition
- Startup migration + seed execution

### 1.3 Middleware Pipeline

Order of middleware execution:

1. `UseAuthentication()` - JWT Bearer handler, populates `HttpContext.User`
2. `JwtMiddleware` - Validates JWT, stores metadata in `HttpContext.Items`
3. `TenantAccessMiddleware` - Blocks non-superadmin calls for blocked/suspended users/companies
4. `UseAuthorization()` - Enforces `[Authorize]` attributes
5. `AuditMiddleware` - Logs successful state-changing requests

### 1.4 Request Flow

1. Blazor page triggers domain service
2. Domain service uses `ApiService` to call `/api/...` endpoint
3. `ApiService` attaches Bearer token from local storage
4. API pipeline validates JWT and enriches `HttpContext.Items`
5. Tenant-access middleware blocks blocked/suspended access
6. Controller delegates to service layer / DbContext
7. EF Core query filters scope data by `CompanyId`
8. API returns DTO/JSON or file response

---

## 2. Authentication Endpoints

### Controller: AuthController

**Base Route**: `/api/auth`  
**Authorization**: Mixed (public + authenticated)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/auth/login` | Public | User login and JWT issuance |
| POST | `/api/auth/login/mfa` | Public | MFA second-step login |
| POST | `/api/auth/register-company` | Public | Create tenant + admin user |
| POST | `/api/auth/forgot-password` | Public | Request password reset email |
| POST | `/api/auth/reset-password` | Public | Reset password with token |
| POST | `/api/auth/confirm-email` | Public | Confirm email address |
| POST | `/api/auth/resend-confirmation` | Public | Resend confirmation email |
| GET | `/api/auth/profile` | Auth | Get current user profile |
| PUT | `/api/auth/profile` | Auth | Update current user profile |
| PUT | `/api/auth/password` | Auth | Change current user password |
| GET | `/api/auth/mfa` | Auth | Get MFA status |
| POST | `/api/auth/mfa/authenticator/setup` | Auth | Start MFA setup |
| POST | `/api/auth/mfa/authenticator/verify` | Auth | Verify and enable MFA |
| POST | `/api/auth/mfa/authenticator/reset` | Auth | Reset authenticator key |
| POST | `/api/auth/mfa/recovery-codes/regenerate` | Auth | Regenerate recovery codes |
| POST | `/api/auth/mfa/disable` | Auth | Disable MFA |

### Request/Response Examples

**Login Request:**
```json
{
  "email": "admin@company.com",
  "password": "your-password"
}
```

**Login Response (Success):**
```json
{
  "token": "eyJhbG...",
  "email": "admin@company.com",
  "fullName": "Admin User",
  "role": "TenantOwner",
  "companyId": 1,
  "companyName": "My Company",
  "requiresTwoFactor": false
}
```

**Login Response (MFA Required):**
```json
{
  "token": "",
  "requiresTwoFactor": true,
  "twoFactorChallengeToken": "challenge-token..."
}
```

---

## 3. Tenant User Management Endpoints

### Controller: UsersController

**Base Route**: `/api/users`  
**Authorization**: `TenantOwner`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users?includeArchived={bool}` | List tenant users |
| POST | `/api/users` | Invite new tenant user |
| POST | `/api/users/{id}/resend-invite` | Resend invitation email |
| DELETE | `/api/users/{id}` | Archive user (soft delete) |
| PUT | `/api/users/{id}/restore` | Restore archived user |

### Request Example

**Invite User:**
```json
{
  "email": "newuser@company.com",
  "roleName": "Accounting",
  "firstName": "John",
  "lastName": "Doe"
}
```

---

## 4. Company Endpoints

### Controller: CompaniesController

**Base Route**: `/api/companies`  
**Authorization**: Authenticated (update requires `TenantOwner`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/companies/current` | Auth | Get current tenant company |
| PUT | `/api/companies/current` | TenantOwner | Update tenant company |

---

## 5. General Ledger Endpoints

### Controller: GeneralLedgerController

**Base Route**: `/api/ledger`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/ledger/accounts?includeArchived={bool}` | TenantOperationalAccess | List chart of accounts |
| POST | `/api/ledger/accounts` | TenantAccountingAccess | Create account |
| PUT | `/api/ledger/accounts/{id}` | TenantAccountingAccess | Update account |
| DELETE | `/api/ledger/accounts/{id}` | TenantAccountingAccess | Archive account |
| PUT | `/api/ledger/accounts/{id}/restore` | TenantAccountingAccess | Restore account |
| GET | `/api/ledger/trial-balance` | TenantOperationalAccess | Get trial balance |
| POST | `/api/ledger/journal` | TenantAccountingAccess | Post journal entry |
| GET | `/api/ledger/fiscal-years` | TenantOperationalAccess | List fiscal years |
| POST | `/api/ledger/fiscal-years/close` | TenantOwner | Close fiscal year |

### Request Example

**Create Journal Entry:**
```json
{
  "description": "Office supplies purchase",
  "reference": "JV-2026-001",
  "date": "2026-02-01T00:00:00Z",
  "lines": [
    { "accountId": 15, "debit": 1000, "credit": 0 },
    { "accountId": 2, "debit": 0, "credit": 1000 }
  ]
}
```

---

## 6. Accounts Payable Endpoints

### Controller: AccountsPayableController

**Base Route**: `/api/payables`  
**Authorization**: `TenantAccountingAccess`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/payables/vendors?includeArchived={bool}` | List vendors |
| POST | `/api/payables/vendors` | Create vendor |
| PUT | `/api/payables/vendors/{id}` | Update vendor |
| DELETE | `/api/payables/vendors/{id}` | Archive vendor |
| PUT | `/api/payables/vendors/{id}/restore` | Restore vendor |
| GET | `/api/payables/bills` | List bills |
| POST | `/api/payables/bill` | Create bill |
| POST | `/api/payables/bill/{id}/pay` | Record payment |

---

## 7. Accounts Receivable Endpoints

### Controller: AccountsReceivableController

**Base Route**: `/api/receivables`  
**Authorization**: `TenantAccountingAccess`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/receivables/customers?includeArchived={bool}` | List customers |
| POST | `/api/receivables/customers` | Create customer |
| PUT | `/api/receivables/customers/{id}` | Update customer |
| DELETE | `/api/receivables/customers/{id}` | Archive customer |
| PUT | `/api/receivables/customers/{id}/restore` | Restore customer |
| GET | `/api/receivables/invoices` | List invoices |
| POST | `/api/receivables/invoice` | Create invoice |
| POST | `/api/receivables/invoice/{id}/receive` | Record payment |

### Request Example

**Record Receivable Payment:**
```json
{
  "referenceId": 12,
  "amount": 2500,
  "paymentDate": "2026-02-10T09:00:00Z",
  "paymentMethod": "Online",
  "referenceNumber": "PM-ABC123",
  "assetAccountId": 1,
  "remarks": "Partial payment"
}
```

---

## 8. Reporting Endpoints

### Controller: ReportsController

**Base Route**: `/api/reports`  
**Authorization**: `TenantOperationalAccess`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/reports/invoices/{id}/pdf` | Generate invoice PDF |
| GET | `/api/reports/financials/pdf` | Generate financial report PDF |

---

## 9. Payment Endpoints

### Controller: PaymentController

**Base Route**: `/api/payments`

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/payments/paymongo-source` | TenantAccountingAccess | Create PayMongo source |
| POST | `/api/payments/webhook` | AllowAnonymous | PayMongo webhook |

---

## 10. Audit Log Endpoints

### Controller: AuditLogsController

**Base Route**: `/api/audit-logs`  
**Authorization**: `TenantOwner`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/audit-logs` | Get tenant audit logs (latest 500) |

---

## 11. Document Numbering Endpoints

### Controller: DocumentNumberingController

**Base Route**: `/api/document-numbering`  
**Authorization**: `TenantOwner`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/document-numbering` | Get document sequences |
| PUT | `/api/document-numbering` | Update document sequences |

---

## 12. Super Admin Endpoints

### Controller: SuperAdminController

**Base Route**: `/api/superadmin`  
**Authorization**: `SuperAdmin`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/superadmin/dashboard` | Platform dashboard stats |
| GET | `/api/superadmin/companies` | List all tenants |
| PUT | `/api/superadmin/companies/{id}/status` | Update tenant status |
| PUT | `/api/superadmin/companies/{id}/toggle` | Toggle tenant active |
| GET | `/api/superadmin/users` | List all platform users |
| PUT | `/api/superadmin/users/{id}/status` | Update user status |
| PUT | `/api/superadmin/users/{id}/toggle` | Toggle user active |
| GET | `/api/superadmin/audit-logs` | Platform admin audit logs |
| GET | `/api/superadmin/security-events` | Platform security events |

---

## 13. DTO Reference

### 13.1 Authentication DTOs

| DTO | Purpose |
|-----|---------|
| `LoginDTO` | Login request (email, password) |
| `AuthResponseDTO` | Login response (token, user info, MFA status) |
| `CompanyRegisterDTO` | Self-registration request |
| `UpdateProfileDTO` | Profile update request |
| `ChangePasswordDTO` | Password change request |
| `ForgotPasswordDTO` | Password reset request |
| `ResetPasswordDTO` | Password reset with token |
| `CurrentProfileDTO` | Current user profile response |

### 13.2 User DTOs

| DTO | Purpose |
|-----|---------|
| `UserDTO` | Tenant user response |
| `InviteTenantUserDTO` | Tenant user invitation request |
| `GlobalUserDTO` | Platform-wide user (superadmin view) |

### 13.3 Company DTOs

| DTO | Purpose |
|-----|---------|
| `CompanyDTO` | Company information |
| `UpdateCompanyDTO` | Company update request |

### 13.4 Ledger DTOs

| DTO | Purpose |
|-----|---------|
| `AccountDTO` | Chart of accounts entry |
| `CreateAccountDTO` | Create account request |
| `UpdateAccountDTO` | Update account request |
| `JournalEntryDTO` | Journal entry with lines |
| `JournalEntryLineDTO` | Journal entry line |
| `TrialBalanceDTO` | Trial balance response |
| `AccountBalanceDTO` | Account balance item |

### 13.5 AP/AR DTOs

| DTO | Purpose |
|-----|---------|
| `VendorDTO` | Vendor information |
| `CreateVendorDTO` | Create vendor request |
| `UpdateVendorDTO` | Update vendor request |
| `BillDTO` | Bill information |
| `CreateBillDTO` | Create bill request |
| `CustomerDTO` | Customer information |
| `CreateCustomerDTO` | Create customer request |
| `UpdateCustomerDTO` | Update customer request |
| `InvoiceDTO` | Invoice information |
| `CreateInvoiceDTO` | Create invoice request |

### 13.6 Payment DTOs

| DTO | Purpose |
|-----|---------|
| `RecordPaymentDTO` | Record payment request |
| `PaymentHistoryDTO` | Payment history entry |
| `CreateSourceDTO` | PayMongo source request |
| `PaymentSourceResponseDTO` | PayMongo source response |

### 13.7 Audit DTOs

| DTO | Purpose |
|-----|---------|
| `AuditLogDTO` | Tenant audit log entry |
| `SuperAdminAuditLogDTO` | Platform audit log entry |
| `SecurityEventDTO` | Security event entry |

### 13.8 SuperAdmin DTOs

| DTO | Purpose |
|-----|---------|
| `SystemDashboardDTO` | Platform stats |
| `TenantDTO` | Tenant information |
| `UpdateCompanyStatusDTO` | Tenant status update |

---

## 14. Entity Relationships

### 14.1 DTO-Entity Mapping

| DTO | Entity |
|-----|--------|
| `AccountDTO` | `Account` |
| `VendorDTO` | `Vendor` |
| `CustomerDTO` | `Customer` |
| `BillDTO` | `Bill` |
| `InvoiceDTO` | `Invoice` |
| `CompanyDTO` | `Company` |
| `UserDTO` | `User` + `Role.Name` |
| `AuditLogDTO` | `AuditLog` + user email join |
| `JournalEntryDTO` | `JournalEntry` + `JournalEntryLine` |

### 14.2 Mapping Strategy

Mapping is done manually inside controllers/services using LINQ projections and object initializers. No AutoMapper is used.

---

## 15. Validation

### 15.1 DTO Validation

Common validation attributes used:
- `[Required]`
- `[EmailAddress]`
- `[StringLength]`
- `[MinLength]`
- `[MaxLength]`
- `[Compare]`

### 15.2 Business Validation

Additional validation enforced in services:
- Balanced journal entries (debits = credits)
- Overpayment prevention
- Duplicate email checks
- Role assignment restrictions

---

## 16. Serialization

### 16.1 Enum Handling

Enums use `JsonStringEnumConverter`:
- `DocumentStatus`
- `PaymentMethod`
- `PaymentType`

### 16.2 External API Models

External API models use `[JsonPropertyName]` for provider payloads:
- PayMongo request/response models
- World Bank API models
- Frankfurter API models

### 16.3 Sensitive Fields

Sensitive fields are hidden with `[JsonIgnore]`:
- `PasswordHash`
- `PasswordSalt`

---

## 17. Error Responses

### 17.1 Standard Error Codes

| Code | Meaning |
|------|---------|
| 400 | Bad Request - Validation errors |
| 401 | Unauthorized - Authentication required |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error |

### 17.2 Rate Limit Headers

When rate limited, responses include:
- `Retry-After` header with seconds until retry allowed
