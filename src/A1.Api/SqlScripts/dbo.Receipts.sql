/*
    Receipts — cash & fund flow /receipts grid persistence.
    Matches A1.Api.Models.Receipt + ApplicationDbContext mapping.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'Receipts'
)
BEGIN
    CREATE TABLE dbo.Receipts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Date] DATE NULL,
        [Month] NVARCHAR(10) NULL,
        ReferenceAutomatic BIT NULL,
        Reference NVARCHAR(100) NULL,
        PaidFrom NVARCHAR(150) NULL,
        PayeeContactType NVARCHAR(50) NULL,
        PayeeName NVARCHAR(300) NULL,
        Description NVARCHAR(500) NULL,
        GrandTotal DECIMAL(18, 2) NULL,
        LinesJson NVARCHAR(MAX) NULL,
        AttachmentsJson NVARCHAR(MAX) NULL,
        FinalizedByAhq BIT NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE INDEX IX_Receipts_IsDeleted_Id
        ON dbo.Receipts (IsDeleted, Id DESC)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;

    CREATE INDEX IX_Receipts_Month_Active
        ON dbo.Receipts ([Month], Id DESC)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;

    CREATE INDEX IX_Receipts_Date_Active
        ON dbo.Receipts ([Date], Id DESC)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;
END;
GO

IF COL_LENGTH('dbo.Receipts', 'Month') IS NULL
BEGIN
    ALTER TABLE dbo.Receipts ADD [Month] NVARCHAR(10) NULL;
END;
GO

IF COL_LENGTH('dbo.Receipts', 'FinalizedByAhq') IS NULL
BEGIN
    ALTER TABLE dbo.Receipts ADD FinalizedByAhq BIT NULL;
END;
GO
