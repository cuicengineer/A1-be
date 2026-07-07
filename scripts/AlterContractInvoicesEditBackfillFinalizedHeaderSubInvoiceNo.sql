-- Backfill header invoice edits so sp_GetContractInvoiceSchedule can join on SubInvoiceNo = 1.
-- Run once after deploying the EnsureSubInvoiceNoWhenFinalizedAsync fix.

UPDATE dbo.ContractInvoicesEdit
SET SubInvoiceNo = '1'
WHERE IsFinalized = 1
  AND (SubInvoiceNo IS NULL OR LTRIM(RTRIM(SubInvoiceNo)) = '');
