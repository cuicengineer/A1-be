IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'ContractAnnotations'
)
BEGIN
    CREATE TABLE dbo.ContractAnnotations (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ContractId INT NOT NULL,
        Remarks NVARCHAR(500) NOT NULL,
        RemarksBy NVARCHAR(150) NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE INDEX IX_ContractAnnotations_ContractId
        ON dbo.ContractAnnotations (ContractId);
END;
GO

IF COL_LENGTH('dbo.ContractAnnotations', 'RemarksBy') IS NULL
    ALTER TABLE dbo.ContractAnnotations ADD RemarksBy NVARCHAR(150) NULL;
GO

IF COL_LENGTH('dbo.ContractAnnotations', 'Remarks') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ContractAnnotations ALTER COLUMN Remarks NVARCHAR(500) NOT NULL;
END;
GO
