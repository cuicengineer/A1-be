-- Exchange rates (Accounts module)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'ExchangeRates' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.ExchangeRates
    (
        Id               INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RateDate         DATE           NOT NULL,
        BaseCurrencyId   INT            NOT NULL,
        ForeignCurrencyId INT           NOT NULL,
        Rate             DECIMAL(18,6)  NOT NULL,
        ActionDate       DATETIME2(3)   NULL,
        ActionBy         NVARCHAR(150)  NULL,
        Action           NVARCHAR(50)   NULL,
        IsDeleted        BIT            NULL
    );

    CREATE INDEX IX_ExchangeRates_RateDate ON dbo.ExchangeRates (RateDate);
    CREATE INDEX IX_ExchangeRates_BaseCurrencyId ON dbo.ExchangeRates (BaseCurrencyId);
    CREATE INDEX IX_ExchangeRates_ForeignCurrencyId ON dbo.ExchangeRates (ForeignCurrencyId);
    CREATE INDEX IX_ExchangeRates_IsDeleted ON dbo.ExchangeRates (IsDeleted);
END
GO
