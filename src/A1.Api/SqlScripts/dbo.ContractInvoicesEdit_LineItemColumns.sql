-- Run on A1Lands if line-item columns are missing from dbo.ContractInvoicesEdit.
-- Matches A1.Api.Models.ContractInvoicesEdit and agreement-prov-invoice UI columns.

IF COL_LENGTH('dbo.ContractInvoicesEdit', 'ItemwithCode') IS NULL
    ALTER TABLE dbo.ContractInvoicesEdit ADD ItemwithCode NVARCHAR(200) NULL;
GO

IF COL_LENGTH('dbo.ContractInvoicesEdit', 'Description') IS NULL
    ALTER TABLE dbo.ContractInvoicesEdit ADD Description NVARCHAR(500) NULL;
GO

IF COL_LENGTH('dbo.ContractInvoicesEdit', 'AccHead') IS NULL
    ALTER TABLE dbo.ContractInvoicesEdit ADD AccHead NVARCHAR(100) NULL;
GO

IF COL_LENGTH('dbo.ContractInvoicesEdit', 'Discount') IS NULL
    ALTER TABLE dbo.ContractInvoicesEdit ADD Discount INT NULL;
GO

IF COL_LENGTH('dbo.ContractInvoicesEdit', 'SortOrder') IS NULL
    ALTER TABLE dbo.ContractInvoicesEdit ADD SortOrder INT NULL;
GO
