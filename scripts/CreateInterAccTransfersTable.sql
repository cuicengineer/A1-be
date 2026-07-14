-- Inter Account Transfers (Cash & Fund Flow module)
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'InterAccTransfers' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.InterAccTransfers
    (
        Id                   INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TransferDate         DATE           NOT NULL,
        VrNo                 NVARCHAR(50)   NOT NULL,
        Description          NVARCHAR(35)   NULL,
        Particulars          NVARCHAR(500)  NULL,
        PaidFromAccountId    INT            NOT NULL,
        SettlementVrNo       NVARCHAR(50)   NULL,
        PaidFromAmount       DECIMAL(18,2)  NOT NULL,
        ReceivedInAccountId  INT            NOT NULL,
        ReceivedInAmount     DECIMAL(18,2)  NOT NULL,
        TinFtn               NVARCHAR(50)   NULL,
        Status               NVARCHAR(20)   NULL,
        ActionDate           DATETIME2(3)   NULL,
        ActionBy             NVARCHAR(150)  NULL,
        Action               NVARCHAR(50)   NULL,
        IsDeleted            BIT            NULL
    );

    CREATE INDEX IX_InterAccTransfers_TransferDate ON dbo.InterAccTransfers (TransferDate);
    CREATE INDEX IX_InterAccTransfers_PaidFromAccountId ON dbo.InterAccTransfers (PaidFromAccountId);
    CREATE INDEX IX_InterAccTransfers_ReceivedInAccountId ON dbo.InterAccTransfers (ReceivedInAccountId);
    CREATE INDEX IX_InterAccTransfers_IsDeleted ON dbo.InterAccTransfers (IsDeleted);
END
GO
