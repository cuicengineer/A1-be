-- Migrate Payment LinesJson into ReceiptLines for databases that already ran
-- CreateReceiptLinesTable.sql when it only migrated Receipt records.
-- Safe to re-run: skips headers that already have active ReceiptLines rows.
IF OBJECT_ID(N'dbo.ReceiptLines', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.Receipts', 'LinesJson') IS NOT NULL
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
      AND r.RecordType = N'Payment'
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.ReceiptLines rl
          WHERE rl.ReceiptId = r.Id
            AND (rl.IsDeleted IS NULL OR rl.IsDeleted = 0)
      );
END
GO
