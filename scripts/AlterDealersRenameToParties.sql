-- Renames dbo.Dealers to dbo.Parties and related indexes/constraints.
-- Safe to re-run: each step checks for the old name before renaming.

IF OBJECT_ID(N'dbo.Dealers', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Parties', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.Dealers', N'Parties', 'OBJECT';
END
GO

IF OBJECT_ID(N'dbo.DF_Dealers_Status', N'D') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.DF_Dealers_Status', N'DF_Parties_Status', 'OBJECT';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Dealers_BankListsId' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    EXEC sp_rename N'dbo.Parties.IX_Dealers_BankListsId', N'IX_Parties_BankListsId', N'INDEX';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Dealers_IsDeleted' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    EXEC sp_rename N'dbo.Parties.IX_Dealers_IsDeleted', N'IX_Parties_IsDeleted', N'INDEX';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Dealers_Name_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    EXEC sp_rename N'dbo.Parties.UX_Dealers_Name_Active', N'UX_Parties_Name_Active', N'INDEX';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Dealers_NtnCnic_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    EXEC sp_rename N'dbo.Parties.UX_Dealers_NtnCnic_Active', N'UX_Parties_NtnCnic_Active', N'INDEX';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Dealers_IBAN_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    EXEC sp_rename N'dbo.Parties.UX_Dealers_IBAN_Active', N'UX_Parties_IBAN_Active', N'INDEX';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Dealers_Code_Active' AND object_id = OBJECT_ID(N'dbo.Parties')
)
BEGIN
    EXEC sp_rename N'dbo.Parties.UX_Dealers_Code_Active', N'UX_Parties_Code_Active', N'INDEX';
END
GO
