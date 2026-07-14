-- Journal Entry line items (normalized rows for reporting)
-- Note: LineNo is a SQL Server reserved keyword (LINENO), so it must be quoted as [LineNo].
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'JournalEntriesLines' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.JournalEntriesLines
    (
        Id              INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        JournalEntryId  INT            NOT NULL,
        [LineNo]        INT            NOT NULL,
        AccountSource   NVARCHAR(20)   NULL,
        AccountCoaId    NVARCHAR(50)   NULL,
        AccountLabel    NVARCHAR(250)  NULL,
        ContractId      NVARCHAR(50)   NULL,
        ContractNo      NVARCHAR(100)  NULL,
        InvoiceKey      NVARCHAR(100)  NULL,
        InvoiceNo       NVARCHAR(100)  NULL,
        InvoiceLabel    NVARCHAR(250)  NULL,
        Quantity        DECIMAL(18,4)  NULL,
        UnitPrice       DECIMAL(18,2)  NULL,
        Debit           DECIMAL(18,2)  NOT NULL CONSTRAINT DF_JournalEntriesLines_Debit DEFAULT (0),
        Credit          DECIMAL(18,2)  NOT NULL CONSTRAINT DF_JournalEntriesLines_Credit DEFAULT (0),
        ActionDate      DATETIME2(3)   NULL,
        ActionBy        NVARCHAR(150)  NULL,
        Action          NVARCHAR(50)   NULL,
        IsDeleted       BIT            NULL,
        CONSTRAINT FK_JournalEntriesLines_JournalEntries
            FOREIGN KEY (JournalEntryId) REFERENCES dbo.JournalEntries (Id)
    );

    CREATE INDEX IX_JournalEntriesLines_JournalEntryId
        ON dbo.JournalEntriesLines (JournalEntryId);

    CREATE INDEX IX_JournalEntriesLines_JournalEntryId_LineNo
        ON dbo.JournalEntriesLines (JournalEntryId, [LineNo]);

    CREATE INDEX IX_JournalEntriesLines_IsDeleted
        ON dbo.JournalEntriesLines (IsDeleted);
END
GO

-- Optional one-time migration from legacy LinesJson into JournalEntriesLines.
-- Safe to re-run: only migrates headers that have JSON but no active line rows yet.
IF COL_LENGTH('dbo.JournalEntries', 'LinesJson') IS NOT NULL
BEGIN
    INSERT INTO dbo.JournalEntriesLines
    (
        JournalEntryId,
        [LineNo],
        AccountSource,
        AccountCoaId,
        AccountLabel,
        ContractId,
        ContractNo,
        InvoiceKey,
        InvoiceNo,
        InvoiceLabel,
        Quantity,
        UnitPrice,
        Debit,
        Credit,
        ActionDate,
        ActionBy,
        Action,
        IsDeleted
    )
    SELECT
        je.Id,
        ROW_NUMBER() OVER (PARTITION BY je.Id ORDER BY (SELECT 1)) AS [LineNo],
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.accountSource'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.accountCoaId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.accountLabel'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.contractId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.contractNo'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.invoiceKey'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.invoiceNo'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.invoiceLabel'))), ''),
        TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(line.value, '$.quantity')),
        TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.unitPrice')),
        ISNULL(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.debit')), 0),
        ISNULL(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.credit')), 0),
        je.ActionDate,
        je.ActionBy,
        je.Action,
        0
    FROM dbo.JournalEntries je
    CROSS APPLY OPENJSON(je.LinesJson) line
    WHERE ISJSON(je.LinesJson) = 1
      AND (je.IsDeleted IS NULL OR je.IsDeleted = 0)
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.JournalEntriesLines jel
          WHERE jel.JournalEntryId = je.Id
            AND (jel.IsDeleted IS NULL OR jel.IsDeleted = 0)
      );
END
GO
