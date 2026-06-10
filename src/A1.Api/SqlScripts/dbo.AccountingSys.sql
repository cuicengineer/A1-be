IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'AccountingSys'
)
BEGIN
    CREATE TABLE dbo.AccountingSys (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ParticularName NVARCHAR(200) NOT NULL,
        Address NVARCHAR(500) NULL,
        TelNo NVARCHAR(50) NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );
END;
GO
