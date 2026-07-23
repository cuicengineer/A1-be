/*
  Payments "Received In" was renamed to "Paid From" in the UI/API.

  Storage is unchanged:
    - dbo.Receipts.CashAndBankAccountId  = Cash & Bank ledger used as Paid From
    - dbo.Receipts.PaidFrom              = display/label snapshot (optional)

  PaidFromAccountDisplay is a NotMapped API field (no table column to rename).
  This script is intentionally a no-op so environments can run it safely after deploy.
*/

PRINT 'Payments Paid From rename: no database column changes required (CashAndBankAccountId / PaidFrom already in use).';
GO
