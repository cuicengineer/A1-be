USE [A1Lands]
GO

/*
    Prevent duplicate ContractInvoicesEdit rows for the same
    ContractNo + InvoiceNo + SubInvoiceNo (active rows only).

    NOTE: SQL Server filtered indexes do not allow OR in the filter predicate.
    Empty SubInvoiceNo values are normalized to NULL so the header unique index
    can use: WHERE IsDeleted = 0 AND SubInvoiceNo IS NULL
*/

SET NOCOUNT ON;

-- Normalize blank SubInvoiceNo to NULL (required for header unique index)
UPDATE dbo.ContractInvoicesEdit
SET SubInvoiceNo = NULL
WHERE SubInvoiceNo IS NOT NULL
  AND LTRIM(RTRIM(SubInvoiceNo)) = N'';

PRINT CONCAT('Normalized blank SubInvoiceNo to NULL: ', @@ROWCOUNT);

;WITH Ranked AS
(
    SELECT
        Id,
        ROW_NUMBER() OVER
        (
            PARTITION BY
                ContractNo,
                InvoiceNo,
                CASE
                    WHEN SubInvoiceNo IS NULL OR LTRIM(RTRIM(SubInvoiceNo)) = N''
                        THEN N'__HEADER__'
                    ELSE LTRIM(RTRIM(SubInvoiceNo))
                END
            ORDER BY
                CASE WHEN ISNULL(IsFinalized, 0) = 1 THEN 0 ELSE 1 END,
                Id DESC
        ) AS rn
    FROM dbo.ContractInvoicesEdit
    WHERE ISNULL(IsDeleted, 0) = 0
)
UPDATE e
SET
    e.IsDeleted = 1,
    e.IsFinalized = 0,
    e.Action = N'DELETE',
    e.ActionDate = GETUTCDATE(),
    e.ActionBy = N'system:dedupe-subinvoice'
FROM dbo.ContractInvoicesEdit e
INNER JOIN Ranked r ON r.Id = e.Id
WHERE r.rn > 1;

PRINT CONCAT('Soft-deleted duplicate SubInvoiceNo rows: ', @@ROWCOUNT);
GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ContractInvoicesEdit_ContractNo_InvoiceNo_SubInvoiceNo'
      AND object_id = OBJECT_ID(N'dbo.ContractInvoicesEdit')
)
BEGIN
    DROP INDEX IX_ContractInvoicesEdit_ContractNo_InvoiceNo_SubInvoiceNo
        ON dbo.ContractInvoicesEdit;
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_ContractInvoicesEdit_Contract_Invoice_Sub_Active'
      AND object_id = OBJECT_ID(N'dbo.ContractInvoicesEdit')
)
BEGIN
    CREATE UNIQUE INDEX UX_ContractInvoicesEdit_Contract_Invoice_Sub_Active
        ON dbo.ContractInvoicesEdit (ContractNo, InvoiceNo, SubInvoiceNo)
        WHERE IsDeleted = 0
          AND SubInvoiceNo IS NOT NULL
          AND SubInvoiceNo <> N'';
END
GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_ContractInvoicesEdit_Contract_Invoice_Header_Active'
      AND object_id = OBJECT_ID(N'dbo.ContractInvoicesEdit')
)
BEGIN
    DROP INDEX UX_ContractInvoicesEdit_Contract_Invoice_Header_Active
        ON dbo.ContractInvoicesEdit;
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_ContractInvoicesEdit_Contract_Invoice_Header_Active'
      AND object_id = OBJECT_ID(N'dbo.ContractInvoicesEdit')
)
BEGIN
    -- No OR here — blanks were normalized to NULL above.
    CREATE UNIQUE INDEX UX_ContractInvoicesEdit_Contract_Invoice_Header_Active
        ON dbo.ContractInvoicesEdit (ContractNo, InvoiceNo)
        WHERE IsDeleted = 0
          AND SubInvoiceNo IS NULL;
END
GO

PRINT 'Unique SubInvoiceNo indexes applied.';
GO
