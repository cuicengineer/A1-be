IF COL_LENGTH('dbo.CollectionEntries', 'TenantNo') IS NULL
BEGIN
    ALTER TABLE dbo.CollectionEntries ADD TenantNo NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH('dbo.CollectionEntries', 'Status') IS NULL
BEGIN
    ALTER TABLE dbo.CollectionEntries ADD Status NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH('dbo.CollectionEntries', 'VrNo') IS NULL
BEGIN
    ALTER TABLE dbo.CollectionEntries ADD VrNo NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.CollectionEntries', 'VrDate') IS NULL
BEGIN
    ALTER TABLE dbo.CollectionEntries ADD VrDate DATE NULL;
END
GO

IF COL_LENGTH('dbo.CollectionEntries', 'ReceiptId') IS NULL
BEGIN
    ALTER TABLE dbo.CollectionEntries ADD ReceiptId INT NULL;
END
GO

IF COL_LENGTH('dbo.CollectionEntries', 'ReceivableAmount') IS NULL
BEGIN
    ALTER TABLE dbo.CollectionEntries ADD ReceivableAmount DECIMAL(18, 2) NULL;
END
GO
