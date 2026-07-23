/*
  Add Tenant/Customer/Supplier party snapshot columns to Journal Entry lines.
  Run against the A1 database after deploying the matching API model changes.
*/

IF COL_LENGTH('dbo.JournalEntriesLines', 'PartyKey') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntriesLines ADD PartyKey NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.JournalEntriesLines', 'PartyType') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntriesLines ADD PartyType NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH('dbo.JournalEntriesLines', 'PartyId') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntriesLines ADD PartyId NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH('dbo.JournalEntriesLines', 'PartyCode') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntriesLines ADD PartyCode NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.JournalEntriesLines', 'PartyName') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntriesLines ADD PartyName NVARCHAR(300) NULL;
END
GO

IF COL_LENGTH('dbo.JournalEntriesLines', 'PartyLabel') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntriesLines ADD PartyLabel NVARCHAR(300) NULL;
END
GO

PRINT 'JournalEntriesLines party columns ensured (PartyKey, PartyType, PartyId, PartyCode, PartyName, PartyLabel).';
GO
