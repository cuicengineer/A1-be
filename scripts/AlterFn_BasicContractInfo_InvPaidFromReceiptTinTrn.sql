USE [A1Lands]
GO

/*
  Inv. Paid on /contracts grid (via fn_BasicContractInfo → sp_GetActiveContractsAsOfDate)

  Change:
    Inv. Paid = SUM of ReceiptLines for the contract where TIN-TRN / TIN-FTN is assigned
    and the line/receipt are not deleted — same source as agreement-prov-invoice
    "Received (TIN)" column.

  Amount per line:
    Total when Total <> 0, else Amount.

  Match:
    ReceiptLines.ContractNo = contract
    OR InvoiceKey starts with ContractNo|
    OR line InvoiceNo belongs to this contract in ContractInvoicesEdit

  Apply full function:
    scripts/AlterFn_BasicContractInfo_CatC_BTS_PAFShare.sql

  API also overlays this on GET /api/Contracts/ActiveByAsOfDate so the grid
  updates after API restart even before the SQL script is run.
*/

PRINT 'Run AlterFn_BasicContractInfo_CatC_BTS_PAFShare.sql for Inv. Paid = ReceiptLines TIN sum.';
GO
