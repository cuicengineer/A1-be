-- Extend Receipts table for Payment records (shared with Receipts)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Receipts') AND name = N'RecordType'
)
BEGIN
    ALTER TABLE dbo.Receipts ADD RecordType NVARCHAR(20) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Receipts') AND name = N'VrNo'
)
BEGIN
    ALTER TABLE dbo.Receipts ADD VrNo NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Receipts') AND name = N'CashAndBankAccountId'
)
BEGIN
    ALTER TABLE dbo.Receipts ADD CashAndBankAccountId INT NULL;
END
GO

-- Existing rows without RecordType are treated as Receipts by the API.
