USE [A1Lands]
GO

/*
  Patch Inv. Due / Paid / Rcvable on contracts grid (via fn_BasicContractInfo →
  sp_GetActiveContractsAsOfDate).

  Inv. Due rules:
    1) Only finalized invoices (IsFinalized = 1)
    2) Exclude locked invoices (any edit row with IsLocked = 1)
    3) Per invoice amount:
         - if item lines exist (SubInvoiceNo not null/blank/0): SUM(TotalRent) of those lines
         - else: header AmountReceivable / TotalRent (null/blank SubInvoiceNo)
    4) Never sum header + lines together

  Inv. Paid (v5):
    Sum ReceiptLines for the contract where TIN-TRN / TIN-FTN is assigned
    (same as agreement-prov-invoice Received column). Not AHQ-finalized LinesJson LIKE.

  Apply the full function from:
    scripts/AlterFn_BasicContractInfo_CatC_BTS_PAFShare.sql

  Then refresh the contracts grid As-of-Date so ActiveByAsOfDate reloads due/paid/rcvable.
*/

PRINT 'Run AlterFn_BasicContractInfo_CatC_BTS_PAFShare.sql to apply Inv. Due/Paid amount fix.';
GO
