IF COL_LENGTH('dbo.Contracts', 'PaymentTiming') IS NULL
BEGIN
    ALTER TABLE dbo.Contracts
    ADD PaymentTiming NVARCHAR(20) NULL;
END
GO
