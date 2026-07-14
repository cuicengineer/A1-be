-- Journal Entries (Cash & Fund Flow module)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'JournalEntries' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.JournalEntries
    (
        Id              INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EntryDate       DATE           NOT NULL,
        VrNo            NVARCHAR(50)   NOT NULL,
        Description     NVARCHAR(50)   NULL,
        TotalDebit      DECIMAL(18,2)  NOT NULL CONSTRAINT DF_JournalEntries_TotalDebit DEFAULT (0),
        TotalCredit     DECIMAL(18,2)  NOT NULL CONSTRAINT DF_JournalEntries_TotalCredit DEFAULT (0),
        LinesJson       NVARCHAR(MAX)  NULL,
        AttachmentsJson NVARCHAR(MAX)  NULL,
        ActionDate      DATETIME2(3)   NULL,
        ActionBy        NVARCHAR(150)  NULL,
        Action          NVARCHAR(50)   NULL,
        IsDeleted       BIT            NULL,
        IsLock          BIT            NOT NULL CONSTRAINT DF_JournalEntries_IsLock DEFAULT (0)
    );

    CREATE INDEX IX_JournalEntries_EntryDate ON dbo.JournalEntries (EntryDate);
    CREATE INDEX IX_JournalEntries_VrNo ON dbo.JournalEntries (VrNo);
    CREATE INDEX IX_JournalEntries_IsDeleted ON dbo.JournalEntries (IsDeleted);
END
GO
