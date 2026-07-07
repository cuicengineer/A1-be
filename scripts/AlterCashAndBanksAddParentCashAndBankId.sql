-- Parent-child hierarchy for TR mode Cash & Bank records
IF COL_LENGTH('dbo.CashAndBanks', 'ParentCashAndBankId') IS NULL
BEGIN
    ALTER TABLE dbo.CashAndBanks
        ADD ParentCashAndBankId INT NULL;

    ALTER TABLE dbo.CashAndBanks
        ADD CONSTRAINT FK_CashAndBanks_ParentCashAndBankId
            FOREIGN KEY (ParentCashAndBankId) REFERENCES dbo.CashAndBanks (Id);

    CREATE INDEX IX_CashAndBanks_ParentCashAndBankId
        ON dbo.CashAndBanks (ParentCashAndBankId);
END
GO

-- Child AcctId values can exceed the original 20-char limit (e.g. A01-01-27May2026)
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'CashAndBanks'
      AND s.name = N'dbo'
      AND c.name = N'AcctId'
      AND c.max_length < 100
)
BEGIN
    ALTER TABLE dbo.CashAndBanks
        ALTER COLUMN AcctId NVARCHAR(50) NULL;
END
GO
