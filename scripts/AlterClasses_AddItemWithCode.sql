/*
  Add ItemWithCode to dbo.Classes for linking a Class to a Product/Service
  "Item with Code" used when finalizing contract invoices.
*/

IF COL_LENGTH('dbo.Classes', 'ItemWithCode') IS NULL
BEGIN
    ALTER TABLE dbo.Classes ADD ItemWithCode NVARCHAR(100) NULL;
END
GO

PRINT 'Classes.ItemWithCode column ensured.';
GO
