# Features Documentation

> A comprehensive guide to the AccSys accounting system features and capabilities.

---

## 1. System Architecture Overview

### 1.1 Architecture Pattern

AccountingSystem follows a **3-project layered architecture**:

- **AccountingSystem.Client**: Blazor WebAssembly SPA (presentation/UI layer)
- **AccountingSystem.Api**: ASP.NET Core Web API (application/service layer)
- **AccountingSystem.Shared**: Shared contracts (DTOs, enums) consumed by both client and API

### 1.2 Project Responsibilities

| Project | Responsibility |
|---------|----------------|
| **Client** | UI pages/components, route protection, role-aware navigation, client-side auth state, JWT token storage |
| **API** | Authentication, authorization, tenant isolation, business workflows, EF Core persistence, cross-cutting middleware |
| **Shared** | DTOs for request/response payloads, enums, JSON serialization settings |

### 1.3 Dependency Direction

```
AccountingSystem.Client → AccountingSystem.Shared
AccountingSystem.Api    → AccountingSystem.Shared
```

Client and API do **not** directly reference each other.

### 1.4 Technology Stack

| Layer | Technology |
|-------|------------|
| Frontend | Blazor WebAssembly (.NET 8), MudBlazor UI |
| Backend | ASP.NET Core 8 Web API |
| Database | Microsoft SQL Server 2022, EF Core 8 |
| Auth | JWT Bearer Tokens |
| PDF Generation | QuestPDF |
| Payment Gateway | PayMongo API |
| External Data | World Bank API, Frankfurter API |

---

## 2. Core Modules

### 2.1 General Ledger (GL) Module

**Purpose**: Manage the chart of accounts and process journal entries.

#### Features:
- **Chart of Accounts Management**
  - Create, update, archive, and restore accounts
  - Account types: Assets, Liabilities, Equity, Revenue, Expenses
  - Account code uniqueness per tenant
  
- **Journal Entry Processing**
  - Manual double-entry journal entries
  - Automatic debit/credit balancing validation
  - Journal entry line association with accounts
  
- **Trial Balance**
  - Real-time trial balance calculation
  - Debit/credit totals verification

- **Fiscal Year Management**
  - Fiscal year close functionality
  - Retained earnings account handling

### 2.2 Accounts Payable (AP) Module

**Purpose**: Manage vendors and track outgoing payments.

#### Features:
- **Vendor Management**
  - Create, update, archive, restore vendor records
  - Vendor contact information tracking
  
- **Bill Management**
  - Create vendor bills
  - Automatic ledger posting on bill creation
  - Bill status tracking (Open, Partially Paid, Paid)
  
- **Payment Recording**
  - Record outgoing payments against bills
  - Overpayment prevention validation
  - Payment history tracking

### 2.3 Accounts Receivable (AR) Module

**Purpose**: Manage customers and track incoming payments.

#### Features:
- **Customer Management**
  - Create, update, archive, restore customer records
  - Customer contact information tracking
  
- **Invoice Management**
  - Create customer invoices
  - Automatic ledger posting on invoice creation
  - Invoice status tracking
  - Invoice PDF generation and export
  
- **Payment Processing**
  - Manual payment receipt recording
  - PayMongo online payment integration
  - Payment callback handling

### 2.4 Financial Reporting Module

**Purpose**: Generate financial statements and KPI dashboards.

#### Features:
- **Dynamic Dashboard**
  - Key financial metrics and charts
  - Real-time data aggregation from GL/AP/AR
  
- **Financial Statements**
  - Statement of Financial Position (Balance Sheet)
  - Statement of Comprehensive Income (Income Statement)
  - Trial Balance report
  
- **Report Export**
  - PDF generation via QuestPDF
  - Download and print capabilities

### 2.5 User Management Module

**Purpose**: Manage tenant users and access control.

#### Features:
- **User Lifecycle Management**
  - Invite new users to tenant
  - Archive and restore users
  - View active and archived user lists
  
- **Role Assignment**
  - Assign tenant-scoped roles (TenantOwner, Accounting, Management)
  - Role-based feature access
  
- **User Status Tracking**
  - Active, Invited, Blocked status management

---

## 3. Frontend Features

### 3.1 Authentication Pages

#### Login (`/`)
- Email and password authentication
- MFA code entry (when enabled)
- Redirect to dashboard on success

#### Company Registration (`/register`)
- Self-service tenant onboarding
- Google reCAPTCHA verification
- Creates company + initial admin user
- Email confirmation workflow

#### MFA Login (`/mfa-login`)
- Second-factor authentication
- TOTP code or recovery code entry

### 3.2 Dashboard & Reports

#### Dashboard (`/dashboard`)
- Financial summary with key metrics
- Charts and visualizations
- Trial balance overview
- External data integration (inflation rates, exchange rates)

#### Financial Reports (`/reports/financials`)
- Trial balance context view
- PDF report generation
- Balance Sheet and Income Statement

### 3.3 General Ledger Pages

#### Chart of Accounts (`/gl/accounts`)
- View, create, update accounts
- Archive/restore functionality
- Filter and search capabilities

#### Journal Entries (`/gl/journal`)
- Create double-entry journal entries
- Add debit/credit lines
- Balance validation

### 3.4 Accounts Payable Pages

#### Vendors (`/ap/vendors`)
- Vendor list management
- Create, edit, archive vendors

#### Bills (`/ap/bills`) & Bill List (`/ap/bills/list`)
- Create new vendor bills
- Track bill status and payments
- Record outgoing payments

### 3.5 Accounts Receivable Pages

#### Customers (`/ar/customers`)
- Customer list management
- Create, edit, archive customers

#### Invoices (`/ar/invoices`) & Invoice List (`/ar/invoices/list`)
- Create customer invoices
- Track invoice status
- Download invoice PDFs

#### Receive Payment (`/ar/receive-payment`)
- Record manual payments
- PayMongo checkout integration

### 3.6 Administration Pages

#### User Management (`/admin/users`)
- View tenant user list
- Invite new users
- Archive/restore users

#### Audit Logs (`/admin/audit-logs`)
- View tenant activity history
- Security event tracking

#### Company Settings (`/admin/company-settings`)
- Update tenant profile information
- Configure company details

### 3.7 Super Admin Pages

#### System Dashboard (`/superadmin/dashboard`)
- Platform-wide KPIs and trends
- Recent admin actions

#### Tenant Manager (`/superadmin/tenants`)
- List all tenants
- Manage tenant status (active, suspended, blocked)

#### Global User Manager (`/superadmin/users`)
- View all platform users
- Manage user status across tenants

#### Admin Audit Logs (`/superadmin/audit-logs`)
- Platform admin action history
- Security event monitoring

---

## 4. User Invitation Flow

### 4.1 Invitation Process

1. **TenantOwner creates invitation**
   - Provides email, role, and optional name
   - No password collected at this stage

2. **System provisions user**
   - Creates user with `Status = "Invited"`
   - `IsActive = false`
   - Sends invitation email with confirmation link

3. **User confirms email**
   - Clicks confirmation link
   - Email is marked as confirmed
   - Redirects to password setup page

4. **User sets password**
   - Creates account password
   - Account activated only when both:
     - Email is confirmed
     - Password is set

5. **Activation completes**
   - Status changes to `Active`
   - `IsActive = true`
   - Normal login is allowed

### 4.2 Resend Invitation

- Available for users still in `Invited` status
- If email not confirmed: resends verification email
- If email confirmed but no password: sends password setup email
- Rejected if user already completed setup

---

## 5. Multi-Factor Authentication (MFA)

### 5.1 Supported Methods

- **TOTP-based MFA** using authenticator apps:
  - Google Authenticator
  - Any app supporting standard `otpauth://totp/...` URIs

### 5.2 Setup Process

1. Sign in to the application
2. Navigate to `/profile`
3. Open the Security tab
4. Choose "Set Up Authenticator"
5. Scan QR code with authenticator app
6. Enter 6-digit verification code
7. Save displayed recovery codes

### 5.3 Login with MFA

1. Enter email and password at `/`
2. If MFA enabled, redirected to `/mfa-login`
3. Enter either:
   - 6-digit TOTP code from authenticator, or
   - Recovery code (single-use)
4. On success, normal JWT token issued

### 5.4 MFA Management

Authenticated users can:
- View MFA status
- Reset authenticator key
- Regenerate recovery codes (invalidates previous)
- Disable MFA

### 5.5 Recovery Codes

- 10 codes generated when MFA is first enabled
- Each code is single-use
- Regenerating replaces all previous codes
- Only shown immediately after enable/regeneration

---

## 6. External Integrations

### 6.1 PayMongo Payment Gateway

**Purpose**: Process online payments for invoices.

**Flow**:
1. Create payment source via API
2. Redirect customer to PayMongo checkout
3. Webhook receives payment confirmation
4. Invoice marked as paid

### 6.2 World Bank API

**Purpose**: Retrieve Philippine inflation rate data for financial context.

### 6.3 Frankfurter API

**Purpose**: Live currency exchange rates for forex-aware reporting.

---

## 7. Database & Data Flow

### 7.1 Entity Relationships

```
Company (1) ←→ (*) User, Account, Vendor, Customer, Bill, Invoice, Payment
Role (1) ←→ (*) User
JournalEntry (1) ←→ (*) JournalEntryLine
JournalEntryLine (*) ←→ (1) Account
Vendor (1) ←→ (*) Bill
Customer (1) ←→ (*) Invoice
Payment → Invoice, Bill, Account (optional references)
```

### 7.2 Data Lifecycle

- **Create/Update**: Timestamps and tenant context set automatically
- **Delete**: Soft-delete for most entities (`IsDeleted=true`, `IsActive=false`)
- **Read**: Tenant-scoped and soft-delete filtered by default

### 7.3 Typical Transaction Flow

**Example: Create Invoice**
1. Client posts `CreateInvoiceDTO`
2. Controller calls `IReceivableService.CreateInvoiceAsync`
3. Service creates invoice and journal entry lines
4. DbContext saves invoice + ledger impacts
5. Trial balance reflects changes

**Example: Pay Bill**
1. Client posts `RecordPaymentDTO`
2. Service validates amount (no overpayment)
3. Bill status and amount paid updated
4. Payment record + journal entries created
5. Transaction committed

---

## 8. Setup & Deployment

### 8.1 Prerequisites

- Visual Studio 2022 or later
- .NET 8 SDK
- Microsoft SQL Server 2022
- SQL Server Management Studio (SSMS)
- Git
- Modern web browser (Chrome, Edge, Firefox)

### 8.2 Local Development Setup

```bash
# Clone repository
git clone https://github.com/dev-aziii/accsys.git
cd accsys

# Restore dependencies
dotnet restore AccountingSystem.sln

# Configure secrets (see docs/SECURITY.md)
cd AccountingSystem.Api
# Use user-secrets or environment variables

# Run API
dotnet run

# Run Client (new terminal)
cd ../AccountingSystem.Client
dotnet run
```

### 8.3 Access Points

- **API**: `https://localhost:7001`
- **Swagger UI**: `https://localhost:7001/swagger`
- **Client**: `https://localhost:7002`

### 8.4 Database Reset

```powershell
# From repo root
.\scripts\reset-dev-db.ps1

# Or using EF Core CLI
dotnet ef database drop --context AccountingDbContext --project AccountingSystem.Api/AccountingSystem.Api.csproj --startup-project AccountingSystem.Api/AccountingSystem.Api.csproj --force
```

After dropping, start the API to recreate and migrate the database.
