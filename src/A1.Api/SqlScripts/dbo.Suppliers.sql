IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'Suppliers'
)
BEGIN
    CREATE TABLE dbo.Suppliers (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL,
        Prefix NVARCHAR(20) NULL,
        Rank NVARCHAR(100) NULL,
        Name NVARCHAR(200) NOT NULL,
        Address NVARCHAR(500) NULL,
        Province NVARCHAR(50) NULL,
        City NVARCHAR(100) NULL,
        NtnCnic NVARCHAR(50) NULL,
        GSTNo NVARCHAR(50) NULL,
        TelNo NVARCHAR(50) NULL,
        MobileNo NVARCHAR(50) NULL,
        CoaId INT NULL,
        Representative NVARCHAR(150) NULL,
        BankListsId INT NULL,
        IBAN NVARCHAR(34) NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE UNIQUE INDEX IX_Suppliers_Code_Active
        ON dbo.Suppliers (Code)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'SupplierRanks'
)
BEGIN
    CREATE TABLE dbo.SupplierRanks (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RankName NVARCHAR(100) NOT NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE UNIQUE INDEX IX_SupplierRanks_RankName_Active
        ON dbo.SupplierRanks (RankName)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;
END;
GO

IF COL_LENGTH('dbo.Suppliers', 'BankListsId') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers ADD BankListsId INT NULL;
END;
GO

IF COL_LENGTH('dbo.Suppliers', 'IBAN') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers ADD IBAN NVARCHAR(34) NULL;
END;
GO
