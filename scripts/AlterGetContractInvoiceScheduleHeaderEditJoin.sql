-- sp_GetContractInvoiceSchedule joins ContractInvoicesEdit with:
--   AND e.SubInvoiceNo = 1
--
-- Header rows finalized after the first invoice on a contract may have NULL SubInvoiceNo,
-- so IsFinalized is not surfaced even when the edit row is finalized.
--
-- Replace the join predicate with the block below (inside the LEFT OUTER JOIN to ContractInvoicesEdit):

/*
    LEFT OUTER JOIN dbo.ContractInvoicesEdit e
        ON e.ContractNo = f.ContractNo
       AND e.InvoiceNo = f.InvoiceNo
       AND ISNULL(e.InvoiceStatus,'') <> 'Deleted'
       AND ISNULL(e.IsDeleted,0) = 0
       AND (
            e.SubInvoiceNo = 1
            OR e.SubInvoiceNo = '1'
            OR (
                (e.SubInvoiceNo IS NULL OR LTRIM(RTRIM(e.SubInvoiceNo)) = '')
                AND ISNULL(e.IsFinalized, 0) = 1
            )
       )
*/
