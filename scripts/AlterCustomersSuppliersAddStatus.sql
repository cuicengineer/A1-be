IF COL_LENGTH('dbo.Customers', 'Status') IS NULL
BEGIN
    ALTER TABLE dbo.Customers
        ADD [Status] BIT NOT NULL CONSTRAINT DF_Customers_Status DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.Suppliers', 'Status') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers
        ADD [Status] BIT NOT NULL CONSTRAINT DF_Suppliers_Status DEFAULT (1);
END
GO
