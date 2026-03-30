# Tenant User Invitation Flow

## Input Model

Tenant user creation now uses `InviteTenantUserDTO`.

- Required:
  - `Email`
  - `RoleName`
- Optional:
  - `FirstName`
  - `LastName`

`FirstName` and `LastName` are combined into the existing `FullName` storage field. No password is collected during tenant user creation.

## Activation Flow

1. `TenantOwner` creates a user from tenant user management with email, role, and optional name fields.
2. The API creates the tenant-scoped legacy user with:
   - `Status = "Invited"`
   - `IsActive = false`
   - empty legacy password hash
   - tenant `CompanyId`
3. The matching Identity user is provisioned without a password, with:
   - `EmailConfirmed = false`
   - `RequireEmailConfirmation = true`
   - `Status = "Invited"`
   - `IsActive = false`
4. The system sends an invitation email with the email-confirmation link.
5. When the invited user confirms email:
   - the system confirms the Identity email
   - if no password exists yet, the response redirects the user to `/reset-password?...&flow=invite`
6. When the invited user sets a password:
   - the password is stored through the existing password reset pipeline
   - the account is activated only if the email is already confirmed
7. Activation completes only when both conditions are true:
   - email confirmed
   - usable password present
8. After activation:
   - legacy and Identity status move to `Active`
   - `IsActive = true`
   - normal login is allowed

If the invited user sets a password before confirming email, the password is accepted but the account remains `Invited` and inactive until email confirmation completes.

## Role Assignment Rules

- Tenant user invitations may assign only tenant-scoped roles:
  - `TenantOwner`
  - `Accounting`
  - `Management`
- `SuperAdmin` cannot be assigned from tenant user management.
- The invited user is always created inside the current tenant context and cannot escape tenant scope.

## Resend Behavior

Tenant user management includes `POST /api/users/{id}/resend-invite`.

Validation rules:

- caller must be a `TenantOwner`
- target user must belong to the same tenant
- target user must not be deleted
- target user must not be `SuperAdmin`
- target user must still be in `Invited` status

Send behavior:

- if email is not confirmed, resend the invitation / verification email
- if email is confirmed but setup is incomplete, send a password-setup email
- if the user has already completed setup, resend is rejected

## Manual Verification Checklist

- Create a tenant user with only email, role, and optional first/last name.
- Confirm the create form does not require a password.
- Confirm the new user appears in user management as `Pending Setup`.
- Confirm the invited user cannot sign in before activation completes.
- Open the invite email and confirm email.
- Confirm the browser is redirected into `/reset-password?...&flow=invite`.
- Set the password and confirm the account becomes active.
- Confirm the user can sign in only after both email confirmation and password setup complete.
- Resend the invite before confirmation and confirm another invitation email is sent.
- Resend the invite after confirmation but before password setup and confirm a password-setup email is sent.
- Confirm tenant user creation still rejects `SuperAdmin`.
- Confirm company self-registration still behaves as before.
- Confirm restoring an archived invited user returns them to `Pending Setup` and inactive.
