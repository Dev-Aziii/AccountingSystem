# Login Attempt Policy

## Implemented Policy

- Attempt window: `15 minutes`
- Max failed attempts within the rolling window: `5`
- First lockout: `5 minutes`
- Second consecutive lockout: `15 minutes`
- Third and subsequent consecutive lockouts: `30 minutes`
- IP rate limit for `/api/auth/login`: `5 requests per minute`
- Automatic account disable threshold: `5 lockout events in 24 hours`
- Persistent disabled state: existing `Blocked` status

Temporary lockout and persistent disable are separate states:

- Temporary lockout uses `LockoutEndUtc` on the legacy user plus mirrored Identity `LockoutEnd`
- Persistent disable uses `Status = Blocked` and `IsActive = false`

## Data Model Changes

The policy uses a dedicated security-state model in `AccountingDbContext`:

- `UserLoginSecurityState`
  - `UserId`
  - `ConsecutiveLockoutCount`
  - `LastSuccessfulLoginAtUtc`
  - `DisabledAtUtc`
  - `DisabledReason`
- `UserFailedLoginAttempt`
  - durable failed-attempt timestamps for the 15-minute rolling window
- `UserLockoutEvent`
  - durable lockout history for escalation tracking and the 24-hour auto-disable threshold

Indexes:

- `UserFailedLoginAttempts(UserId, OccurredAtUtc)`
- `UserLockoutEvents(UserId, OccurredAtUtc)`
- unique `UserLoginSecurityStates(UserId)`

Migration:

- `20260401170020_AddLoginAttemptPolicySecurityState`

## Reset Logic

The implemented reset rule is:

- a full successful sign-in resets the failed-attempt window and the consecutive lockout escalation state

For MFA-enabled accounts this reset does **not** happen when the password is accepted. It happens only after the MFA challenge is completed and the final JWT is issued.

Admin re-enable also clears:

- active temporary lockout
- current failed-attempt window
- current consecutive lockout count

Admin re-enable does **not** delete historical `UserLockoutEvent` records.

## Lockout Escalation Logic

Failed password attempts are tracked in a rolling 15-minute window.

- Attempts `1` to `4`: login fails with invalid-credentials handling only
- Attempt `5` inside the window: a lockout event is created and the first lockout is applied

Lockout duration is based on `ConsecutiveLockoutCount`:

- `1` => `5 minutes`
- `2` => `15 minutes`
- `3+` => `30 minutes`

Consecutive lockouts reset only after a full successful sign-in or explicit admin re-enable.

## 24-Hour Disable Logic

Each applied lockout creates a `UserLockoutEvent`.

If a user reaches `5` lockout events within the rolling `24-hour` disable window:

- the account is automatically disabled
- the legacy user is set to `Status = Blocked`
- `IsActive = false`
- `UserLoginSecurityState.DisabledReason = RepeatedLockouts`

Once auto-disabled, the account stays disabled until explicit admin action re-enables it.

## API Response Behavior

Structured auth-failure responses use `AuthFailureResponseDTO`.

Fields:

- `ErrorCode`
- `Message`
- `LockoutEndUtc`
- `RemainingSeconds`
- `RetryAfterSeconds`
- `Disabled`

Response mapping:

- invalid credentials: `401`
- temporary lockout: `423`
- disabled account: `403`
- IP rate limited: `429`

Error codes:

- `InvalidCredentials`
- `TemporaryLockout`
- `AccountDisabled`
- `TooManyRequests`

The login endpoint keeps success responses unchanged. Failure responses are now structured JSON so the client can show lockout countdowns and other explicit states safely.

## UI Countdown Behavior

The login page now:

- shows distinct messages for invalid credentials, temporary lockout, disabled account, and rate limiting
- starts a visible countdown when the API returns a temporary lockout
- uses the server-provided lockout metadata when available
- updates the countdown every second
- blocks repeat submissions while the countdown is active
- automatically re-enables login attempts when the countdown expires

The countdown state is managed in the component and survives normal rerendering during the active lockout period.

## Audit Logging

The implementation writes audit events for:

- `AUTH-LOGIN-SUCCESS`
- `AUTH-LOGIN-FAILURE`
- `AUTH-RATE-LIMIT`
- `AUTH-LOCKOUT-APPLIED`
- `AUTH-LOCKOUT-BLOCKED`
- `AUTH-LOCKOUT-LEVEL-SELECTED`
- `AUTH-ACCOUNT-AUTO-DISABLED`
- `AUTH-ACCOUNT-DISABLED-ADMIN`
- `AUTH-ACCOUNT-ENABLED-ADMIN`
- `AUTH-LOGIN-BLOCKED-DISABLED`

Sensitive data such as passwords, MFA codes, raw secrets, and tokens are not logged.

## Manual Verification Checklist

- Confirm failed attempts `1` to `4` inside 15 minutes do not lock the account.
- Confirm the `5th` failed attempt inside 15 minutes applies a `5-minute` lockout.
- Confirm the next lockout without a fully successful sign-in escalates to `15 minutes`.
- Confirm the third and later consecutive lockouts escalate to `30 minutes`.
- Confirm a full successful sign-in resets failed-attempt history and consecutive escalation.
- Confirm MFA challenge issuance does not reset escalation and only final MFA success does.
- Confirm `5` lockout events inside `24 hours` automatically disable the account.
- Confirm an auto-disabled account cannot log in after the temporary lockout time has passed.
- Confirm superadmin enable/reactivate clears the disabled state and active temporary lockout.
- Confirm `/api/auth/login` is rate limited to `5` requests per minute per IP.
- Confirm rate-limited requests return structured `429` payloads with retry metadata.
- Confirm the login page shows a live countdown during temporary lockout and allows login again after expiry.
- Confirm the global superadmin user-management view distinguishes blocked/disabled users from temporarily locked users.
- Confirm audit logs are written for the expected success, failure, lockout, disable, enable, and rate-limit events.
