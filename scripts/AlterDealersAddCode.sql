IF COL_LENGTH('dbo.Dealers', 'Code') IS NULL
BEGIN
    ALTER TABLE dbo.Dealers
        ADD Code NVARCHAR(30) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Dealers_Code_Active' AND object_id = OBJECT_ID(N'dbo.Dealers')
)
BEGIN
    CREATE UNIQUE INDEX UX_Dealers_Code_Active
        ON dbo.Dealers (Code)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1 AND Code IS NOT NULL;
END
GO
