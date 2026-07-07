-- Cash & Bank ledger accounts (Cash & Fund Flow module)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'CashAndBanks' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.CashAndBanks
    (
        Id            INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AcctId        NVARCHAR(50)   NULL,
        Name          NVARCHAR(200)  NOT NULL,
        CoaId         INT            NULL,
        Currency      NVARCHAR(50)   NOT NULL,
        Mode          NVARCHAR(20)   NOT NULL CONSTRAINT DF_CashAndBanks_Mode DEFAULT ('Cash'),
        IBAN          NVARCHAR(34)   NULL,
        BankListsId   INT            NULL,
        Status        NVARCHAR(20)   NULL,
        ParentCashAndBankId INT      NULL,
        ActionDate    DATETIME2(3)   NULL,
        ActionBy      NVARCHAR(150)  NULL,
        Action        NVARCHAR(50)   NULL,
        IsDeleted     BIT            NULL
    );

    CREATE INDEX IX_CashAndBanks_CoaId ON dbo.CashAndBanks (CoaId);
    CREATE INDEX IX_CashAndBanks_BankListsId ON dbo.CashAndBanks (BankListsId);
    CREATE INDEX IX_CashAndBanks_ParentCashAndBankId ON dbo.CashAndBanks (ParentCashAndBankId);
    CREATE INDEX IX_CashAndBanks_IsDeleted ON dbo.CashAndBanks (IsDeleted);
END
GO
