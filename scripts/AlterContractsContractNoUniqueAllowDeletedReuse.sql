-- Allow reusing Contracts.ContractNo after soft-delete.
-- Replaces a full unique index/constraint on ContractNo with a filtered unique index
-- that only applies to non-deleted rows.

SET NOCOUNT ON;

DECLARE @sql nvarchar(max);

-- Drop unique constraints that include ContractNo
DECLARE constraint_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT kc.name
FROM sys.key_constraints kc
INNER JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID(N'dbo.Contracts')
  AND kc.type = 'UQ'
  AND c.name = N'ContractNo';

OPEN constraint_cursor;
FETCH NEXT FROM constraint_cursor INTO @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC(N'ALTER TABLE dbo.Contracts DROP CONSTRAINT [' + @sql + N']');
    FETCH NEXT FROM constraint_cursor INTO @sql;
END
CLOSE constraint_cursor;
DEALLOCATE constraint_cursor;

-- Drop unique indexes that include ContractNo (excluding PK)
DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT i.name
FROM sys.indexes i
INNER JOIN sys.index_columns ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns c
    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(N'dbo.Contracts')
  AND i.is_unique = 1
  AND i.is_primary_key = 0
  AND i.name IS NOT NULL
  AND c.name = N'ContractNo'
  AND i.has_filter = 0;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC(N'DROP INDEX [' + @sql + N'] ON dbo.Contracts');
    FETCH NEXT FROM index_cursor INTO @sql;
END
CLOSE index_cursor;
DEALLOCATE index_cursor;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Contracts_ContractNo_Active'
      AND object_id = OBJECT_ID(N'dbo.Contracts')
)
BEGIN
    CREATE UNIQUE INDEX UX_Contracts_ContractNo_Active
        ON dbo.Contracts (ContractNo)
        WHERE ISNULL(IsDeleted, 0) = 0 AND ContractNo IS NOT NULL;
END
GO
