# Project Documentation

## 11. Fiscal Year & Closing Process

- Fiscal years are tenant-scoped records (`FiscalYear`) with start/end boundaries and closure metadata.
- Closing moves all Revenue and Expense balances into Retained Earnings (`3100`) via a system-generated, balanced journal entry (`IsSystemGenerated = true`, `Reference = FY-CLOSE-*`).
- Carry-forward includes only Asset, Liability, and Equity balances.
- Opening balances are posted on the first day of the next year using `FY-OPEN-*` system entries.

### Example closing journal entry
- Date: 2025-12-31
- Dr Service Revenue 120,000
- Cr Salaries Expense 70,000
- Cr Retained Earnings 50,000

### Example opening balance entry
- Date: 2026-01-01
- Dr Cash 90,000
- Dr Accounts Receivable 20,000
- Cr Accounts Payable 15,000
- Cr Retained Earnings 95,000
