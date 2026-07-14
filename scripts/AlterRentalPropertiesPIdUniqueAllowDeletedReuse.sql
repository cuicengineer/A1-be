-- Allow reusing RentalProperties.PId after soft-delete.
-- Replaces a full unique index/constraint on PId with a filtered unique index
-- that only applies to non-deleted rows.

SET NOCOUNT ON;

DECLARE @sql nvarchar(max);

-- Drop unique constraints that include PId
DECLARE constraint_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT kc.name
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID(N'dbo.RentalProperties')
  AND kc.type = 'UQ'
  AND c.name = N'PId';

OPEN constraint_cursor;
FETCH NEXT FROM constraint_cursor INTO @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC(N'ALTER TABLE dbo.RentalProperties DROP CONSTRAINT [' + @sql + N']');
    FETCH NEXT FROM constraint_cursor INTO @sql;
END
CLOSE constraint_cursor;
DEALLOCATE constraint_cursor;

-- Drop unique indexes that include PId (excluding PK)
DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT i.name
FROM sys.indexes i
INNER JOIN sys.index_columns ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(N'dbo.RentalProperties')
  AND i.is_unique = 1
  AND i.is_primary_key = 0
  AND i.name IS NOT NULL
  AND c.name = N'PId'
  AND i.has_filter = 0;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC(N'DROP INDEX [' + @sql + N'] ON dbo.RentalProperties');
    FETCH NEXT FROM index_cursor INTO @sql;
END
CLOSE index_cursor;
DEALLOCATE index_cursor;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_RentalProperties_PId_Active'
      AND object_id = OBJECT_ID(N'dbo.RentalProperties')
)
BEGIN
    CREATE UNIQUE INDEX UX_RentalProperties_PId_Active
        ON dbo.RentalProperties (PId)
        WHERE ISNULL(IsDeleted, 0) = 0 AND PId IS NOT NULL;
END
GO
