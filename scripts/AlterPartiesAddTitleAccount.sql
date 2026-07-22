USE [A1Lands]
GO

IF COL_LENGTH('dbo.Parties', 'TitleAccount') IS NULL
BEGIN
    ALTER TABLE dbo.Parties
        ADD TitleAccount NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.Customers', 'TitleAccount') IS NULL
BEGIN
    ALTER TABLE dbo.Customers
        ADD TitleAccount NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.Suppliers', 'TitleAccount') IS NULL
BEGIN
    ALTER TABLE dbo.Suppliers
        ADD TitleAccount NVARCHAR(100) NULL;
END
GO
