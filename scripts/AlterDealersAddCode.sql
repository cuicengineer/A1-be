DECLARE @partyTable SYSNAME =
    CASE
        WHEN OBJECT_ID(N'dbo.Parties', N'U') IS NOT NULL THEN N'dbo.Parties'
        WHEN OBJECT_ID(N'dbo.Dealers', N'U') IS NOT NULL THEN N'dbo.Dealers'
        ELSE NULL
    END;

IF @partyTable IS NOT NULL
   AND COL_LENGTH(@partyTable, 'Code') IS NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'ALTER TABLE ' + @partyTable + N' ADD Code NVARCHAR(30) NULL;';
    EXEC sp_executesql @sql;
END
GO

IF OBJECT_ID(N'dbo.Parties', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'UX_Parties_Code_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
   )
BEGIN
    CREATE UNIQUE INDEX UX_Parties_Code_Active
        ON dbo.Parties (Code)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1 AND Code IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.Dealers', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Parties', N'U') IS NULL
   AND NOT EXISTS (
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
