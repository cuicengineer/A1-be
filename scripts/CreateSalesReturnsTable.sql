-- Sales Returns (Income Agreements module)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'SalesReturns' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.SalesReturns
    (
        Id                      INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Date]                  DATE           NULL,
        VrNo                    NVARCHAR(50)   NULL,
        ContractCustomerKey     NVARCHAR(100)  NULL,
        ContractCustomerLabel   NVARCHAR(500)  NULL,
        ContractId              INT            NULL,
        ContractNo              NVARCHAR(50)   NULL,
        CustomerId              INT            NULL,
        InvoiceKey              NVARCHAR(150)  NULL,
        InvoiceNo               NVARCHAR(50)   NULL,
        InvoiceLabel            NVARCHAR(300)  NULL,
        Description             NVARCHAR(500)  NULL,
        GrandTotal              DECIMAL(18,2)  NULL,
        LinesJson               NVARCHAR(MAX)  NULL,
        AttachmentsJson         NVARCHAR(MAX)  NULL,
        ActionDate              DATETIME2(3)   NULL,
        ActionBy                NVARCHAR(150)  NULL,
        Action                  NVARCHAR(50)   NULL,
        IsDeleted               BIT            NULL
    );

    CREATE INDEX IX_SalesReturns_Date ON dbo.SalesReturns ([Date]);
    CREATE INDEX IX_SalesReturns_ContractNo ON dbo.SalesReturns (ContractNo);
    CREATE INDEX IX_SalesReturns_InvoiceNo ON dbo.SalesReturns (InvoiceNo);
    CREATE INDEX IX_SalesReturns_IsDeleted ON dbo.SalesReturns (IsDeleted);
END
GO
