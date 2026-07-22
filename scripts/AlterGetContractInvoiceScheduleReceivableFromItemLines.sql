USE [A1Lands]
GO

/*
    Agreement Prov Invoice grid: Receivable must equal Edit dialog "Total Amount"
    (sum of ContractInvoicesEdit item-line TotalRent for that ContractNo + InvoiceNo).

    Apply inside dbo.fn_GetContractInvoiceSchedule (final SELECT over Final f):

    1) Replace the et aggregate join so it sums item lines only, keyed by contract + invoice.
    2) Drive both TotalRent and AmountReceivable from that sum when present.
    3) Derive AmountPending from receivable - received when item lines exist.
*/

-- ============================================================================ in dbo.fn_GetContractInvoiceSchedule final SELECT:

-- OLD:
--    COALESCE(e.TotalRent, f.TotalRent) AS TotalRent,
--    ...
--    COALESCE(et.TotalRent, f.TotalRent) AS AmountReceivable,
--    COALESCE(e.AmountPending, f.Pending) AS AmountPending,
--
--    LEFT JOIN
--    (
--        SELECT
--            InvoiceNo,
--            SUM(TotalRent) AS TotalRent
--        FROM dbo.ContractInvoicesEdit
--        WHERE ISNULL(InvoiceStatus,'') <> 'Deleted'
--          AND IsDeleted = 0
--        GROUP BY InvoiceNo
--    ) et
--        ON et.InvoiceNo = f.InvoiceNo

-- NEW:
/*
    COALESCE(et.TotalRent, e.TotalRent, f.TotalRent) AS TotalRent,
    ...
    COALESCE(et.TotalRent, e.TotalRent, f.TotalRent) AS AmountReceivable,
    CASE
        WHEN et.TotalRent IS NOT NULL THEN
            CAST(ROUND(et.TotalRent - COALESCE(e.AmountReceived, rc.AmountReceived, f.AmountReceived, 0), 2) AS DECIMAL(18, 2))
        ELSE COALESCE(e.AmountPending, f.Pending)
    END AS AmountPending,

    LEFT JOIN
    (
        SELECT
            ContractNo,
            InvoiceNo,
            SUM(ISNULL(TotalRent, 0)) AS TotalRent
        FROM dbo.ContractInvoicesEdit
        WHERE ISNULL(InvoiceStatus, '') <> 'Deleted'
          AND ISNULL(IsDeleted, 0) = 0
          AND SubInvoiceNo IS NOT NULL
          AND LTRIM(RTRIM(CAST(SubInvoiceNo AS NVARCHAR(50)))) <> ''
          AND LTRIM(RTRIM(CAST(SubInvoiceNo AS NVARCHAR(50)))) <> '0'
        GROUP BY ContractNo, InvoiceNo
    ) et
        ON et.ContractNo = f.ContractNo
       AND et.InvoiceNo = f.InvoiceNo
*/

-- After editing the function definition, refresh with:
--   ALTER FUNCTION [dbo].[fn_GetContractInvoiceSchedule] ...
-- (use the full function body from scripts/subinvoiceno sp change.txt with the replacements above)

PRINT 'Review and apply the Receivable/TotalRent changes to dbo.fn_GetContractInvoiceSchedule as documented above.';
GO
