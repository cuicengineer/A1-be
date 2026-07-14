-- System Notice (singleton rich-text message shown after login)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'Notices' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.Notices
    (
        Id                   INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ContentHtml          NVARCHAR(MAX)  NOT NULL,
        Status               BIT            NOT NULL CONSTRAINT DF_Notices_Status DEFAULT (1),
        ExcludedUserIdsJson  NVARCHAR(200)  NULL,
        ActionDate           DATETIME2(3)   NULL,
        ActionBy             NVARCHAR(150)  NULL,
        Action               NVARCHAR(50)   NULL,
        IsDeleted            BIT            NULL
    );

    CREATE INDEX IX_Notices_IsDeleted ON dbo.Notices (IsDeleted);
END
GO
