IF COL_LENGTH('dbo.CashAndBanks', 'Mode') IS NULL
BEGIN
    ALTER TABLE dbo.CashAndBanks
        ADD [Mode] NVARCHAR(20) NOT NULL CONSTRAINT DF_CashAndBanks_Mode DEFAULT ('Cash');
END
GO
