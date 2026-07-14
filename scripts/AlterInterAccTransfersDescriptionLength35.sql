-- Increase InterAccTransfers.Description from NVARCHAR(15) to NVARCHAR(35)
IF COL_LENGTH('dbo.InterAccTransfers', 'Description') IS NOT NULL
BEGIN
    ALTER TABLE dbo.InterAccTransfers
        ALTER COLUMN Description NVARCHAR(35) NULL;
END
