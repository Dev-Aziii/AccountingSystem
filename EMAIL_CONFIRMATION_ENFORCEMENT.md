# Email Confirmation Enforcement

## Rule

All users must have a confirmed Identity email before the API will issue a login token.

The login flow now checks the linked `ApplicationUser` with `UserManager.IsEmailConfirmedAsync(...)` after credentials are validated and before JWT issuance. If the email is not confirmed, login is denied with the same generic unauthorized response used for other invalid credential failures.

## SuperAdmin Exemption

Accounts whose effective role is `SuperAdmin` are exempt from the email-confirmation login requirement.

The exemption is role-based. The current codebase identifies the bootstrap or seeded administrator path through the `SuperAdmin` role name in the legacy user record, and that role is treated as the reliable bypass signal during login.

## Registration And Resend Behavior

Normal users are still created with `EmailConfirmed = false` and receive a confirmation email. The resend endpoint remains non-enumerating and can provision a legacy-only account into Identity before sending a confirmation email when needed.
