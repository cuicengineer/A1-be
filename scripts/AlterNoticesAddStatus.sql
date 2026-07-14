-- Add Status (Active/Inactive) to Notices for login popup control
IF EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'Notices' AND s.name = N'dbo'
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'Notices' AND s.name = N'dbo' AND c.name = N'Status'
)
BEGIN
    ALTER TABLE dbo.Notices
        ADD Status BIT NOT NULL CONSTRAINT DF_Notices_Status DEFAULT (1);
END
GO
