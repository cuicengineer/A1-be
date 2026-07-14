-- Add IsLock to JournalEntries (Lock / Unlock status for AHQ supervisor / superuser)
IF COL_LENGTH('dbo.JournalEntries', 'IsLock') IS NULL
BEGIN
    ALTER TABLE dbo.JournalEntries
        ADD IsLock BIT NOT NULL
            CONSTRAINT DF_JournalEntries_IsLock DEFAULT (0);
END
GO
