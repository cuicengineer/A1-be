-- Add UoM to Classes (Marla, Sq Ft, Acre).
IF COL_LENGTH('dbo.Classes', 'UoM') IS NULL
BEGIN
    ALTER TABLE dbo.Classes
    ADD UoM NVARCHAR(20) NULL;
END
GO

UPDATE dbo.Classes
SET UoM = 'Marla'
WHERE UoM IS NULL OR LTRIM(RTRIM(UoM)) = '';
GO
