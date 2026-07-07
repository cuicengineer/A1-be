-- Purchase Returns (Purchases module)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'PurchaseReturns' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.PurchaseReturns
    (
        Id                      INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Date]                  DATE           NULL,
        VrNo                    NVARCHAR(50)   NULL,
        SupplierKey             NVARCHAR(100)  NULL,
        SupplierLabel           NVARCHAR(500)  NULL,
        SupplierId              INT            NULL,
        SupplierCode            NVARCHAR(50)   NULL,
        PurchaseInvoiceNo       NVARCHAR(100)  NULL,
        PurchaseInvoiceLabel    NVARCHAR(300)  NULL,
        Description             NVARCHAR(500)  NULL,
        GrandTotal              DECIMAL(18,2)  NULL,
        LinesJson               NVARCHAR(MAX)  NULL,
        ActionDate              DATETIME2(3)   NULL,
        ActionBy                NVARCHAR(150)  NULL,
        Action                  NVARCHAR(50)   NULL,
        IsDeleted               BIT            NULL
    );

    CREATE INDEX IX_PurchaseReturns_Date ON dbo.PurchaseReturns ([Date]);
    CREATE INDEX IX_PurchaseReturns_SupplierId ON dbo.PurchaseReturns (SupplierId);
    CREATE INDEX IX_PurchaseReturns_PurchaseInvoiceNo ON dbo.PurchaseReturns (PurchaseInvoiceNo);
    CREATE INDEX IX_PurchaseReturns_IsDeleted ON dbo.PurchaseReturns (IsDeleted);
END
GO
