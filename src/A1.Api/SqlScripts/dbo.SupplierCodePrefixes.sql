IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'SupplierCodePrefixes'
)
BEGIN
    CREATE TABLE dbo.SupplierCodePrefixes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PrefixAlpha NVARCHAR(20) NOT NULL,
        Description NVARCHAR(500) NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE UNIQUE INDEX IX_SupplierCodePrefixes_PrefixAlpha_Active
        ON dbo.SupplierCodePrefixes (PrefixAlpha)
        WHERE IsDeleted = 0 OR IsDeleted IS NULL;
END;
GO

IF COL_LENGTH('dbo.SupplierCodePrefixes', 'Description') IS NULL
BEGIN
    ALTER TABLE dbo.SupplierCodePrefixes ADD Description NVARCHAR(500) NULL;
END;
GO
