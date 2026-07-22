USE [A1Lands]
GO

IF COL_LENGTH('dbo.CollectionEntries', 'Remarks') IS NULL
BEGIN
    ALTER TABLE dbo.CollectionEntries ADD Remarks NVARCHAR(500) NULL;
END
GO
