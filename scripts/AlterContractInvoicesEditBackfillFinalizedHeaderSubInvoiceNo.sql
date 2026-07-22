-- Backfill header invoice edits so sp_GetContractInvoiceSchedule can join on SubInvoiceNo = 1.
-- Safe version: only promote empty headers when SubInvoiceNo = '1' does not already exist.

UPDATE h
SET h.SubInvoiceNo = N'1'
FROM dbo.ContractInvoicesEdit h
WHERE ISNULL(h.IsFinalized, 0) = 1
  AND ISNULL(h.IsDeleted, 0) = 0
  AND (h.SubInvoiceNo IS NULL OR LTRIM(RTRIM(h.SubInvoiceNo)) = N'')
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.ContractInvoicesEdit x
      WHERE x.ContractNo = h.ContractNo
        AND x.InvoiceNo = h.InvoiceNo
        AND ISNULL(x.IsDeleted, 0) = 0
        AND LTRIM(RTRIM(ISNULL(x.SubInvoiceNo, N''))) = N'1'
  );
