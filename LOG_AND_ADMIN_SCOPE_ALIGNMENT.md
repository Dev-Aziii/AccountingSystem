# Log And Admin Scope Alignment

## Scope Model

- `SuperAdmin` is the only platform-scoped role.
- `TenantOwner` is the only tenant-admin role with audit-log visibility inside a tenant.
- `Accounting` and `Management` remain tenant-scoped business roles and do not gain audit-log access in this phase.
- Phase 6 does not introduce a platform-wide business audit viewer. The new platform security view is limited to auth and security events.

## Updated Visibility Rules

### Platform Admin

- `SuperAdmin` can access:
  - `api/superadmin/dashboard`
  - `api/superadmin/companies`
  - `api/superadmin/users`
  - `api/superadmin/audit-logs`
  - `api/superadmin/security-events`
- `api/superadmin/audit-logs` remains the platform admin action feed.
- `api/superadmin/security-events` exposes `AUTH-*` security events across the platform, except `AUTH-EMAIL-CONFIRMATION-BYPASS`, which remains stored but is intentionally hidden from the superadmin security-events view.
- Platform log views now surface a nullable structured `IpAddress`. Existing historical rows may show `Unavailable`.
- Platform log timestamps are rendered in Philippine local time (`UTC+08:00`) using 12-hour AM/PM formatting.

### Tenant Admin

- `TenantOwner` can access:
  - `api/audit-logs`
  - tenant user management
  - company settings
  - document numbering
  - fiscal year close
- `api/audit-logs` remains tenant-filtered and only returns logs for the current tenant.
- `api/audit-logs` excludes platform-originated records, including:
  - `SUPERADMIN-*` actions
  - `AUTH-*` rows tied to `SuperAdmin`
  - `AUTH-*` rows marked with `reason = "SuperAdminRole"`
  - `AUTH-*` rows sourced from `/api/superadmin/*`
- Tenant audit logs now surface `IpAddress` when available and continue to show tenant-only activity and security events.
- Tenant audit timestamps are rendered in Philippine local time (`UTC+08:00`) using 12-hour AM/PM formatting.

### Business Roles

- `Accounting` and `Management` keep their existing business and reporting access.
- `Accounting` and `Management` cannot access:
  - `api/audit-logs`
  - `api/superadmin/audit-logs`
  - `api/superadmin/security-events`
  - platform admin pages
- No new business-role reporting or analytics module is introduced.

## Affected Modules Reviewed

- Shared log DTOs for tenant audit logs and superadmin logs.
- Tenant audit API and tenant audit UI.
- Superadmin controller, service, and `/superadmin/audit-logs` page.
- Audit writers:
  - request audit middleware
  - auth security audit service
  - superadmin action audit writes
- Navigation visibility in the Blazor client.

## Capability Classification

| Capability | Classification | Role Scope |
| --- | --- | --- |
| Superadmin dashboard, tenant manager, global users | Platform admin | `SuperAdmin` |
| Platform admin audit logs | Platform admin | `SuperAdmin` |
| Platform security events (`AUTH-*`) | Platform admin | `SuperAdmin` |
| Tenant users, tenant audit logs, company settings | Tenant admin | `TenantOwner` |
| Document numbering, fiscal year close | Tenant admin | `TenantOwner` |
| Dashboard, financial statements | Business-role feature | `TenantOwner`, `Accounting`, `Management` |
| GL/AP/AR operational actions | Business-role feature | existing accounting or operational policies |

## Manual Verification Checklist

- Sign in as `SuperAdmin` and confirm the nav shows `Platform Logs`, not tenant audit links.
- Open `/superadmin/audit-logs` and confirm both tabs render:
  - `Platform Admin Actions`
  - `Platform Security Events`
- Confirm platform security events show only auth/security events and include tenant/user context.
- Confirm `AUTH-EMAIL-CONFIRMATION-BYPASS` does not appear in the platform security-events tab.
- Confirm new platform log entries show IP addresses; older rows may show `Unavailable`.
- Sign in as `TenantOwner` and confirm the nav shows `Tenant Audit Logs` and no platform log links.
- Open `/admin/audit-logs` and confirm only current-tenant activity is visible, with no `SUPERADMIN-*` or superadmin-originated `AUTH-*` rows.
- Confirm tenant audit rows show IP addresses when available.
- Confirm tenant and superadmin log timestamps render in PH local time with AM/PM.
- Sign in as `Accounting` and `Management` and confirm there is no audit-log navigation and direct audit-log routes are forbidden.
- Confirm no tenant role can access `/api/superadmin/audit-logs` or `/api/superadmin/security-events`.
- Confirm no tenant can see another tenant's logs.

## Notes

- Historical IP values are not backfilled.
- This phase aligns access scope and log visibility only. It does not add new analytics, reporting, or role types.
