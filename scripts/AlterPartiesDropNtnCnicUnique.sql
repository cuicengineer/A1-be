-- Allow duplicate NTN / CNIC on active Parties (dealers).
-- UI highlights duplicates; uniqueness is no longer enforced in the database.

USE [A1Lands]
GO

SET NOCOUNT ON;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Parties_NtnCnic_Active'
      AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    DROP INDEX UX_Parties_NtnCnic_Active ON dbo.Parties;
END
GO

-- Legacy name before Dealers -> Parties rename
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Dealers_NtnCnic_Active'
      AND object_id = OBJECT_ID(N'dbo.Dealers')
)
BEGIN
    DROP INDEX UX_Dealers_NtnCnic_Active ON dbo.Dealers;
END
GO

-- Non-unique lookup index (optional; helps filters without blocking duplicates)
IF OBJECT_ID(N'dbo.Parties', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Parties_NtnCnic_Active'
          AND object_id = OBJECT_ID(N'dbo.Parties')
    )
BEGIN
    CREATE NONCLUSTERED INDEX IX_Parties_NtnCnic_Active
        ON dbo.Parties (NtnCnic)
        WHERE ISNULL(IsDeleted, 0) = 0 AND Status = 1 AND NtnCnic IS NOT NULL;
END
GO
