IF OBJECT_ID(N'dbo.Tenants', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.Tenants')
         AND name = N'CoaId'
   )
BEGIN
    ALTER TABLE dbo.Tenants
        ADD CoaId INT NULL;
END
GO
