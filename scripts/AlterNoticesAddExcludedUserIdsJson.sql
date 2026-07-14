-- Add ExcludedUserIdsJson to Notices (users who skip login popup)
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
    WHERE t.name = N'Notices' AND s.name = N'dbo' AND c.name = N'ExcludedUserIdsJson'
)
BEGIN
    ALTER TABLE dbo.Notices
        ADD ExcludedUserIdsJson NVARCHAR(200) NULL;
END
GO
