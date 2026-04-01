# Security Documentation

> Comprehensive security reference for authentication, authorization, password policies, MFA, and secrets management.

---

## 1. Authentication Architecture

### 1.1 Authentication Flow Overview

The system uses **JWT Bearer authentication** with an optional **TOTP MFA** second factor.

#### Standard Login Flow

1. User opens Blazor client login page (`/`)
2. Client submits `LoginDTO` to `POST /api/auth/login`
3. API validates credentials against legacy user store
4. If MFA not enabled: API issues JWT token
5. If MFA enabled: API returns challenge token, user redirected to `/mfa-login`
6. Client stores JWT in browser local storage (`authToken`)
7. Client parses JWT claims for UI auth state
8. User navigated to dashboard

#### MFA Login Flow

1. Password step returns `RequiresTwoFactor = true` + `TwoFactorChallengeToken`
2. Client navigates to `/mfa-login`
3. User enters 6-digit TOTP code or recovery code
4. Client posts to `POST /api/auth/login/mfa`
5. On success, API issues normal JWT with same claim contract
6. Client stores token and completes login

#### Company Registration Flow

1. User opens `/register`
2. Page renders Google reCAPTCHA
3. Client submits `CompanyRegisterDTO` with reCAPTCHA token
4. API verifies reCAPTCHA via `CaptchaService`
5. API creates tenant company + initial admin user
6. API sends email confirmation
7. User must confirm email before login

### 1.2 Token Architecture

**JWT Claims:**
- `ClaimTypes.Name` - User email
- `ClaimTypes.Role` - Role name
- `UserId` - User ID (int)
- `role` - Role name (duplicate for client)
- `FullName` - User display name
- `CompanyId` - Tenant ID
- `CompanyName` - Tenant name

**Token Settings:**
- Symmetric signing key (`HmacSha256Signature`)
- Configurable expiry via `JwtSettings:ExpiryMinutes`
- Configurable clock skew via `JwtSettings:ClockSkewSeconds`

### 1.3 Dual-Store Architecture

The system operates with parallel identity stores:

| Store | Purpose |
|-------|---------|
| **Legacy Store** (`Users`, `Roles`) | Production source of truth for login, authorization |
| **Identity Store** (`AspNetUsers`, `AspNetRoles`) | MFA, email confirmation, password reset tokens |

Users are hydrated into Identity lazily during successful operations (login, password change, etc.).

---

## 2. Password Policies

### 2.1 Password Requirements

The shared password policy accepts two formats:

**Standard passwords:**
- 12-128 characters
- At least 3 of 4 character classes:
  - Uppercase letters
  - Lowercase letters
  - Digits
  - Special characters

**Passphrase-style passwords:**
- 16-128 characters
- At least 3 words (space-separated)

### 2.2 Password Storage

**Legacy Store (current production):**
- `HMACSHA512` with per-user salt
- Salt generated from HMAC key
- Both hash and salt stored as Base64

**Identity Store:**
- ASP.NET Core Identity password hasher
- Synchronized from legacy on successful login

### 2.3 Identity Password Validation

Identity is configured to delegate to the shared password policy:
- `RequiredLength = 12`
- `RequiredUniqueChars = 1`
- `RequireNonAlphanumeric = false`
- `RequireLowercase = false`
- `RequireUppercase = false`
- `RequireDigit = false`
- Custom `SharedPasswordIdentityValidator` enforces the full policy

---

## 3. Account Lockout & Rate Limiting

### 3.1 Account Lockout

Persistent lockout tracking on `User` entity:

| Setting | Default |
|---------|---------|
| `MaxFailedAccessAttempts` | 5 |
| `LockoutMinutes` | 15 |

After 5 failed login attempts, the account is temporarily locked. The lockout is stored in `LockoutEndUtc` and cleared after the configured period.

### 3.2 Rate Limiting

Endpoint-specific ASP.NET Core rate limiting:

| Endpoint | Limit | Window |
|----------|-------|--------|
| `POST /api/auth/login` | 5 requests | 60 seconds |
| `POST /api/auth/login/mfa` | 5 requests | 60 seconds |
| `POST /api/auth/register-company` | 3 requests | 600 seconds |
| `PUT /api/auth/password` | 5 requests | 600 seconds |
| `POST /api/auth/forgot-password` | 3 requests | 900 seconds |
| `POST /api/auth/reset-password` | 5 requests | 900 seconds |
| `POST /api/auth/confirm-email` | 5 requests | 900 seconds |
| `POST /api/auth/resend-confirmation` | 3 requests | 900 seconds |
| MFA management endpoints | 5 requests | 600 seconds |

Rate-limited responses include `429 Too Many Requests` with `Retry-After` header.

---

## 4. Multi-Factor Authentication (MFA)

### 4.1 Supported Methods

- **TOTP-based MFA** via authenticator apps
- Supported apps: Google Authenticator, any standard `otpauth://totp/...` URI app

**Not implemented:**
- SMS OTP
- Email OTP
- Push notifications
- Remember-device flows

### 4.2 MFA Enrollment

1. Authenticated user opens `/profile` → Security tab
2. Clicks "Set Up Authenticator"
3. API returns `SharedKey` and `AuthenticatorUri`
4. User scans QR code or enters manual key
5. User submits 6-digit verification code
6. API verifies code and enables MFA
7. API generates 10 recovery codes (shown once)

### 4.3 MFA Login Challenge

When MFA is enabled:

1. Password login returns `RequiresTwoFactor = true`
2. API issues short-lived `TwoFactorChallengeToken`
3. User enters TOTP code or recovery code
4. API validates and issues normal JWT

Challenge token settings:
- `Mfa:AuthenticatorIssuer` (default: `AccountingSystem`)
- `Mfa:LoginChallengeLifespanMinutes` (default: 5)

### 4.4 Recovery Codes

- 10 codes generated when MFA is first enabled
- Each code is single-use
- Regenerating replaces all previous codes
- Recovery-code login works only while MFA is enabled
- Codes are managed through ASP.NET Core Identity

### 4.5 MFA Management Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /api/auth/mfa` | Get MFA status |
| `POST /api/auth/mfa/authenticator/setup` | Start setup, get QR data |
| `POST /api/auth/mfa/authenticator/verify` | Verify code, enable MFA |
| `POST /api/auth/mfa/authenticator/reset` | Reset authenticator key |
| `POST /api/auth/mfa/recovery-codes/regenerate` | Regenerate codes |
| `POST /api/auth/mfa/disable` | Disable MFA |

Sensitive actions require re-authentication with:
- Current password, OR
- Current authenticator code, OR
- Recovery code

### 4.6 Security Notes

- TOTP codes are verified through Identity, never stored
- Recovery codes are invalidated on use
- Secrets, QR URIs, codes are not written to audit logs
- SuperAdmin has no MFA exemption (if enabled, second step required)

---

## 5. Email Confirmation

### 5.1 Enforcement Rule

All users must have a confirmed email before login token issuance.

**Exception:** `SuperAdmin` role is exempt from email confirmation requirement.

### 5.2 Confirmation Flow

1. Registration or resend triggers Identity token generation
2. Token is base64url-encoded
3. API resolves client base URL:
   - Development: request origin first, then `AppUrls:ClientBaseUrl`
   - Production: `AppUrls:ClientBaseUrl` only
4. Email contains link: `{baseUrl}/confirm-email?email=...&token=...`
5. Client `/confirm-email` page reads parameters
6. Page posts to `POST /api/auth/confirm-email`
7. API decodes token and confirms via Identity
8. Success/failure shown to user

### 5.3 Resend Behavior

- `POST /api/auth/resend-confirmation` is non-enumerating
- Can provision legacy-only account into Identity before sending
- Rate limited to prevent abuse

### 5.4 Token Lifetimes

Configurable via:
- `IdentityTokens:PasswordResetTokenLifespanMinutes` (default: 120)
- `IdentityTokens:EmailConfirmationTokenLifespanMinutes` (default: 1440)

---

## 6. Password Reset

### 6.1 Forgot Password Flow

1. User submits email to `POST /api/auth/forgot-password`
2. API generates Identity password reset token
3. Email sent with link: `{baseUrl}/reset-password?email=...&token=...`
4. User clicks link, enters new password
5. Client posts to `POST /api/auth/reset-password`
6. API validates token and updates password in both stores

### 6.2 Invited User Password Setup

For invited users (no initial password):

1. Email confirmation redirects to `/reset-password?...&flow=invite`
2. User sets password through same reset pipeline
3. Account activated only when both:
   - Email confirmed
   - Password set

---

## 7. Secrets Configuration

### 7.1 Required Configuration Keys

**Always required at startup:**

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | Database connection |
| `JwtSettings:Secret` | JWT signing key |
| `JwtSettings:Issuer` | JWT issuer |
| `JwtSettings:Audience` | JWT audience |
| `JwtSettings:ExpiryMinutes` | Token expiry |
| `JwtSettings:ClockSkewSeconds` | Clock tolerance |
| `IdentityTokens:PasswordResetTokenLifespanMinutes` | Reset token lifetime |
| `IdentityTokens:EmailConfirmationTokenLifespanMinutes` | Confirmation lifetime |
| `AuthSecurity:Lockout:MaxFailedAccessAttempts` | Lockout threshold |
| `AuthSecurity:Lockout:LockoutMinutes` | Lockout duration |
| `AuthSecurity:RateLimiting:*` | All rate limit settings |
| `AppUrls:ClientBaseUrl` | Client URL for email links |

**Required outside Development:**

| Key | Purpose |
|-----|---------|
| `PayMongo:SecretKey` | Payment processing |
| `Recaptcha:SecretKey` | Registration protection |
| `Smtp:*` | Email delivery settings |

**Conditionally required:**

| Key | Condition |
|-----|-----------|
| `BootstrapAdmin:*` | First run with no super-admin |

### 7.2 Local Development Setup

Use ASP.NET Core user-secrets:

```powershell
cd AccountingSystem.Api

# Database
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=...;Initial Catalog=AccountingSystemDB;..."

# JWT
dotnet user-secrets set "JwtSettings:Secret" "your-long-random-secret"
dotnet user-secrets set "JwtSettings:Issuer" "AccountingAPI"
dotnet user-secrets set "JwtSettings:Audience" "AccountingClient"
dotnet user-secrets set "JwtSettings:ExpiryMinutes" "60"
dotnet user-secrets set "JwtSettings:ClockSkewSeconds" "60"

# Identity tokens
dotnet user-secrets set "IdentityTokens:PasswordResetTokenLifespanMinutes" "120"
dotnet user-secrets set "IdentityTokens:EmailConfirmationTokenLifespanMinutes" "1440"

# Lockout
dotnet user-secrets set "AuthSecurity:Lockout:MaxFailedAccessAttempts" "5"
dotnet user-secrets set "AuthSecurity:Lockout:LockoutMinutes" "15"

# Rate limiting (example)
dotnet user-secrets set "AuthSecurity:RateLimiting:Login:PermitLimit" "5"
dotnet user-secrets set "AuthSecurity:RateLimiting:Login:WindowSeconds" "60"

# Client URL
dotnet user-secrets set "AppUrls:ClientBaseUrl" "https://localhost:7273"

# Bootstrap admin (first run only)
dotnet user-secrets set "BootstrapAdmin:Email" "superadmin@example.com"
dotnet user-secrets set "BootstrapAdmin:FullName" "Bootstrap Super Admin"
dotnet user-secrets set "BootstrapAdmin:InitialPassword" "your-secure-password"
```

### 7.3 Development SMTP Behavior

In Development environment:
- SMTP settings are optional
- If absent, API uses logging sender
- Password reset and confirmation links written to application logs
- If any SMTP values provided, full SMTP block is validated

### 7.4 Production Configuration

**Do not store production secrets in `appsettings.json`.**

Inject sensitive values through:
- Environment variables
- Container/orchestrator secret injection
- Managed secret store (Azure Key Vault, etc.)

### 7.5 Secret Rotation

**JWT Secret:**
- Rotating invalidates all existing JWTs
- Plan for maintenance window or forced re-login

**Database Credentials:**
- Update in secret store first
- Restart/redeploy API
- Verify migrations and connectivity

**PayMongo/reCAPTCHA:**
- Update secret values
- Restart/redeploy API
- Validate affected flows

**SMTP:**
- Update credentials
- Verify email delivery immediately

**Bootstrap Admin:**
- Treat as one-time bootstrap secret
- Remove or rotate after first super-admin created

---

## 8. Middleware Security

### 8.1 JWT Middleware

- Validates bearer token from `Authorization` header
- Stores user metadata in `HttpContext.Items`:
  - `User`, `Role`, `UserId`, `CompanyId`
- Used by downstream middleware for tenant access

### 8.2 Tenant Access Middleware

- Reads user/company context from `HttpContext.Items`
- Blocks non-`SuperAdmin` users when:
  - User is blocked
  - Company is blocked or suspended
- Returns `403 Forbidden` for blocked access

### 8.3 Audit Middleware

- Logs successful `POST`/`PUT`/`DELETE` requests
- Writes to `AuditLogs` table with:
  - Action name
  - User context
  - Request body (sanitized for sensitive endpoints)
  - IP address
  - Timestamp

**Sanitized endpoints:**
- Login (no credentials logged)
- Password change (no passwords logged)
- MFA endpoints (no codes/secrets logged)

---

## 9. Client Security

### 9.1 Token Storage

- JWT stored in browser local storage (`authToken`)
- Persisted via `Blazored.LocalStorage`
- Accessible to JavaScript (XSS risk)

### 9.2 Auth State Management

`CustomAuthStateProvider`:
- Reads token from local storage
- Decodes JWT claims client-side
- Builds `ClaimsPrincipal` for UI
- Does NOT validate signature or expiry client-side
- Actual validation only occurs on API calls

### 9.3 Client-Side Authorization

- `AuthorizeRouteView` in `App.razor`
- Page-level `[Authorize]` attributes
- `AuthorizeView` components for role-based UI
- Navigation visibility based on parsed claims

### 9.4 Logout

Logout is client-side only:
1. Remove `authToken` from local storage
2. Clear authorization header
3. Notify anonymous auth state
4. Navigate to login

**Note:** No server-side token revocation exists.

---

## 10. Audit Logging

### 10.1 What Is Logged

| Event Type | Examples |
|------------|----------|
| Auth success | Login, registration |
| Auth failure | Failed login, lockout |
| Security events | Password change, MFA enable/disable |
| Business actions | Create/update/delete operations |
| Admin actions | User status changes, tenant management |

### 10.2 Sensitive Data Handling

The following are **never** logged:
- Passwords (plaintext or hashed)
- JWT tokens
- Authorization headers
- CAPTCHA tokens
- MFA codes and secrets
- Recovery codes

### 10.3 IP Address Logging

- IP addresses captured in audit logs
- Historical rows may show "Unavailable"
- Timestamps in Philippine local time (UTC+08:00)

---

## 11. Security Configuration Checklist

### Development

- [ ] Configure user-secrets with all required keys
- [ ] Set `AppUrls:ClientBaseUrl` to match local client
- [ ] Configure `BootstrapAdmin:*` for first run
- [ ] SMTP optional (logs links if absent)

### Production

- [ ] Remove all secrets from `appsettings.json`
- [ ] Inject secrets via environment/secret store
- [ ] Set `RequireHttpsMetadata = true` for JWT
- [ ] Restrict CORS to production domains
- [ ] Configure real SMTP provider
- [ ] Set appropriate token lifetimes
- [ ] Configure rate limits for load
- [ ] Enable secure reverse proxy settings
- [ ] Implement webhook signature verification (PayMongo)

---

## 12. Identity Migration Notes

### 12.1 Current State

- Legacy store remains production source of truth
- Identity operates in parallel for MFA/email confirmation
- Users hydrated into Identity lazily on successful operations

### 12.2 Legacy Password Migration

- No bulk password migration
- On login: legacy password verified, Identity password updated
- Future migration path:
  1. User attempts login
  2. System verifies legacy hash
  3. System creates/updates Identity record
  4. System writes Identity-compatible hash
  5. Links via `LegacyUserId`

### 12.3 Rollback Capability

Rollback is straightforward because active auth path unchanged:
- Revert Identity context/entities
- Roll back Identity migrations
- No client/contract changes required
