IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'Parties' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.Parties
    (
        Id             INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Prefix         NVARCHAR(20)   NULL,
        Rank           NVARCHAR(100)  NULL,
        Name           NVARCHAR(200)  NOT NULL,
        Address        NVARCHAR(500)  NULL,
        Province       NVARCHAR(50)   NULL,
        City           NVARCHAR(100)  NULL,
        NtnCnic        NVARCHAR(50)   NULL,
        GSTNo          NVARCHAR(50)   NULL,
        TelNo          NVARCHAR(50)   NULL,
        MobileNo       NVARCHAR(50)   NULL,
        Representative NVARCHAR(150)  NULL,
        BankListsId    INT            NULL,
        IBAN           NVARCHAR(34)   NULL,
        Status         BIT            NOT NULL CONSTRAINT DF_Parties_Status DEFAULT (1),
        ActionDate     DATETIME2(3)   NULL,
        ActionBy       NVARCHAR(150)  NULL,
        Action         NVARCHAR(50)   NULL,
        IsDeleted      BIT            NULL
    );

    CREATE INDEX IX_Parties_BankListsId ON dbo.Parties (BankListsId);
    CREATE INDEX IX_Parties_IsDeleted ON dbo.Parties (IsDeleted);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Parties_Name_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    CREATE UNIQUE INDEX UX_Parties_Name_Active
        ON dbo.Parties (Name)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Parties_NtnCnic_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Parties_NtnCnic_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    -- Non-unique: duplicate NTN / CNIC is allowed on active parties
    CREATE NONCLUSTERED INDEX IX_Parties_NtnCnic_Active
        ON dbo.Parties (NtnCnic)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1 AND NtnCnic IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Parties_IBAN_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    CREATE UNIQUE INDEX UX_Parties_IBAN_Active
        ON dbo.Parties (IBAN)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1 AND IBAN IS NOT NULL;
END
GO

IF COL_LENGTH('dbo.Customers', 'DealerId') IS NULL
BEGIN
    ALTER TABLE dbo.Customers
        ADD DealerId INT NULL;
END
GO

IF COL_LENGTH('dbo.Suppliers', 'DealerId') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers
        ADD DealerId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Customers_DealerId' AND object_id = OBJECT_ID(N'dbo.Customers')
)
BEGIN
    CREATE INDEX IX_Customers_DealerId ON dbo.Customers (DealerId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Suppliers_DealerId' AND object_id = OBJECT_ID(N'dbo.Suppliers')
)
BEGIN
    CREATE INDEX IX_Suppliers_DealerId ON dbo.Suppliers (DealerId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Customers_DealerId_Active' AND object_id = OBJECT_ID(N'dbo.Customers')
)
BEGIN
    CREATE UNIQUE INDEX UX_Customers_DealerId_Active
        ON dbo.Customers (DealerId)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1 AND DealerId IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Suppliers_DealerId_Active' AND object_id = OBJECT_ID(N'dbo.Suppliers')
)
BEGIN
    CREATE UNIQUE INDEX UX_Suppliers_DealerId_Active
        ON dbo.Suppliers (DealerId)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1 AND DealerId IS NOT NULL;
END
GO
