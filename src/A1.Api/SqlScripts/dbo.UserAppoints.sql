IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'UserAppoints'
)
BEGIN
    CREATE TABLE dbo.UserAppoints (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Status TINYINT NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );
END;
GO

INSERT INTO dbo.UserAppoints (Name, Status, IsDeleted, ActionDate, Action)
SELECT DISTINCT
    LTRIM(RTRIM(u.Appoint)) AS Name,
    1 AS Status,
    0 AS IsDeleted,
    GETUTCDATE() AS ActionDate,
    'SEED' AS Action
FROM dbo.Users u
WHERE u.Appoint IS NOT NULL
  AND LTRIM(RTRIM(u.Appoint)) <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.UserAppoints ua
      WHERE (ua.IsDeleted IS NULL OR ua.IsDeleted = 0)
        AND LTRIM(RTRIM(ua.Name)) = LTRIM(RTRIM(u.Appoint))
  );
GO
