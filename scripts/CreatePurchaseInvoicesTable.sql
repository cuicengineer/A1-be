IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'PurchaseInvoices' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.PurchaseInvoices
    (
        Id          INT             IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Date]      DATE            NULL,
        PiNo        NVARCHAR(50)    NULL,
        Description NVARCHAR(500)   NULL,
        GrandTotal  DECIMAL(18,2)   NULL,
        LinesJson   NVARCHAR(MAX)   NULL,
        ActionDate  DATETIME2(3)    NULL,
        ActionBy    NVARCHAR(150)   NULL,
        Action      NVARCHAR(50)    NULL,
        IsDeleted   BIT             NULL
    );

    CREATE INDEX IX_PurchaseInvoices_IsDeleted ON dbo.PurchaseInvoices (IsDeleted);
    CREATE INDEX IX_PurchaseInvoices_Date ON dbo.PurchaseInvoices ([Date]);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_PurchaseInvoices_PiNo_Active' AND object_id = OBJECT_ID(N'dbo.PurchaseInvoices')
)
BEGIN
    -- Filtered index predicates cannot use functions (LTRIM/RTRIM). App normalizes PiNo on save.
    CREATE UNIQUE INDEX UX_PurchaseInvoices_PiNo_Active
    ON dbo.PurchaseInvoices (PiNo)
    WHERE IsDeleted = 0 AND PiNo IS NOT NULL AND PiNo <> N'';
END
GO
