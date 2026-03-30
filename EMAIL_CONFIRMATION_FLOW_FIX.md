# EMAIL_CONFIRMATION_FLOW_FIX

## Root Cause Found

The broken local confirmation link was caused by a stale `AppUrls:ClientBaseUrl` value pointing to `https://localhost:5173`.

In this solution, local development is currently running the hosted Blazor client through the API origin, which the user was already accessing at `https://localhost:7273/register`. The confirmation email therefore sent users to a port that was no longer serving the client, which produced `ERR_CONNECTION_REFUSED` before the `/confirm-email` page could even load.

The confirm-email flow itself was otherwise already wired correctly:

- the API generated and base64url-encoded the Identity email confirmation token
- the client already had a `/confirm-email` route
- the page already read `email` and `token` from the query string
- the page already posted to `POST /api/auth/confirm-email`
- the API already decoded and confirmed the token correctly

## Files Changed

- `C:\SoftDev_repo\AccountingSystem\AccountingSystem.Api\Services\AuthService.cs`
- `C:\SoftDev_repo\AccountingSystem\AccountingSystem.Api\appsettings.Development.json`
- `C:\SoftDev_repo\AccountingSystem\AccountingSystem.Api\appsettings.Template.json`
- `C:\SoftDev_repo\AccountingSystem\AccountingSystem.API.Tests\UnitTest1.cs`
- `C:\SoftDev_repo\AccountingSystem\EMAIL_CONFIRMATION_FLOW_FIX.md`

## Local Configuration Required

### Canonical setting

Email links still use `AppUrls:ClientBaseUrl` as the explicit configured fallback.

For the hosted local-development setup in this repo, the correct local value is:

```json
"AppUrls": {
  "ClientBaseUrl": "https://localhost:7273"
}
```

### Development behavior

In Development, link generation now also inspects the active browser request origin:

- if the request came from `https://localhost:7273`, links use `https://localhost:7273`
- if the request came from another local client origin, that origin is used instead
- if no request origin is available, the API falls back to `AppUrls:ClientBaseUrl`

This prevents stale localhost ports from breaking local verification while preserving a single explicit config value for non-local and non-request-driven scenarios.

## How The Confirm-Email Flow Now Works

1. Registration or resend-confirmation triggers Identity token generation in the API.
2. The token is base64url-encoded.
3. The API resolves the client base URL:
   - Development: current request origin first, then `AppUrls:ClientBaseUrl`
   - non-Development: `AppUrls:ClientBaseUrl`
4. The email contains a link like:
   - `https://localhost:7273/confirm-email?email=...&token=...`
5. The hosted Blazor client serves the `/confirm-email` page.
6. The page reads `email` and `token` from the query string.
7. The page posts them to `POST /api/auth/confirm-email`.
8. The API decodes the token and calls Identity email confirmation.
9. The page shows a clear success or failure state.

## Manual Test Steps

1. Start the API:
   - `dotnet run --project C:\SoftDev_repo\AccountingSystem\AccountingSystem.Api\AccountingSystem.Api.csproj`
2. Open the hosted client from the API origin:
   - `https://localhost:7273/register`
3. Register a new company account.
4. Open the confirmation email and verify the link target starts with:
   - `https://localhost:7273/confirm-email?...`
5. Click the link and confirm the browser loads the local client page instead of showing `ERR_CONNECTION_REFUSED`.
6. Confirm the page shows:
   - success if the token is valid
   - a clear failure message if the token is invalid or expired
7. Attempt login after confirmation and verify the account can sign in normally.

## Notes

- The same base-URL resolution fix is shared by password-reset link generation because it used the same broken local URL source.
- No password reset behavior or confirmation-token behavior was redesigned beyond the shared local link-base repair.
