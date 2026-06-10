IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'IncomeStatements'
)
BEGIN
    CREATE TABLE dbo.IncomeStatements (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AcctId NVARCHAR(50) NULL,
        AcctName NVARCHAR(200) NOT NULL,
        GroupName NVARCHAR(50) NOT NULL,
        SubGroup NVARCHAR(150) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_IncomeStatements_SortOrder DEFAULT (0),
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE INDEX IX_IncomeStatements_SortOrder
        ON dbo.IncomeStatements (SortOrder);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'dbo' AND t.name = 'IncomeStatementSubGroups'
)
BEGIN
    CREATE TABLE dbo.IncomeStatementSubGroups (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        GroupName NVARCHAR(50) NOT NULL,
        SubGroupName NVARCHAR(150) NOT NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE INDEX IX_IncomeStatementSubGroups_GroupName
        ON dbo.IncomeStatementSubGroups (GroupName);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.IncomeStatementSubGroups WHERE IsDeleted = 0 OR IsDeleted IS NULL)
BEGIN
    INSERT INTO dbo.IncomeStatementSubGroups (GroupName, SubGroupName, ActionDate, Action, IsDeleted)
    VALUES
        (N'Revenue', N'Membership', GETUTCDATE(), N'SEED', 0),
        (N'Revenue', N'Contribution', GETUTCDATE(), N'SEED', 0),
        (N'Expenses', N'Recurring Expenses', GETUTCDATE(), N'SEED', 0),
        (N'Expenses', N'Non-Recurring Expenses', GETUTCDATE(), N'SEED', 0);
END;
GO
