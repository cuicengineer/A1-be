USE [A1Lands]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
    Share distribution now uses Income Agreements > Collections (CollectionEntries)
    instead of finalized receipt JSON lines.

    Included rows (CollectionEntries only — no ContractInvoicesEdit join):
      - CollectionEntries.Status = 'Received' (these are the valid collection/receipt lines)
      - Amount > 0, resolvable ContractId and InvoiceNo present
      - One output row per ContractId (receipt amounts aggregated by contract)
      - @AsOfDate applies to fn_BasicContractInfo only (legacy receipt SP did not filter lines by date)

    Share balancing (so leaf shares reconcilable to ReceiptAmount):
      - Govt is prorated, then clamped to [0, ReceiptAmount]
      - PAF = ReceiptAmount - Govt
      - AHQ / RAC / BaseShareRaw from sharing-formula rates on PAF
      - If Govt + AHQ + RAC + BaseShareRaw > ReceiptAmount, force:
            PAF = ReceiptAmount - Govt
            BaseShare = PAF - AHQ - RAC
        so Govt + AHQ + RAC + BaseShare = ReceiptAmount
*/
ALTER PROCEDURE [dbo].[sp_GetShareDistributionFromFinalizedReceipts]
    @AsOfDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Sql NVARCHAR(MAX);
    DECLARE @PrintSql NVARCHAR(MAX);
    DECLARE @AsOfDateLiteral VARCHAR(10) = CONVERT(VARCHAR(10), @AsOfDate, 23);

    SET @Sql = N'
;WITH CollectionLines AS
(
    SELECT
        ce.Id AS CollectionEntryId,
        ce.ReceiptId,
        ResolvedContractId = COALESCE(
            NULLIF(ce.ContractId, 0),
            cById.Id,
            cByNo.Id
        ),
        ResolvedContractNo =
            COALESCE(
                NULLIF(LTRIM(RTRIM(ce.ContractNo)), ''''),
                NULLIF(LTRIM(RTRIM(cById.ContractNo)), ''''),
                NULLIF(LTRIM(RTRIM(cByNo.ContractNo)), '''')
            ),
        ResolvedInvoiceNo =
            NULLIF(LTRIM(RTRIM(ce.InvoiceNo)), ''''),
        CollectionDate =
            CAST(COALESCE(ce.VrDate, ce.CollectionDate) AS DATE),
        LineAmount = CAST(ISNULL(ce.Amount, 0) AS DECIMAL(18, 4))
    FROM dbo.CollectionEntries ce
    LEFT JOIN dbo.Contracts cById
        ON cById.Id = ce.ContractId
       AND (cById.IsDeleted = 0 OR cById.IsDeleted IS NULL)
    OUTER APPLY
    (
        SELECT TOP (1)
            c.Id,
            c.ContractNo
        FROM dbo.Contracts c
        WHERE (NULLIF(ce.ContractId, 0) IS NULL OR cById.Id IS NULL)
          AND NULLIF(LTRIM(RTRIM(ce.ContractNo)), '''') IS NOT NULL
          AND c.ContractNo = LTRIM(RTRIM(ce.ContractNo))
          AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
        ORDER BY c.Id DESC
    ) cByNo
    WHERE (ce.IsDeleted = 0 OR ce.IsDeleted IS NULL)
      AND LTRIM(RTRIM(UPPER(ISNULL(ce.Status, '''')))) = ''RECEIVED''
      AND ISNULL(ce.Amount, 0) > 0
      AND COALESCE(
            NULLIF(ce.ContractId, 0),
            cById.Id,
            cByNo.Id
          ) IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(ce.InvoiceNo)), '''') IS NOT NULL
),
ReceiptAgg AS
(
    SELECT
        ResolvedContractId AS ContractId,
        MAX(ResolvedContractNo) AS ContractNo,
        ReceiptDate = MAX(CollectionDate),
        ReceiptAmount = CAST(ROUND(SUM(LineAmount), 2) AS DECIMAL(18, 2))
    FROM CollectionLines
    GROUP BY ResolvedContractId
),
ContractBase AS
(
    SELECT
        c.Id,
        c.ContractNo,
        c.ContractStartDate,
        c.ContractEndDate,
        c.TenantNo,
        c.BusinessName,
        c.InitialRentPA,
        CurrentRentPA = COALESCE(c.currentRentPA, c.InitialRentPA),
        c.VaArea,
        c.GroupArea,
        c.CmdId,
        c.BaseId,
        c.ClassId,
        RACName = COALESCE(NULLIF(rac.[Name], ''''), ''''),
        b.Code AS BaseCode,
        b.[Name] AS BaseName,
        b.FullName AS BaseFullName,
        t.OwnerName AS TenantOwnerName,
        t.BusinessName AS TenantBusinessName,
        bci.RRFY,
        bci.AreaBase,
        bci.GroupRate,
        bci.RentalValue,
        bci.GovtShare AS GovtSharePA,
        bci.PAFShare AS PAFSharePA,
        cls.[Name] AS ClassName,
        cls.Code AS ClassCode,
        WorkbookNo = ISNULL(wb.WorkbookNo, ''''),
        ra.ReceiptDate,
        ra.ReceiptAmount
    FROM ReceiptAgg ra
    INNER JOIN dbo.Contracts c
        ON c.Id = ra.ContractId
       AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
    LEFT JOIN dbo.Bases b
        ON b.Id = c.BaseId
       AND (b.IsDeleted = 0 OR b.IsDeleted IS NULL)
    LEFT JOIN dbo.Commands rac
        ON rac.Id = c.CmdId
       AND (rac.IsDeleted = 0 OR rac.IsDeleted IS NULL)
    LEFT JOIN dbo.Classes cls
        ON cls.Id = c.ClassId
       AND (cls.IsDeleted = 0 OR cls.IsDeleted IS NULL)
    LEFT JOIN dbo.ShareDistributionWorkbookAssignments wb
        ON wb.ContractId = c.Id
       AND (wb.IsDeleted = 0 OR wb.IsDeleted IS NULL)
    LEFT JOIN dbo.Tenants t
        ON t.TenantNo = c.TenantNo
       AND (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
    LEFT JOIN dbo.fn_BasicContractInfo(@AsOfDate) bci
        ON bci.Id = c.Id
),
LatestSharingFormula AS
(
    SELECT
        cb.Id AS ContractId,
        sf.AHQRate,
        sf.RACRate,
        sf.BaseRate
    FROM ContractBase cb
    OUTER APPLY
    (
        SELECT TOP (1)
            sfInner.AHQRate,
            sfInner.RACRate,
            sfInner.BaseRate
        FROM dbo.SharingFormulas sfInner
        WHERE sfInner.CmdId = cb.CmdId
          AND sfInner.BaseId = cb.BaseId
          AND sfInner.ClassId = cb.ClassId
          AND (sfInner.IsDeleted = 0 OR sfInner.IsDeleted IS NULL)
          AND (sfInner.Status = 1 OR sfInner.Status IS NULL)
        ORDER BY sfInner.ApplicableDate DESC
    ) sf
),
Calculated AS
(
    SELECT
        cb.Id,
        cb.ContractNo,
        cb.ContractStartDate,
        cb.ContractEndDate,
        cb.TenantNo,
        cb.BusinessName,
        cb.InitialRentPA,
        cb.CurrentRentPA,
        cb.VaArea,
        cb.GroupArea,
        cb.CmdId,
        cb.BaseId,
        cb.RACName,
        cb.BaseCode,
        cb.BaseName,
        cb.BaseFullName,
        cb.TenantOwnerName,
        cb.TenantBusinessName,
        cb.RRFY,
        cb.AreaBase,
        cb.GroupRate,
        cb.RentalValue,
        cb.GovtSharePA,
        cb.PAFSharePA,
        cb.ClassName,
        cb.ClassCode,
        cb.WorkbookNo,
        cb.ReceiptDate,
        cb.ReceiptAmount,
        Ratio =
            CASE
                WHEN cb.CurrentRentPA IS NULL THEN NULL
                ELSE CAST(ROUND(ISNULL(cb.CurrentRentPA, 0) - cb.ReceiptAmount, 2) AS DECIMAL(18, 2))
            END,
        GovtRaw =
            CASE
                WHEN NULLIF(cb.CurrentRentPA, 0) IS NULL THEN CAST(0 AS DECIMAL(18, 2))
                ELSE CAST(ROUND(ISNULL(cb.GovtSharePA, 0) * (cb.ReceiptAmount / NULLIF(cb.CurrentRentPA, 0)), 2) AS DECIMAL(18, 2))
            END,
        lsf.AHQRate,
        lsf.RACRate,
        lsf.BaseRate
    FROM ContractBase cb
    LEFT JOIN LatestSharingFormula lsf
        ON lsf.ContractId = cb.Id
),
ShareCalc AS
(
    SELECT
        c.*,
        -- Clamp Govt so it never exceeds ReceiptAmount
        Govt =
            CASE
                WHEN c.GovtRaw < 0 THEN CAST(0 AS DECIMAL(18, 2))
                WHEN c.GovtRaw > c.ReceiptAmount THEN c.ReceiptAmount
                ELSE c.GovtRaw
            END
    FROM Calculated c
),
ShareParts AS
(
    SELECT
        sc.*,
        -- PAF is always the residual of ReceiptAmount after Govt
        PAF = CAST(ROUND(sc.ReceiptAmount - sc.Govt, 2) AS DECIMAL(18, 2))
    FROM ShareCalc sc
),
ShareFinal AS
(
    SELECT
        sp.*,
        AHQ = CAST(ROUND(sp.PAF * ISNULL(sp.AHQRate, 0) / 100.0, 2) AS DECIMAL(18, 2)),
        RAC = CAST(ROUND(sp.PAF * ISNULL(sp.RACRate, 0) / 100.0, 2) AS DECIMAL(18, 2)),
        BaseShareRaw = CAST(ROUND(sp.PAF * ISNULL(sp.BaseRate, 0) / 100.0, 2) AS DECIMAL(18, 2))
    FROM ShareParts sp
)
SELECT
    SN = ROW_NUMBER() OVER (ORDER BY ReceiptDate DESC, ContractNo),
    Id,
    ContractNo,
    BaseId,
    RACName,
    Base = COALESCE(NULLIF(BaseCode, ''''), NULLIF(BaseName, ''''), NULLIF(BaseFullName, ''''), ''''),
    Class = COALESCE(NULLIF(ClassName, ''''), NULLIF(ClassCode, ''''), ''''),
    Agreement =
        CASE
            WHEN NULLIF(LTRIM(RTRIM(ContractNo)), '''') IS NULL THEN ''''
            WHEN ContractStartDate IS NOT NULL AND ContractEndDate IS NOT NULL THEN
                LTRIM(RTRIM(ContractNo)) + ''('' +
                FORMAT(ContractStartDate, ''dd-MMM-yy'') + '' To '' +
                FORMAT(ContractEndDate, ''dd-MMM-yy'') + '')''
            ELSE LTRIM(RTRIM(ContractNo))
        END,
    TenantAndBusiness =
        LTRIM(RTRIM(
            COALESCE(NULLIF(TenantNo, ''''), '''')
            + CASE
                WHEN COALESCE(NULLIF(TenantOwnerName, ''''), NULLIF(TenantBusinessName, ''''), NULLIF(BusinessName, '''')) IS NULL
                    THEN ''''
                WHEN COALESCE(NULLIF(TenantNo, ''''), '''') = ''''
                    THEN COALESCE(NULLIF(TenantOwnerName, ''''), NULLIF(TenantBusinessName, ''''), NULLIF(BusinessName, ''''))
                ELSE '' - '' + COALESCE(NULLIF(TenantOwnerName, ''''), NULLIF(TenantBusinessName, ''''), NULLIF(BusinessName, ''''))
              END
        )),
    BoOArea = COALESCE(GroupArea, AreaBase, VaArea),
    RRFY,
    RevenueRate = GroupRate,
    GovtSharePA,
    CurrentRentPA,
    ReceiptDate,
    ReceiptAmount,
    Ratio,
    Govt,
    PAF,
    AHQ,
    RAC,
    -- When rate-based leaf shares overrun ReceiptAmount, force residual BaseShare
    -- so Govt + AHQ + RAC + BaseShare = ReceiptAmount (PAF = ReceiptAmount - Govt).
    BaseShare =
        CASE
            WHEN (Govt + AHQ + RAC + BaseShareRaw) > ReceiptAmount
            THEN CAST(ROUND(PAF - AHQ - RAC, 2) AS DECIMAL(18, 2))
            ELSE BaseShareRaw
        END,
    Workbook = ISNULL(WorkbookNo, ''''),
    CAId = ContractNo,
    CAArea1 = VaArea,
    CAArea2 = COALESCE(GroupArea, AreaBase, VaArea)
FROM ShareFinal
ORDER BY ReceiptDate DESC, ContractNo;';

    SET @PrintSql = REPLACE(@Sql, N'@AsOfDate', QUOTENAME(@AsOfDateLiteral, ''''));

    PRINT '-- sp_GetShareDistributionFromFinalizedReceipts executed query:';

    DECLARE @PrintOffset INT = 1;
    DECLARE @PrintChunkLen INT = 4000;
    DECLARE @PrintTotalLen INT = LEN(@PrintSql);
    DECLARE @PrintChunk NVARCHAR(4000);

    WHILE @PrintOffset <= @PrintTotalLen
    BEGIN
        SET @PrintChunk = SUBSTRING(@PrintSql, @PrintOffset, @PrintChunkLen);
        PRINT @PrintChunk;
        SET @PrintOffset += @PrintChunkLen;
    END;

    EXEC sys.sp_executesql
        @Sql,
        N'@AsOfDate DATE',
        @AsOfDate = @AsOfDate;
END
GO
