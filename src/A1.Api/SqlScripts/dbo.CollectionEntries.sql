/*
    CollectionEntries — income-agreements/collections grid persistence.
    Matches A1.Api.Models.CollectionEntry + ApplicationDbContext mapping.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'CollectionEntries'
)
BEGIN
    CREATE TABLE dbo.CollectionEntries (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ClassId INT NULL,
        ContractId INT NULL,
        ContractNo NVARCHAR(50) NULL,
        TenantBusiness NVARCHAR(300) NULL,
        CoaId INT NULL,
        InvoiceNo NVARCHAR(100) NULL,
        DueAmount DECIMAL(18, 2) NULL,
        BalanceAmount DECIMAL(18, 2) NULL,
        CollectionDate DATE NULL,
        Amount DECIMAL(18, 2) NULL,
        TinTrn NVARCHAR(100) NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    /* List screen: active rows, newest first (GET /api/Collections) */
    CREATE INDEX IX_CollectionEntries_IsDeleted_Id
        ON dbo.CollectionEntries (IsDeleted, Id DESC)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;

    /* Lookup / reporting by agreement */
    CREATE INDEX IX_CollectionEntries_ContractId_Active
        ON dbo.CollectionEntries (ContractId, Id DESC)
        INCLUDE (InvoiceNo, ContractNo, CollectionDate, Amount)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;

    /* Lookup by contract + invoice (natural business key) */
    CREATE INDEX IX_CollectionEntries_ContractNo_InvoiceNo_Active
        ON dbo.CollectionEntries (ContractNo, InvoiceNo)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;

    /* Filter by class */
    CREATE INDEX IX_CollectionEntries_ClassId_Active
        ON dbo.CollectionEntries (ClassId, Id DESC)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;

    /* Date-range reports */
    CREATE INDEX IX_CollectionEntries_CollectionDate_Active
        ON dbo.CollectionEntries (CollectionDate, Id DESC)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;
END;
GO
