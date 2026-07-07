-- Products module tables: Services, Goods, UoM, Tax Codes
IF NOT EXISTS (SELECT 1 FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = N'ProductUoms' AND s.name = N'dbo')
BEGIN
    CREATE TABLE dbo.ProductUoms (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(20) NOT NULL,
        Name NVARCHAR(100) NULL,
        Status NVARCHAR(20) NULL,
        ActionDate DATETIME2(3) NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = N'TaxCodes' AND s.name = N'dbo')
BEGIN
    CREATE TABLE dbo.TaxCodes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL,
        Description NVARCHAR(200) NULL,
        Status NVARCHAR(20) NULL,
        ActionDate DATETIME2(3) NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = N'ProductServices' AND s.name = N'dbo')
BEGIN
    CREATE TABLE dbo.ProductServices (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ItemCode NVARCHAR(50) NOT NULL,
        ItemName NVARCHAR(200) NOT NULL,
        Uom NVARCHAR(20) NULL,
        SaleAccountCoaId INT NULL,
        SaleAccountIncomeStatementId INT NULL,
        PurchaseAccountCoaId INT NULL,
        PurchaseAccountIncomeStatementId INT NULL,
        DefaultParticulars NVARCHAR(500) NULL,
        DefaultUnitPriceSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        DefaultUnitPricePurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        TaxCodeId INT NULL,
        Status NVARCHAR(20) NULL,
        ActionDate DATETIME2(3) NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = N'ProductGoods' AND s.name = N'dbo')
BEGIN
    CREATE TABLE dbo.ProductGoods (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ItemCode NVARCHAR(50) NOT NULL,
        ItemName NVARCHAR(200) NOT NULL,
        Uom NVARCHAR(20) NULL,
        ControlAccountCoaId INT NULL,
        SaleAccountIncomeStatementId INT NULL,
        PurchaseAccountIncomeStatementId INT NULL,
        DefaultParticulars NVARCHAR(500) NULL,
        DefaultUnitPriceSales DECIMAL(18,2) NOT NULL DEFAULT 0,
        DefaultUnitPricePurchase DECIMAL(18,2) NOT NULL DEFAULT 0,
        TaxCodeId INT NULL,
        Status NVARCHAR(20) NULL,
        ActionDate DATETIME2(3) NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );
END
GO
