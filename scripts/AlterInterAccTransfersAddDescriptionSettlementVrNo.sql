-- Add Description and SettlementVrNo to InterAccTransfers
IF COL_LENGTH('dbo.InterAccTransfers', 'Description') IS NULL
BEGIN
    ALTER TABLE dbo.InterAccTransfers
        ADD Description NVARCHAR(15) NULL;
END
GO

IF COL_LENGTH('dbo.InterAccTransfers', 'SettlementVrNo') IS NULL
BEGIN
    ALTER TABLE dbo.InterAccTransfers
        ADD SettlementVrNo NVARCHAR(50) NULL;
END
GO
