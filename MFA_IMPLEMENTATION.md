# MFA Implementation

## Summary

Phase 8 adds optional TOTP-based MFA using ASP.NET Core Identity authenticator support while keeping the existing JWT-based API/client architecture.

Supported authenticator apps:
- Google Authenticator
- Any app that supports standard `otpauth://totp/...` URIs and 6-digit TOTP codes

Not implemented:
- SMS OTP
- email OTP
- push notifications
- remember-device / remember-browser flows

## Flow

### 1. Password step

- Client calls `POST /api/auth/login` with email and password.
- If MFA is not enabled, the API returns the normal JWT `AuthResponseDTO`.
- If MFA is enabled, the API returns:
  - `RequiresTwoFactor = true`
  - `TwoFactorChallengeToken`
  - `Token = ""`

The challenge token is short-lived, signed, purpose-bound, and used only for the second login step.

### 2. MFA step

- Client navigates to `/mfa-login`.
- User enters either:
  - a 6-digit Google Authenticator code, or
  - a recovery code
- Client calls `POST /api/auth/login/mfa`.
- On success, the API issues the normal JWT with the existing claim contract.

### 3. MFA enrollment

- Authenticated user opens `/profile` and starts authenticator setup.
- API returns:
  - `SharedKey`
  - `AuthenticatorUri`
- Client renders the QR code from the `AuthenticatorUri`.
- User scans it with Google Authenticator or enters the manual key.
- User submits a 6-digit code.
- API verifies the code and only then enables MFA.
- API generates 10 recovery codes and returns them once.

### 4. MFA management

Authenticated users can:
- view MFA status
- reset the authenticator key
- regenerate recovery codes
- disable MFA

Sensitive actions require exactly one re-authentication factor:
- current password, or
- current authenticator code, or
- a recovery code

## Setup Steps

1. Sign in to the app.
2. Open `/profile`.
3. In the security tab, choose `Set Up Authenticator`.
4. Scan the QR code with Google Authenticator.
5. If scanning is unavailable, enter the manual key in Google Authenticator.
6. Enter the 6-digit code shown by the app.
7. Save the recovery codes shown after verification.

## Recovery Codes

- Recovery codes are generated when MFA is first enabled.
- Regenerating recovery codes replaces the previous usable set.
- Recovery codes are single-use.
- Recovery-code login works only while MFA is enabled.
- The UI shows recovery codes only immediately after enable/regeneration.

## Configuration

Safe non-secret configuration:

- `Mfa:AuthenticatorIssuer`
  - default: `AccountingSystem`
- `Mfa:LoginChallengeLifespanMinutes`
  - default: `5`

Rate-limit configuration was added for:

- `AuthSecurity:RateLimiting:LoginMfa`
- `AuthSecurity:RateLimiting:MfaManage`

## Key API Endpoints

- `POST /api/auth/login`
- `POST /api/auth/login/mfa`
- `GET /api/auth/mfa`
- `POST /api/auth/mfa/authenticator/setup`
- `POST /api/auth/mfa/authenticator/reset`
- `POST /api/auth/mfa/authenticator/verify`
- `POST /api/auth/mfa/recovery-codes/regenerate`
- `POST /api/auth/mfa/disable`

## Security Notes

- TOTP codes are verified through ASP.NET Core Identity and are never stored.
- Recovery codes are managed through Identity and are invalidated on use.
- Secrets, QR URIs, TOTP codes, and recovery codes are not written to audit logs.
- SuperAdmin has no MFA exemption in this phase. If MFA is enabled on the account, the second step is required.
