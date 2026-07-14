-- Guidelines (top-level menu: document attachments for users)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'Guidelines' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.Guidelines
    (
        Id          INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Title       NVARCHAR(200)  NOT NULL,
        Description NVARCHAR(500)  NULL,
        Status      BIT            NOT NULL CONSTRAINT DF_Guidelines_Status DEFAULT (1),
        ActionDate  DATETIME2(3)   NULL,
        ActionBy    NVARCHAR(150)  NULL,
        Action      NVARCHAR(50)   NULL,
        IsDeleted   BIT            NULL
    );

    CREATE INDEX IX_Guidelines_Title ON dbo.Guidelines (Title);
    CREATE INDEX IX_Guidelines_IsDeleted ON dbo.Guidelines (IsDeleted);
END
GO
