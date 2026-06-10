IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'ChartOfAccounts'
)
BEGIN
    CREATE TABLE dbo.ChartOfAccounts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AcctId NVARCHAR(50) NULL,
        AcctName NVARCHAR(200) NOT NULL,
        GroupName NVARCHAR(50) NOT NULL,
        SubGroup NVARCHAR(150) NULL,
        ControlAccount NVARCHAR(100) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_ChartOfAccounts_SortOrder DEFAULT (0),
        SectionType NVARCHAR(100) NOT NULL CONSTRAINT DF_ChartOfAccounts_SectionType DEFAULT ('(A) Balance Sheet'),
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE INDEX IX_ChartOfAccounts_SectionType_SortOrder
        ON dbo.ChartOfAccounts (SectionType, SortOrder);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'ChartOfAccountSubGroups'
)
BEGIN
    CREATE TABLE dbo.ChartOfAccountSubGroups (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        GroupName NVARCHAR(50) NOT NULL,
        SubGroupName NVARCHAR(150) NOT NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE INDEX IX_ChartOfAccountSubGroups_GroupName
        ON dbo.ChartOfAccountSubGroups (GroupName);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'ChartOfAccountControlAccounts'
)
BEGIN
    CREATE TABLE dbo.ChartOfAccountControlAccounts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ControlAccountName NVARCHAR(100) NOT NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE INDEX IX_ChartOfAccountControlAccounts_Name
        ON dbo.ChartOfAccountControlAccounts (ControlAccountName);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ChartOfAccountControlAccounts WHERE IsDeleted = 0 OR IsDeleted IS NULL)
BEGIN
    INSERT INTO dbo.ChartOfAccountControlAccounts (ControlAccountName, ActionDate, Action, IsDeleted)
    VALUES
        (N'Cash & Bank', GETUTCDATE(), N'SEED', 0),
        (N'Supplier', GETUTCDATE(), N'SEED', 0),
        (N'Customers', GETUTCDATE(), N'SEED', 0),
        (N'Inventory', GETUTCDATE(), N'SEED', 0),
        (N'Contracts', GETUTCDATE(), N'SEED', 0),
        (N'Investment', GETUTCDATE(), N'SEED', 0),
        (N'Property', GETUTCDATE(), N'SEED', 0),
        (N'Employee', GETUTCDATE(), N'SEED', 0),
        (N'Loan', GETUTCDATE(), N'SEED', 0);
END;
GO
