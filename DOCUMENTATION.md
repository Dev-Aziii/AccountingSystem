# PROJECT_DOCUMENTATION — AccountingSystem

## 1. System Architecture Overview

### 1.1 Overall architecture pattern

AccountingSystem follows a **3-project layered architecture**:

- **AccountingSystem.Client**: Blazor WebAssembly SPA (presentation/UI layer).
- **AccountingSystem.Api**: ASP.NET Core Web API (application/service layer).
- **AccountingSystem.Shared**: shared contracts (DTOs, enums) consumed by both client and API.

It is effectively a **client-server architecture** with contract-sharing through a common library.

### 1.2 Responsibility of each project

- **AccountingSystem.Client**
  - UI pages/components, route protection, role-aware navigation.
  - Client-side auth state and JWT token storage.
  - Calls API endpoints through `ApiService` and domain services.
- **AccountingSystem.Api**
  - Authentication, authorization, tenant isolation, business workflows (GL/AP/AR/Reports/SuperAdmin).
  - EF Core persistence and migrations.
  - Cross-cutting middleware: JWT parsing, tenant access enforcement, audit logging.
- **AccountingSystem.Shared**
  - DTOs used in request/response payloads.
  - Enums and JSON serialization settings for contract consistency.

### 1.3 Dependency direction

- `AccountingSystem.Client -> AccountingSystem.Shared`
- `AccountingSystem.Api -> AccountingSystem.Shared`
- `AccountingSystem.Api` and `AccountingSystem.Client` do **not** directly reference each other.

### 1.4 Request flow (Client → API → Database)

1. Blazor page triggers a domain service (e.g., `ReceivableService`).
2. Domain service uses `ApiService` to call `/api/...` endpoint.
3. `ApiService` attaches Bearer token from local storage.
4. API pipeline validates JWT and enriches `HttpContext.Items` (`UserId`, `Role`, `CompanyId`).
5. Tenant-access middleware blocks blocked/suspended users/companies.
6. Controller delegates to service layer / DbContext.
7. EF Core query filters scope tenant data by `CompanyId` (except explicit `IgnoreQueryFilters()`).
8. API returns DTO/json or file response to client.

### 1.5 Authentication mechanism

- **JWT Bearer authentication** configured in API.
- Claims include: `UserId`, `role`, `CompanyId`, `CompanyName`, plus standard name/role claims.
- Client stores token in local storage (`authToken`) and rebuilds auth state from token claims.
- Route authorization is applied both:
  - server-side via `[Authorize]`/`[Authorize(Roles=...)]`
  - client-side via `AuthorizeRouteView`, page-level `[Authorize]`, and `AuthorizeView`.

### 1.6 State management strategy (Blazor)

- Authentication state: custom `AuthenticationStateProvider` (`CustomAuthStateProvider`).
- Session/token state: `TokenStorageService` + local storage.
- Feature data state: page-local component state (`List<T>`, filters, dialogs, loading flags) fetched via scoped services.
- No centralized Flux/Redux-style state store; state is primarily per-page and service-driven.

---

## 2. Backend Documentation

### 2.1 API composition

- **Controllers** expose REST endpoints and handle role-gated access.
- **Services** contain business logic (Auth, Ledger, Payables, Receivables, Payments, PDF, Tenant).
- **Persistence** is EF Core via `AccountingDbContext`.
- **Middleware** adds JWT claim extraction, tenant status enforcement, and audit logging.
- **Migrations** are present and actively used (`Database.Migrate()` at startup).

### 2.2 Program.cs and DI setup

- Registers SQL Server DbContext with `DefaultConnection`.
- Registers scoped services/interfaces:
  - `IAuthService`, `ILedgerService`, `IPayableService`, `IReceivableService`, `IPaymentService`, `IPdfService`, `ITenantService`.
- Registers captcha service using `HttpClient`.
- Configures JWT bearer validation (issuer, audience, symmetric signing key, zero clock skew).
- Configures CORS policy `AllowBlazorClient` for local client origins.
- Enables Swagger with Bearer security definition.
- Startup migration + seed execution via `DataSeeder.SeedDataAsync(context)`.
- Middleware order:
  - `UseAuthentication()`
  - `JwtMiddleware`
  - `TenantAccessMiddleware`
  - `UseAuthorization()`
  - `AuditMiddleware`

### 2.3 Middleware

- **JwtMiddleware**: parses bearer token and stores user metadata in `HttpContext.Items`.
- **TenantAccessMiddleware**: blocks non-superadmin calls when user/company status is blocked/suspended.
- **AuditMiddleware**: logs successful state-changing requests (POST/PUT/DELETE) into `AuditLogs` with action naming conventions.

### 2.4 Filters

- No custom ASP.NET MVC filter classes are present.
- Cross-cutting behavior is implemented primarily through middleware and EF query filters.

### 2.5 Authentication/authorization configuration

- Authentication: JWT Bearer.
- Authorization model: role-based attributes (`Admin`, `Accounting`, `Management`, `SuperAdmin`) plus authenticated-only routes.

### 2.6 DbContext

- `DbSet`s: `Companies`, `Users`, `Roles`, `Accounts`, `JournalEntries`, `JournalEntryLines`, `Vendors`, `Customers`, `Bills`, `Invoices`, `Payments`, `AuditLogs`, `SuperAdminAuditLogs`.
- Global query filters enforce tenant isolation and soft-delete visibility.
- Enum-to-string conversion for `DocumentStatus`, `PaymentMethod`, `PaymentType`.
- Decimal precision standardization (`18,2`).
- Constraints:
  - unique `User.Email`
  - unique `Account(Code, CompanyId)`
- Role seed data includes `SuperAdmin` role.
- `SaveChangesAsync` auto-manages timestamps, soft-delete behavior, and tenant assignment for `BaseEntity`.

### 2.7 Controller endpoint catalog

### Controller: AuthController

Base Route: `/api/auth`  
Authorization Required: Mixed (public + authenticated)

Endpoints:

- **[POST] /api/auth/login**
  - Description: User login and JWT issuance.
  - Request: `LoginDTO`.
  - Response: `AuthResponseDTO`.
  - Status Codes: `200`, `401`.
- **[POST] /api/auth/register-company**
  - Description: Creates tenant company + admin user; returns JWT.
  - Request: `CompanyRegisterDTO` (includes recaptcha token).
  - Response: `AuthResponseDTO`.
  - Status Codes: `200`, `400`.
- **[PUT] /api/auth/profile**
  - Description: Updates current user profile.
  - Request: `UpdateProfileDTO`.
  - Response: message object.
  - Status Codes: `200`, `400`, `401`.
- **[PUT] /api/auth/password**
  - Description: Changes current user password.
  - Request: `ChangePasswordDTO`.
  - Response: message object.
  - Status Codes: `200`, `400`, `401`.

### Controller: UsersController

Base Route: `/api/users`  
Authorization Required: Yes (`Admin`)

Endpoints:

- **[GET] /api/users?includeArchived={bool}**
- **[POST] /api/users**
- **[DELETE] /api/users/{id}** (soft archive)
- **[PUT] /api/users/{id}/restore**

### Controller: CompaniesController

Base Route: `/api/companies`  
Authorization Required: Yes (`[Authorize]`, update requires `Admin`)

Endpoints:

- **[GET] /api/companies/current**
- **[PUT] /api/companies/current**

### Controller: GeneralLedgerController

Base Route: `/api/ledger`  
Authorization Required: Per endpoint role

Endpoints:

- **[GET] /api/ledger/accounts?includeArchived={bool}** (`Admin,Accounting,Management`)
- **[POST] /api/ledger/accounts** (`Admin,Accounting`)
- **[PUT] /api/ledger/accounts/{id}** (`Admin,Accounting`)
- **[DELETE] /api/ledger/accounts/{id}** (`Admin,Accounting`)
- **[PUT] /api/ledger/accounts/{id}/restore** (`Admin,Accounting`)
- **[GET] /api/ledger/trial-balance** (`Admin,Accounting,Management`)
- **[POST] /api/ledger/journal** (`Admin,Accounting`)

### Controller: AccountsPayableController

Base Route: `/api/payables`  
Authorization Required: Yes (`Admin,Accounting`)

Endpoints:

- **[GET] /api/payables/vendors?includeArchived={bool}**
- **[POST] /api/payables/vendors**
- **[PUT] /api/payables/vendors/{id}**
- **[DELETE] /api/payables/vendors/{id}**
- **[PUT] /api/payables/vendors/{id}/restore**
- **[GET] /api/payables/bills**
- **[POST] /api/payables/bill**
- **[POST] /api/payables/bill/{id}/pay**

### Controller: AccountsReceivableController

Base Route: `/api/receivables`  
Authorization Required: Yes (`Admin,Accounting`)

Endpoints:

- **[GET] /api/receivables/customers?includeArchived={bool}**
- **[POST] /api/receivables/customers**
- **[PUT] /api/receivables/customers/{id}**
- **[DELETE] /api/receivables/customers/{id}**
- **[PUT] /api/receivables/customers/{id}/restore**
- **[GET] /api/receivables/invoices**
- **[POST] /api/receivables/invoice**
- **[POST] /api/receivables/invoice/{id}/receive**

### Controller: ReportsController

Base Route: `/api/reports`  
Authorization Required: Yes (authenticated)

Endpoints:

- **[GET] /api/reports/invoices/{id}/pdf**
  - Returns invoice PDF file.
- **[GET] /api/reports/financials/pdf**
  - Returns financial report PDF file.

### Controller: AuditLogsController

Base Route: `/api/audit-logs`  
Authorization Required: Yes (`Admin`)

Endpoints:

- **[GET] /api/audit-logs** (returns latest 500 logs)

### Controller: PaymentController

Base Route: `/api/payments`  
Authorization Required: Mixed

Endpoints:

- **[POST] /api/payments/paymongo-source** (`Admin,Accounting`)
  - Creates PayMongo payment source and returns checkout link/source id.
- **[POST] /api/payments/webhook** (`AllowAnonymous`)
  - Receives PayMongo webhook payload.

### Controller: SuperAdminController

Base Route: `/api/superadmin`  
Authorization Required: Yes (`SuperAdmin`)

Endpoints:

- **[GET] /api/superadmin/dashboard**
- **[GET] /api/superadmin/companies**
- **[PUT] /api/superadmin/companies/{id}/status**
- **[PUT] /api/superadmin/companies/{id}/toggle**
- **[GET] /api/superadmin/users**
- **[PUT] /api/superadmin/users/{id}/status**
- **[PUT] /api/superadmin/users/{id}/toggle**
- **[GET] /api/superadmin/audit-logs**

---

## 3. Shared Models & DTO Documentation

### 3.1 DTO inventory

| Domain        | DTOs                                                                                                                                   |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Auth          | `LoginDTO`, `RegisterDTO`, `CompanyRegisterDTO`, `AuthResponseDTO`, `UpdateProfileDTO`, `ChangePasswordDTO`                            |
| Company       | `CompanyDTO`, `UpdateCompanyDTO`                                                                                                       |
| Users         | `UserDTO`, `GlobalUserDTO`, `UpdateUserStatusDTO`                                                                                      |
| Ledger        | `AccountDTO`, `CreateAccountDTO`, `UpdateAccountDTO`, `JournalEntryDTO`, `JournalEntryLineDTO`, `TrialBalanceDTO`, `AccountBalanceDTO` |
| AP            | `VendorDTO`, `CreateVendorDTO`, `UpdateVendorDTO`, `BillDTO`, `CreateBillDTO`                                                          |
| AR            | `CustomerDTO`, `CreateCustomerDTO`, `UpdateCustomerDTO`, `InvoiceDTO`, `CreateInvoiceDTO`                                              |
| Payments      | `RecordPaymentDTO`, `PaymentHistoryDTO`, `CreateSourceDTO`, `PaymentSourceResponseDTO`, PayMongo request/response/webhook DTOs         |
| Audit         | `AuditLogDTO`, `SuperAdminAuditLogDTO`, `SystemDashboardDTO`, `MonthlyActivityDTO`, `TenantDTO`, `UpdateCompanyStatusDTO`              |
| External data | `WorldBankDataPoint`, `WorldBankIndicator`, `FrankfurterResponse`, `FrankfurterRates`                                                  |

### 3.2 Mapping relationships (DTO ↔ Entity)

- `AccountDTO` ↔ `Account`
- `VendorDTO` ↔ `Vendor`
- `CustomerDTO` ↔ `Customer`
- `BillDTO` ↔ `Bill`
- `InvoiceDTO` ↔ `Invoice`
- `CompanyDTO` ↔ `Company`
- `UserDTO` ↔ `User (+Role.Name)`
- `AuditLogDTO` ↔ `AuditLog` + user email join
- `SuperAdminAuditLogDTO` ↔ `SuperAdminAuditLog`
- `JournalEntryDTO`/`JournalEntryLineDTO` ↔ `JournalEntry`/`JournalEntryLine`

Mapping is done manually inside controllers/services (no AutoMapper).

### 3.3 Validation attributes

Used heavily in DTOs, e.g.:

- `[Required]`, `[EmailAddress]`, `[StringLength]`, `[MinLength]`, `[Compare]`, `[MaxLength]`.
- Validation exists in DTO contracts; business rules are additionally enforced in services (e.g., overpayment checks, balanced journal entries).

### 3.4 Serialization behavior

- Enums use `JsonStringEnumConverter` in shared enums and payment DTO fields.
- External API models rely on `[JsonPropertyName]` to align with provider payloads (PayMongo, World Bank, Frankfurter).
- API entities hide sensitive fields (`PasswordHash`, `PasswordSalt`) with `[JsonIgnore]`.

---

## 4. Frontend Documentation

### 4.1 Frontend architecture summary

- Blazor WASM app with MudBlazor UI.
- Routing via `App.razor` + `AuthorizeRouteView`.
- Role-based menu rendering in `NavMenu.razor`.
- Feature services wrap API routes and are consumed by pages.

### 4.2 Pages/features

### Feature: Login (`/`)

Purpose: Authenticate user and establish session.  
Connected API Endpoints: `POST /api/auth/login`.  
Data Models Used: `LoginDTO`, `AuthResponseDTO`.  
User Flow: submit credentials → token stored → auth state updated → navigate dashboard.

### Feature: Register Company (`/register`)

Purpose: Tenant self-onboarding + admin account creation.  
Connected API Endpoints: `POST /api/auth/register-company`.  
Data Models Used: `CompanyRegisterDTO`, `AuthResponseDTO`.  
User Flow: fill company/admin form + recaptcha → API creates tenant + user + token.

### Feature: Dashboard (`/dashboard`)

Purpose: Main financial summary view with key metrics/charts.  
Connected API Endpoints: trial balance, ledger/AP/AR aggregations, external data service calls from client.  
Data Models Used: `TrialBalanceDTO`, account/bill/invoice DTOs.  
User Flow: authorized user opens dashboard → data fetched from multiple services.

### Feature: User Profile (`/profile`)

Purpose: Manage own profile and password.  
Connected API Endpoints: `PUT /api/auth/profile`, `PUT /api/auth/password`.  
Data Models Used: `UpdateProfileDTO`, `ChangePasswordDTO`.  
User Flow: edit profile/password → save → snackbar feedback.

### Feature: General Ledger Accounts (`/gl/accounts`)

Purpose: Manage chart of accounts (includes archive/restore).  
Connected API Endpoints: `/api/ledger/accounts*`.  
Data Models Used: `AccountDTO`, `CreateAccountDTO`, `UpdateAccountDTO`.  
User Flow: list/filter accounts → create/update/archive/restore by role.

### Feature: Journal Entries (`/gl/journal`)

Purpose: Create double-entry journal entries.  
Connected API Endpoints: `POST /api/ledger/journal`.  
Data Models Used: `JournalEntryDTO`, `JournalEntryLineDTO`.  
User Flow: build debit/credit lines → post entry → validation/error handling.

### Feature: Vendors (`/ap/vendors`)

Purpose: Manage AP vendors.  
Connected API Endpoints: `/api/payables/vendors*`.  
Data Models Used: `VendorDTO`, `CreateVendorDTO`, `UpdateVendorDTO`.  
User Flow: create/edit/archive/restore vendor records.

### Feature: Bills (`/ap/bills`) and Bill List (`/ap/bills/list`)

Purpose: Create and track vendor bills; record outgoing payments.  
Connected API Endpoints: `/api/payables/bill`, `/api/payables/bills`, `/api/payables/bill/{id}/pay`.  
Data Models Used: `CreateBillDTO`, `BillDTO`, `RecordPaymentDTO`.  
User Flow: create bill → auto ledger posting in API → view bills → pay bill.

### Feature: Customers (`/ar/customers`)

Purpose: Manage AR customers.  
Connected API Endpoints: `/api/receivables/customers*`.  
Data Models Used: `CustomerDTO`, `CreateCustomerDTO`, `UpdateCustomerDTO`.  
User Flow: create/edit/archive/restore customer data.

### Feature: Invoices (`/ar/invoices`) and Invoice List (`/ar/invoices/list`)

Purpose: Create and monitor customer invoices; export invoice PDF.  
Connected API Endpoints: `/api/receivables/invoice`, `/api/receivables/invoices`, `/api/reports/invoices/{id}/pdf`.  
Data Models Used: `CreateInvoiceDTO`, `InvoiceDTO`.  
User Flow: create invoice → ledger impact in API → list/filter/download.

### Feature: Receive Payment (`/ar/receive-payment`) + Payment Callback (`/payment-callback`)

Purpose: Handle PayMongo-based receivable payments.  
Connected API Endpoints: `POST /api/payments/paymongo-source`, `POST /api/receivables/invoice/{id}/receive`.  
Data Models Used: `CreateSourceDTO`, `PaymentSourceResponseDTO`, `RecordPaymentDTO`.  
User Flow: generate checkout URL → redirect to provider → callback page completes receipt flow.

### Feature: Financial Reports (`/reports/financials`)

Purpose: View trial balance context and export financial PDF reports.  
Connected API Endpoints: `GET /api/ledger/trial-balance`, `GET /api/reports/financials/pdf`.  
Data Models Used: `TrialBalanceDTO`, `CompanyDTO`.  
User Flow: open report page → fetch data → download generated PDF.

### Feature: Admin - User Management (`/admin/users`)

Purpose: Tenant admin user lifecycle management.  
Connected API Endpoints: `/api/users*`.  
Data Models Used: `UserDTO`, `RegisterDTO`.  
User Flow: view active/archived users → create, archive, restore.

### Feature: Admin - Audit Logs (`/admin/audit-logs`)

Purpose: View tenant audit trail.  
Connected API Endpoints: `GET /api/audit-logs`.  
Data Models Used: `AuditLogDTO`.  
User Flow: query logs and inspect action history.

### Feature: Admin - Company Settings (`/admin/company-settings`)

Purpose: Update tenant profile info.  
Connected API Endpoints: `GET/PUT /api/companies/current`.  
Data Models Used: `CompanyDTO`, `UpdateCompanyDTO`.  
User Flow: load current company → edit settings → save.

### Feature: SuperAdmin pages (`/superadmin/*`)

Purpose: Multi-tenant governance and platform monitoring.

- `SystemDashboard`: platform KPIs/trends/recent actions.
- `TenantManager`: list tenants + status management.
- `GlobalUserManager`: global user status management.
- `AdminAuditLogs`: superadmin action history.
  Connected API Endpoints: `/api/superadmin/*`.  
  Data Models Used: `SystemDashboardDTO`, `TenantDTO`, `GlobalUserDTO`, `SuperAdminAuditLogDTO`, status update DTOs.

---

## 5. Database & Data Flow

### 5.1 DbContext structure

Main entities: company/user/role, ledger accounts/journal entries, AP (vendors/bills), AR (customers/invoices), payments, tenant and superadmin audit logs.

### 5.2 Entity relationships

- `Company` 1..\* `User`, `Account`, `Vendor`, `Customer`, `Bill`, `Invoice`, `Payment` (via `CompanyId` on `BaseEntity`).
- `Role` 1..\* `User`.
- `JournalEntry` 1..\* `JournalEntryLine`.
- `JournalEntryLine` \*..1 `Account`.
- `Vendor` 1..\* `Bill`.
- `Customer` 1..\* `Invoice`.
- `Payment` optionally references `Invoice`, `Bill`, and `Account`.

### 5.3 Migrations

Migrations folder indicates active schema evolution (multi-tenancy, audit tenancy fixes, superadmin enhancements, etc.).

### 5.4 Data lifecycle

- Create/update operations set timestamps and tenant context automatically in `SaveChangesAsync`.
- Delete operations are soft-deletes for most `BaseEntity` entities (`IsDeleted=true`, `IsActive=false`).
- Read operations are tenant-scoped and soft-delete filtered unless explicitly bypassed.

### 5.5 Typical transaction flow

**Example: Create Invoice**

1. Client posts `CreateInvoiceDTO`.
2. API `AccountsReceivableController` calls `IReceivableService.CreateInvoiceAsync`.
3. Service creates invoice and corresponding journal entry lines.
4. DbContext saves invoice + ledger impacts.
5. Trial balance reflects changes.

**Example: Pay Bill**

1. Client posts `RecordPaymentDTO`.
2. Service validates amount (no overpayment).
3. Bill status/amount paid updated.
4. Payment record + journal entries are created.
5. Commit and return payment result.

---

## 6. Cross-Project Communication

### 6.1 How Shared is referenced

Both API and Client include project references to `AccountingSystem.Shared`, creating a unified contract model.

### 6.2 DTO movement between layers

- Client pages build DTOs → client services post/get to API.
- API controllers accept DTOs, call services.
- Services map DTOs to EF entities and back to DTOs.
- Response DTOs are rendered by client pages/components.

### 6.3 Model mapping strategy

- Manual in-code mapping inside services/controllers using LINQ projections and object initializers.
- This keeps mapping explicit but can become repetitive as models grow.

### 6.4 Dependency flow rules

- UI should not reference API internals.
- API should keep EF entities internal to server side; external contracts remain shared DTOs.
- Shared project should remain transport-contract focused (no infrastructure dependencies).

---

## 7. Technology Stack

- **.NET Version:** .NET 8 (`net8.0` for all projects)
- **ASP.NET Core Version:** ASP.NET Core 8 (Web API)
- **Blazor Type:** Blazor WebAssembly (WASM)
- **EF Core:** Entity Framework Core 8 + SQL Server provider
- **Database:** Microsoft SQL Server
- **Authentication:** JWT Bearer Tokens + role-based authorization
- **Hosting Strategy:** split frontend/backend apps (client and API served separately in dev; API exposes Swagger)
- **Additional libraries:** MudBlazor, Blazored.LocalStorage, QuestPDF, Swashbuckle

---

## 8. Security Overview

### 8.1 Authentication implementation

- JWT tokens issued at login/register-company.
- Signed with symmetric key from config.
- Token validation enforces issuer, audience, signing key, expiration.

### 8.2 Authorization policies

- Endpoint-level `[Authorize]` and `[Authorize(Roles=...)]` across controllers/pages.
- SuperAdmin endpoints strictly isolated under `SuperAdmin` role.

### 8.3 Role management

- Seeded roles: `Admin`, `Accounting`, `Management`, `SuperAdmin`.
- Navigation and page actions are role-sensitive in client UI.

### 8.4 Token handling

- Client stores token in local storage and rehydrates claims.
- API also parses token in middleware for tenant/access checks.

### 8.5 CORS configuration

- Explicit local origins allowed (`https://localhost:7150`, `http://localhost:5240`).
- Any headers/methods are accepted for those origins.

### 8.6 Input validation

- DTO data annotations validate shape/format.
- Additional business validation in services (e.g., balanced journal entries, overpayment prevention).

### 8.7 Protection against common attacks

Current strengths:

- Role checks and route protection.
- Tenant isolation via query filters and middleware status checks.
- Password hashing with HMACSHA512 + salt.
- Audit trail for mutating actions.

Current gaps/risks:

- Secrets/API keys stored in committed appsettings.
- Webhook signature verification currently stubbed (`VerifyWebhookSignature => true`).
- `RequireHttpsMetadata=false` in JWT bearer config (acceptable for local dev only).

---

## 9. Setup & Deployment Guide

### 9.1 Local run steps

```bash
# from repository root
dotnet restore AccountingSystem.sln

# API
cd AccountingSystem.Api
dotnet run

# Client (new terminal)
cd ../AccountingSystem.Client
dotnet run
```

### 9.2 Required configuration/environment

API requires configuration values for:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings:Secret`, `Issuer`, `Audience`, `ExpiryMinutes`
- `PayMongo:SecretKey` / `PublicKey`
- `Recaptcha:SecretKey`, `ScoreThreshold`

### 9.3 Configuration files

- `AccountingSystem.Api/appsettings.json`
- `AccountingSystem.Api/appsettings.Development.json`
- Optional user secrets (API csproj includes `UserSecretsId`).

### 9.4 Build commands

```bash
dotnet build AccountingSystem.sln
```

### 9.5 Production considerations

- Move all secrets to secure secret stores (Key Vault/env vars).
- Restrict CORS origins to production domains.
- Enable strict HTTPS and secure reverse-proxy settings.
- Validate webhook signatures cryptographically.
- Consider CI/CD migration strategy and zero-downtime deployment plan.

---

## 10. Recommendations & Improvements

### 10.1 Architectural improvements

- Introduce repository/specification pattern only where query complexity justifies it.
- Add application layer abstractions for clearer use-case boundaries.
- Consider splitting superadmin module into bounded context.

### 10.2 Refactoring suggestions

- Consolidate repeated CRUD patterns in AP/AR/GL services.
- Normalize response envelope/error model for consistent client handling.
- Add mapping utilities (or AutoMapper) to reduce manual mapping duplication.

### 10.3 Security improvements

- Remove hardcoded secrets from repository immediately.
- Implement real webhook signature verification for PayMongo.
- Add refresh token strategy or short-lived access token with silent renewal.
- Add rate limiting and lockout policy for auth endpoints.

### 10.4 Performance considerations

- Add pagination for heavy list endpoints (users, logs, invoices, bills).
- Add caching for static/reference datasets (chart of accounts, company profile).
- Review eager-loading patterns and index strategy for large tenants.

### 10.5 Scalability considerations

- Introduce structured logging + centralized observability.
- Consider background jobs for heavy report generation.
- Prepare horizontal scaling strategy (stateless API, distributed cache, queue-based integration).

---

## Appendix A — API example payloads

### Login request

```json
{
  "email": "admin@company.com",
  "password": "your-password"
}
```

### Create journal entry

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

### Record receivable payment

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

## Document Numbering

The system auto-generates document numbers per company for:
- Invoices (`INV-0001`)
- Journal entries (`JE-0001`)
- Customer payments received (`PR-0001`)
- Bill payments/checks (`CHK-0001`)

Vendor bills remain manually entered and should continue to use the vendor invoice number for accounting traceability.

Implementation notes:
- `DocumentSequences` stores `CompanyId`, `DocumentType`, `Prefix`, and `NextNumber`.
- The sequence service uses optimistic concurrency (`RowVersion`) with retries to avoid duplicate numbers under concurrent requests.
- Admins can edit prefix and next number in **Company Settings → Document Numbering**.
