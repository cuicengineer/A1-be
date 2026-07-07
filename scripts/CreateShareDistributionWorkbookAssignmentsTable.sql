USE [A1Lands]
GO

IF OBJECT_ID(N'dbo.ShareDistributionWorkbookAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShareDistributionWorkbookAssignments
    (
        Id INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        ContractId INT NOT NULL,
        WorkbookNo NVARCHAR(30) NOT NULL,
        WorkbookSerial INT NOT NULL,
        WorkbookCreatedDate DATE NOT NULL,
        ActionDate DATETIME NULL,
        ActionBy NVARCHAR(150) NULL,
        Action NVARCHAR(50) NULL,
        IsDeleted BIT NULL
    );

    CREATE UNIQUE INDEX UX_ShareDistributionWorkbookAssignments_ContractId
        ON dbo.ShareDistributionWorkbookAssignments (ContractId)
        WHERE IsDeleted = 0 ;

    CREATE INDEX IX_ShareDistributionWorkbookAssignments_WorkbookNo
        ON dbo.ShareDistributionWorkbookAssignments (WorkbookNo)
        WHERE IsDeleted = 0;
END
GO
