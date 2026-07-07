IF COL_LENGTH('dbo.Customers', 'CoaId2') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD [CoaId2] INT NULL;
END
GO

IF COL_LENGTH('dbo.Suppliers', 'CoaId2') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers ADD [CoaId2] INT NULL;
END
GO

IF COL_LENGTH('dbo.Tenants', 'CoaId2') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD [CoaId2] INT NULL;
END
GO
