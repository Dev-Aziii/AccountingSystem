# Role Assignment Rules

## Assignment Matrix

Current canonical roles:

- `SuperAdmin`
- `TenantOwner`
- `Accounting`
- `Management`

Who can assign which roles:

| Actor | Assignable roles |
| --- | --- |
| `SuperAdmin` | `SuperAdmin`, `TenantOwner`, `Accounting`, `Management` |
| `TenantOwner` | `Accounting`, `Management` |
| `Accounting` | none |
| `Management` | none |

## Prohibited Assignments

- `TenantOwner` cannot assign `SuperAdmin`.
- `TenantOwner` cannot assign another `TenantOwner`.
- `Accounting` cannot assign any role.
- `Management` cannot assign any role.
- Tenant-scoped user management cannot operate on `SuperAdmin` accounts.
- Tenant-scoped user management cannot archive, restore, or resend invites for `TenantOwner` accounts.

## Reviewed Assignment Surfaces

- `POST /api/users`
  - tenant invite creation now validates the current actor role and tenant scope server-side
  - `TenantOwner` may invite only `Accounting` or `Management`
- `POST /api/users/{id}/resend-invite`
  - tenant-scoped resend is blocked for `SuperAdmin` and `TenantOwner` targets
- `DELETE /api/users/{id}`
  - tenant-scoped archive is blocked for `SuperAdmin` and `TenantOwner` targets
- `PUT /api/users/{id}/restore`
  - tenant-scoped restore is blocked for `SuperAdmin` and `TenantOwner` targets
- `SuperAdminController`
  - reviewed for escalation risk
  - remains platform status-management only in this phase
  - no new role-edit endpoint was added
- Tenant user management UI
  - invite role options now come from the shared assignment matrix instead of a hardcoded role list

## Manual Verification Steps

1. Sign in as `TenantOwner` and open `/admin/users`.
2. Confirm the invite form only offers `Accounting` and `Management`.
3. Submit a forged request to `POST /api/users` with `RoleName = "SuperAdmin"` and confirm the API rejects it.
4. Submit a forged request to `POST /api/users` with `RoleName = "TenantOwner"` and confirm the API rejects it.
5. Confirm valid `Accounting` and `Management` invites still succeed for the current tenant.
6. Attempt tenant-side archive, restore, or resend-invite operations against a `TenantOwner` user and confirm they are rejected.
7. Attempt tenant-side management of a user from another tenant and confirm the endpoint does not act on the record.
8. Confirm `SuperAdmin` platform status-management screens still work across tenants.
