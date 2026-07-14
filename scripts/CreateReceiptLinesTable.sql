-- Receipt line items (normalized rows for reporting)
-- Note: LineNo is a SQL Server reserved keyword (LINENO), so it must be quoted as [LineNo].
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = N'ReceiptLines' AND s.name = N'dbo'
)
BEGIN
    CREATE TABLE dbo.ReceiptLines
    (
        Id                 INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ReceiptId          INT            NOT NULL,
        [LineNo]           INT            NOT NULL,
        RacId              NVARCHAR(50)   NULL,
        BaseId             NVARCHAR(50)   NULL,
        Item               NVARCHAR(300)  NULL,
        Account            NVARCHAR(300)  NULL,
        AccountCoaId       NVARCHAR(50)   NULL,
        PartyKey           NVARCHAR(100)  NULL,
        PartyType          NVARCHAR(50)   NULL,
        PartyId            NVARCHAR(50)   NULL,
        PartyCode          NVARCHAR(100)  NULL,
        PartyName          NVARCHAR(300)  NULL,
        PartyLabel         NVARCHAR(300)  NULL,
        ContractId         NVARCHAR(50)   NULL,
        InvoiceKey         NVARCHAR(100)  NULL,
        ContractNo         NVARCHAR(100)  NULL,
        InvoiceNo          NVARCHAR(100)  NULL,
        CollectionEntryId  NVARCHAR(50)   NULL,
        TinTrn             NVARCHAR(100)  NULL,
        TinFtn             NVARCHAR(100)  NULL,
        Amount             DECIMAL(18,2)  NOT NULL CONSTRAINT DF_ReceiptLines_Amount DEFAULT (0),
        UnitPrice          DECIMAL(18,2)  NULL,
        Quantity           DECIMAL(18,4)  NULL,
        ProductKey         NVARCHAR(100)  NULL,
        ProductType        NVARCHAR(50)   NULL,
        ProductId          NVARCHAR(50)   NULL,
        Discount           DECIMAL(18,2)  NOT NULL CONSTRAINT DF_ReceiptLines_Discount DEFAULT (0),
        Tax                DECIMAL(18,2)  NOT NULL CONSTRAINT DF_ReceiptLines_Tax DEFAULT (0),
        Total              DECIMAL(18,2)  NOT NULL CONSTRAINT DF_ReceiptLines_Total DEFAULT (0),
        ActionDate         DATETIME2(3)   NULL,
        ActionBy           NVARCHAR(150)  NULL,
        Action             NVARCHAR(50)   NULL,
        IsDeleted          BIT            NULL,
        CONSTRAINT FK_ReceiptLines_Receipts
            FOREIGN KEY (ReceiptId) REFERENCES dbo.Receipts (Id)
    );

    CREATE INDEX IX_ReceiptLines_ReceiptId
        ON dbo.ReceiptLines (ReceiptId);

    CREATE INDEX IX_ReceiptLines_ReceiptId_LineNo
        ON dbo.ReceiptLines (ReceiptId, [LineNo]);

    CREATE INDEX IX_ReceiptLines_IsDeleted
        ON dbo.ReceiptLines (IsDeleted);
END
GO

-- Optional one-time migration from legacy LinesJson into ReceiptLines
-- (Receipt and Payment records that share dbo.Receipts).
-- Safe to re-run: only migrates headers that have JSON but no active line rows yet.
IF COL_LENGTH('dbo.Receipts', 'LinesJson') IS NOT NULL
BEGIN
    INSERT INTO dbo.ReceiptLines
    (
        ReceiptId,
        [LineNo],
        RacId,
        BaseId,
        Item,
        Account,
        AccountCoaId,
        PartyKey,
        PartyType,
        PartyId,
        PartyCode,
        PartyName,
        PartyLabel,
        ContractId,
        InvoiceKey,
        ContractNo,
        InvoiceNo,
        CollectionEntryId,
        TinTrn,
        TinFtn,
        Amount,
        UnitPrice,
        Quantity,
        ProductKey,
        ProductType,
        ProductId,
        Discount,
        Tax,
        Total,
        ActionDate,
        ActionBy,
        Action,
        IsDeleted
    )
    SELECT
        r.Id,
        ROW_NUMBER() OVER (PARTITION BY r.Id ORDER BY (SELECT 1)) AS [LineNo],
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.racId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.baseId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.item'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.account'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.accountCoaId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.partyKey'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.partyType'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.partyId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.partyCode'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.partyName'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.partyLabel'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.contractId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.invoiceKey'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.contractNo'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.invoiceNo'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.collectionEntryId'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.tinTrn'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.tinFtn'))), ''),
        ISNULL(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.amount')), 0),
        TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.unitPrice')),
        TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(line.value, '$.quantity')),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.productKey'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.productType'))), ''),
        NULLIF(LTRIM(RTRIM(JSON_VALUE(line.value, '$.productId'))), ''),
        ISNULL(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.discount')), 0),
        ISNULL(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.tax')), 0),
        ISNULL(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(line.value, '$.total')), 0),
        r.ActionDate,
        r.ActionBy,
        r.Action,
        0
    FROM dbo.Receipts r
    CROSS APPLY OPENJSON(r.LinesJson) line
    WHERE ISJSON(r.LinesJson) = 1
      AND (r.IsDeleted IS NULL OR r.IsDeleted = 0)
      AND (
          r.RecordType IS NULL
          OR r.RecordType = N''
          OR r.RecordType = N'Receipt'
          OR r.RecordType = N'Payment'
      )
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.ReceiptLines rl
          WHERE rl.ReceiptId = r.Id
            AND (rl.IsDeleted IS NULL OR rl.IsDeleted = 0)
      );
END
GO
