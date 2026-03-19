# SECURITY_ASSESSMENT

## Phase Scope

This document inventories the current authentication and security implementation in the .NET 8 `AccountingSystem` solution as implemented today. It is based on the current code in `AccountingSystem.Api` and `AccountingSystem.Client`.

This phase does not change authentication behavior, DTOs, schema, migrations, or endpoint contracts.

## Current Auth Flow Diagram

### Login flow

1. User opens the Blazor client login page at `/`.
2. `AccountingSystem.Client/Pages/Auth/Login.razor` submits `LoginDTO` through `AccountingSystem.Client/Services/AuthService.cs`.
3. Client `AuthService` calls `ApiService.PostAsync("api/auth/login", requiresAuth: false)`.
4. `AccountingSystem.Api/Controllers/AuthController.cs` forwards the request to `AccountingSystem.Api/Services/AuthService.cs::LoginAsync`.
5. `LoginAsync`:
   - Loads the user with `IgnoreQueryFilters()` and includes the role.
   - Rejects deleted, blocked, inactive, or invalid-password users.
   - Loads the company with `IgnoreQueryFilters()`.
   - Rejects blocked or suspended companies for non-`SuperAdmin` users.
   - Generates a JWT with user, role, and tenant claims.
6. API returns `AuthResponseDTO` containing the JWT and account metadata.
7. Client stores the token in browser local storage under `authToken`.
8. Client `CustomAuthStateProvider` parses the JWT payload into claims and marks the UI as authenticated.
9. Client navigates to `/dashboard` or `/superadmin/dashboard` based on the returned role.

### Register-company flow

1. User opens `/register`.
2. `AccountingSystem.Client/Pages/Auth/RegisterCompany.razor` renders Google reCAPTCHA using a hard-coded site key and `wwwroot/js/recaptcha.js`.
3. Client submits `CompanyRegisterDTO` with the reCAPTCHA token through client `AuthService`.
4. API `AuthController.RegisterCompany` calls `AuthService.RegisterCompanyAsync`.
5. `RegisterCompanyAsync`:
   - Verifies the reCAPTCHA token through `CaptchaService`.
   - Rejects duplicate email addresses using `Users.IgnoreQueryFilters()`.
   - Creates a new `Company`.
   - Loads the `Admin` role.
   - Creates password hash and salt.
   - Creates the initial admin `User`.
   - Seeds base company data inside a database transaction.
   - Generates a JWT for the new admin account.
6. API returns `AuthResponseDTO`.
7. Client stores the token in local storage and updates `CustomAuthStateProvider`.
8. Client auto-navigates to `/dashboard`.

### Authenticated request flow

1. A protected client page or service makes an API call through `ApiService`.
2. `ApiService` reads `authToken` from local storage and sets `Authorization: Bearer <token>` on the shared `HttpClient`.
3. API pipeline runs in this order:
   - `UseAuthentication()`
   - `JwtMiddleware`
   - `TenantAccessMiddleware`
   - `UseAuthorization()`
   - `AuditMiddleware`
4. `UseAuthentication()` runs the configured JWT Bearer handler and populates `HttpContext.User`.
5. `JwtMiddleware` manually validates the same JWT again and copies selected values into `HttpContext.Items`:
   - `User`
   - `Role`
   - `UserId`
   - `CompanyId`
6. `TenantAccessMiddleware` checks the current user and company status using the `HttpContext.Items` values and blocks blocked or suspended access for non-`SuperAdmin` users.
7. `UseAuthorization()` enforces `[Authorize]` and `[Authorize(Roles = ...)]` on controllers/actions.
8. Controllers and services read identity from `HttpContext.User`, JWT claims, or `ITenantService`.
9. `ITenantService` resolves `CompanyId` from `HttpContext.User`.
10. `AccountingDbContext` global query filters scope tenant data by the current tenant ID.
11. `AuditMiddleware` logs successful `POST`/`PUT`/`DELETE` requests after controller execution.

### Profile/password update flow

1. Authenticated client pages call:
   - `PUT /api/auth/profile`
   - `PUT /api/auth/password`
2. `AuthController` extracts `UserId` from the JWT claim.
3. `AuthService` updates the current user record or verifies the current password and rewrites `PasswordHash` and `PasswordSalt`.
4. The existing JWT is not reissued after profile or password changes.
5. Client UI keeps using the previously stored JWT until logout or expiry.

## Password Storage / Hash Strategy Used Today

- Passwords are stored on `AccountingSystem.Api/Models/AuthModels.cs::User` as:
  - `PasswordHash` string
  - `PasswordSalt` string
- Both values are Base64-encoded before persistence.
- Hashing is performed with `HMACSHA512`.
- Salt generation uses the `HMACSHA512` key itself.
- Verification recomputes the HMAC using the stored salt and compares byte-by-byte.
- This strategy is used in:
  - `AuthService.RegisterCompanyAsync`
  - `AuthService.RegisterAsync`
  - `AuthService.ChangePasswordAsync`
  - `AuthService.LoginAsync`
  - `DataSeeder.CreatePasswordHash`
- Admin-created users created through `POST /api/users` use the same password hashing helper.

## JWT Issuance / Validation Path

### Issuance

- JWT configuration is read from `JwtSettings` in API configuration.
- `AuthService.GenerateJwtToken` issues the token using a symmetric signing key and `HmacSha256Signature`.
- Issued claims currently include:
  - `ClaimTypes.Name` with the user email
  - `ClaimTypes.Role` with the role name
  - `UserId`
  - `role`
  - `FullName`
  - `CompanyId`
  - `CompanyName`
- Expiry is controlled by `JwtSettings:ExpiryMinutes`.
- The response contract returned to the client is `AuthResponseDTO`.

### Server-side validation

- `AccountingSystem.Api/Program.cs` configures `AddAuthentication().AddJwtBearer(...)`.
- Current validation settings:
  - `ValidateIssuerSigningKey = true`
  - `ValidateIssuer = true`
  - `ValidateAudience = true`
  - `ClockSkew = TimeSpan.Zero`
  - `RequireHttpsMetadata = false`
  - `SaveToken = true`
- `JwtMiddleware` then manually validates the token again using the same secret, issuer, audience, and zero clock skew.
- `JwtMiddleware` copies selected values into `HttpContext.Items` for later middleware use.

### Client-side handling

- `AccountingSystem.Client/Auth/CustomAuthStateProvider.cs` does not validate the token signature or expiry.
- It splits the JWT, Base64-decodes the payload, deserializes the JSON claims, and builds a `ClaimsIdentity`.
- Client route/UI authorization is driven from the parsed claims through:
  - `CascadingAuthenticationState`
  - `AuthorizeRouteView`
  - page-level `[Authorize]`
  - `AuthorizeView`

## Where Secrets Are Loaded From

### API configuration sources

- The API host uses `WebApplication.CreateBuilder(args)`, so configuration is loaded from the normal ASP.NET Core providers, including:
  - `appsettings.json`
  - `appsettings.{Environment}.json`
  - environment variables
  - command-line arguments
- In the current repository state, `AccountingSystem.Api/appsettings.json` contains committed values for:
  - `ConnectionStrings:DefaultConnection`
  - `JwtSettings:Secret`
  - `PayMongo:SecretKey`
  - `PayMongo:PublicKey`
  - `Recaptcha:SecretKey`
- `AccountingSystem.Api/appsettings.Development.json` currently only overrides logging and does not move secrets out of source control.

### Current secret consumers

- `Program.cs` reads `JwtSettings`.
- `AuthService.GenerateJwtToken` reads `JwtSettings`.
- `JwtMiddleware` reads `JwtSettings`.
- `CaptchaService` reads `Recaptcha:SecretKey` and `Recaptcha:ScoreThreshold`.
- `PaymentService` reads `PayMongo:SecretKey`.
- EF Core SQL Server registration reads `ConnectionStrings:DefaultConnection`.

### Client-side configuration

- `AccountingSystem.Client/wwwroot/index.html` loads the Google reCAPTCHA script.
- `AccountingSystem.Client/Pages/Auth/RegisterCompany.razor` contains a hard-coded reCAPTCHA site key constant.
- The client stores bearer tokens in local storage under `authToken`.

## Auth / Account-Related Endpoints

### Public auth endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | Anonymous | Login and JWT issuance |
| POST | `/api/auth/register-company` | Anonymous | Create tenant company, create admin user, return JWT |

### Authenticated account endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| PUT | `/api/auth/profile` | Authenticated | Update current user profile |
| PUT | `/api/auth/password` | Authenticated | Change current user password |

### Tenant user-management endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| GET | `/api/users` | `Admin` | List tenant users |
| POST | `/api/users` | `Admin` | Create tenant user account |
| DELETE | `/api/users/{id}` | `Admin` | Soft-archive tenant user |
| PUT | `/api/users/{id}/restore` | `Admin` | Restore archived tenant user |

### Company/account settings endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| GET | `/api/companies/current` | Authenticated | Read current tenant company profile |
| PUT | `/api/companies/current` | `Admin` | Update current tenant company profile |

### Super-admin access/account endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| GET | `/api/superadmin/companies` | `SuperAdmin` | List tenant companies |
| PUT | `/api/superadmin/companies/{id}/status` | `SuperAdmin` | Change tenant company status |
| PUT | `/api/superadmin/companies/{id}/toggle` | `SuperAdmin` | Toggle tenant company active status |
| GET | `/api/superadmin/users` | `SuperAdmin` | List tenant users across companies |
| PUT | `/api/superadmin/users/{id}/status` | `SuperAdmin` | Change user status |
| PUT | `/api/superadmin/users/{id}/toggle` | `SuperAdmin` | Toggle user active status |
| GET | `/api/superadmin/audit-logs` | `SuperAdmin` | Review super-admin status changes |

### Payment/auth-adjacent endpoints

| Method | Route | Access | Purpose |
| --- | --- | --- | --- |
| POST | `/api/payments/paymongo-source` | `Admin,Accounting` | Create payment source using stored PayMongo credentials |
| POST | `/api/payments/webhook` | Anonymous | Receive PayMongo webhook and trust signature verification result |

### Not present today

- No logout endpoint
- No refresh-token endpoint
- No password reset / forgot-password flow
- No email confirmation flow
- No MFA endpoints

## Middleware and Access-Control Touchpoints

### API pipeline

- `UseAuthentication()`
  - Validates JWT Bearer tokens and populates `HttpContext.User`.
- `JwtMiddleware`
  - Revalidates the JWT and writes `User`, `Role`, `UserId`, and `CompanyId` into `HttpContext.Items`.
- `TenantAccessMiddleware`
  - Blocks blocked users and blocked/suspended companies for non-`SuperAdmin` requests.
- `UseAuthorization()`
  - Applies controller/action authorization attributes.
- `AuditMiddleware`
  - Logs successful state-changing requests, including some auth/account flows.

### Authorization boundaries

- API controllers rely on `[Authorize]` and `[Authorize(Roles = ...)]`.
- Client pages use page-level `[Authorize]` attributes.
- `App.razor` uses `AuthorizeRouteView`.
- Layout and navigation use `AuthorizeView` to show/hide tenant and role-based UI.

### Tenant isolation

- `TenantService` resolves `CompanyId` from the authenticated user claims.
- `AccountingDbContext` applies tenant query filters to:
  - `Users`
  - `Accounts`
  - `JournalEntries`
  - `FiscalYearCloses`
  - `Vendors`
  - `Customers`
  - `Bills`
  - `Invoices`
  - `Payments`
  - `DocumentSequences`
  - `AuditLogs`
- `SaveChangesAsync` also auto-assigns `CompanyId` for new `BaseEntity` records when a current tenant exists.

## Client Token Storage and Auth State Mechanism

- Token persistence is handled by `AccountingSystem.Client/Services/TokenStorageService.cs`.
- The token key is `authToken`.
- Storage backend is browser local storage via `Blazored.LocalStorage`.
- `ApiService`:
  - Reads the token before authenticated calls.
  - Sets `HttpClient.DefaultRequestHeaders.Authorization`.
  - Clears the header for anonymous calls or logout.
- `CustomAuthStateProvider`:
  - Reads the token from local storage.
  - Decodes JWT claims client-side.
  - Builds a client-side `ClaimsPrincipal`.
  - Notifies the app after login, registration, or logout.
- `MainLayout`, `NavMenu`, `Login.razor`, and `UserProfile.razor` read role and tenant/user claims from that client-side principal.
- Logout is purely client-side:
  - remove `authToken`
  - clear the auth header
  - notify anonymous auth state

## Primary Code Locations

- API bootstrap and auth config:
  - `AccountingSystem.Api/Program.cs`
- API auth endpoints and service:
  - `AccountingSystem.Api/Controllers/AuthController.cs`
  - `AccountingSystem.Api/Services/AuthService.cs`
- API middleware:
  - `AccountingSystem.Api/Middleware/JwtMiddleware.cs`
  - `AccountingSystem.Api/Middleware/TenantAccessMiddleware.cs`
  - `AccountingSystem.Api/Middleware/AuditMiddleware.cs`
- API tenant/data boundary:
  - `AccountingSystem.Api/Services/TenantService.cs`
  - `AccountingSystem.Api/Data/AccountingDbContext.cs`
- API seed data and default credentials:
  - `AccountingSystem.Api/Data/DataSeeder.cs`
- API auth-adjacent integrations:
  - `AccountingSystem.Api/Services/CaptchaService.cs`
  - `AccountingSystem.Api/Services/PaymentService.cs`
  - `AccountingSystem.Api/Controllers/PaymentController.cs`
- Client auth handling:
  - `AccountingSystem.Client/Auth/CustomAuthStateProvider.cs`
  - `AccountingSystem.Client/Services/TokenStorageService.cs`
  - `AccountingSystem.Client/Services/ApiService.cs`
  - `AccountingSystem.Client/Services/AuthService.cs`
  - `AccountingSystem.Client/App.razor`
  - `AccountingSystem.Client/Pages/Auth/Login.razor`
  - `AccountingSystem.Client/Pages/Auth/RegisterCompany.razor`
  - `AccountingSystem.Client/Pages/Auth/UserProfile.razor`

## Security Risks by Severity

### Critical

1. Committed secrets and connection data in source control
   - `AccountingSystem.Api/appsettings.json` contains the SQL connection string, JWT secret, PayMongo keys, and reCAPTCHA secret.
   - This is incompatible with production secret-management expectations and increases blast radius if the repository is copied or leaked.

2. Webhook signature verification is effectively disabled
   - `PaymentService.VerifyWebhookSignature` currently returns `true` unconditionally.
   - `POST /api/payments/webhook` is anonymous, so any caller can submit a payload that will be treated as verified.

3. Audit logging captures credential-bearing request bodies
   - `AuditMiddleware` logs successful `POST`/`PUT`/`DELETE` request bodies after execution.
   - Login is redacted, but successful `POST /api/auth/register-company` and `PUT /api/auth/password` are not redacted.
   - That means plaintext registration password, password-change values, and reCAPTCHA token can be written into audit storage.

4. Seeded default credentials are created automatically at startup
   - `DataSeeder.SeedDataAsync` creates fixed accounts such as:
     - `sysadmin@accsys.com / master123`
     - `admin@accsys.com / admin123`
     - `accountant@accsys.com / user123`
     - `manager@accsys.com / user123`
   - Startup runs `Database.Migrate()` and seeding automatically, so these credentials are not limited to an isolated test-only path.

### High

1. JWT is stored in browser local storage
   - The client persists `authToken` in local storage.
   - Any successful XSS in the client can exfiltrate the bearer token and replay it until expiry.

2. Password hashing uses raw `HMACSHA512` rather than a slow password KDF
   - The current design uses salted HMAC, but not a password-specific adaptive hash such as PBKDF2, bcrypt, scrypt, or Argon2.
   - That weakens offline resistance if password hashes are exposed.

3. `RequireHttpsMetadata = false` is not environment-gated
   - JWT Bearer configuration disables HTTPS metadata checks globally in `Program.cs`.
   - The prompt correctly notes this is acceptable only for local development, but the current code does not limit it to development.

4. No refresh, revocation, or server-side logout path exists
   - Tokens are issued with expiry only.
   - There is no refresh-token flow, token revocation store, session invalidation, or logout endpoint.
   - Password changes and account status changes rely on future request checks, not token invalidation.

5. Client trusts decoded JWT payload for UI auth state without verification
   - `CustomAuthStateProvider` treats any stored JWT-shaped string as authenticated UI state after decoding the payload.
   - Signature and expiry are enforced only when the API receives the token, not when the client rebuilds auth state.

### Medium

1. JWT validation is duplicated and coupled to `HttpContext.Items`
   - `UseAuthentication()` already validates the token into `HttpContext.User`.
   - `JwtMiddleware` validates the token again and then downstream middleware depends on `HttpContext.Items`.
   - This creates two identity paths to keep consistent during future auth migration.

2. Client/UI auth state can drift from actual server auth state
   - The client continues to present the user as authenticated until local logout or token removal.
   - Profile changes do not reissue the token.
   - Password changes do not invalidate the current token.
   - Expired or otherwise invalid tokens can leave the UI appearing logged in until an API call fails.

3. reCAPTCHA client configuration is hard-coded in the page
   - The public site key is embedded directly in `RegisterCompany.razor`.
   - This is not a secret by itself, but it is not centralized or environment-configurable and will complicate deployment hygiene.

### Low

1. Undocumented super-admin toggle endpoints remain active
   - `PUT /api/superadmin/companies/{id}/toggle` and `PUT /api/superadmin/users/{id}/toggle` are marked "not used" in code comments but are still routable.
   - They are protected by `SuperAdmin` role checks, so the immediate severity is low, but they still expand the live surface area.

2. Auth endpoints return raw exception messages in response bodies
   - `AuthController` returns `ex.Message` for login, registration, profile, and password failures.
   - Current messages are mostly user-oriented, but this pattern should be reviewed when hardening the auth layer.

## Migration Readiness

### What can stay

- The current 3-project split:
  - `AccountingSystem.Api`
  - `AccountingSystem.Client`
  - `AccountingSystem.Shared`
- Shared DTOs as transport contracts only.
- API ownership of authentication, authorization, persistence, business rules, and middleware.
- Client as a thin UI/API consumer.
- Existing route shapes and business modules.
- Role and tenant concepts:
  - `Admin`
  - `Accounting`
  - `Management`
  - `SuperAdmin`
- Server-side authorization at controller/action boundaries.
- Tenant-scoped data access model through claims plus EF query filters.

### What must change before introducing Identity

- Replace current `PasswordHash` / `PasswordSalt` handling and migrate stored password material to an Identity-compatible password hasher.
- Move secrets and connection values out of committed `appsettings.json`.
- Remove or tightly gate seeded default credentials.
- Implement real webhook signature verification.
- Redact or suppress credential-bearing request bodies in audit logging.
- Make JWT/HTTPS behavior environment-specific, including the current `RequireHttpsMetadata = false` setting.
- Reduce or remove the custom `JwtMiddleware` dependency on manual validation and `HttpContext.Items`.

### What can be migrated later

- Refresh-token and revocation design.
- Contract-preserving replacement of auth endpoints while keeping existing client/API boundaries intact.
- Claim normalization and simplification, including duplicate role-related claims.
- MFA, email confirmation, forgot-password, and account-recovery flows.

## Phase 1 Conclusion

- Current auth is a custom JWT-based implementation owned by the API, with the client acting as a token consumer and claim-driven UI shell.
- The current login flow should remain stable for this phase.
- The main immediate security concerns are secret management, webhook trust, audit redaction, default seeded credentials, and the password-hashing approach.
