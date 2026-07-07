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
      - Amount > 0, ContractNo and InvoiceNo present
      - @AsOfDate applies to fn_BasicContractInfo only (legacy receipt SP did not filter lines by date)
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
        ResolvedContractNo =
            COALESCE(
                NULLIF(LTRIM(RTRIM(ce.ContractNo)), ''''),
                NULLIF(LTRIM(RTRIM(c.ContractNo)), '''')
            ),
        ResolvedInvoiceNo =
            NULLIF(LTRIM(RTRIM(ce.InvoiceNo)), ''''),
        CollectionDate =
            CAST(COALESCE(ce.VrDate, ce.CollectionDate) AS DATE),
        LineAmount = CAST(ISNULL(ce.Amount, 0) AS DECIMAL(18, 4))
    FROM dbo.CollectionEntries ce
    LEFT JOIN dbo.Contracts c
        ON c.Id = ce.ContractId
       AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
    WHERE (ce.IsDeleted = 0 OR ce.IsDeleted IS NULL)
      AND LTRIM(RTRIM(UPPER(ISNULL(ce.Status, '''')))) = ''RECEIVED''
      AND ISNULL(ce.Amount, 0) > 0
      AND COALESCE(
            NULLIF(LTRIM(RTRIM(ce.ContractNo)), ''''),
            NULLIF(LTRIM(RTRIM(c.ContractNo)), '''')
          ) IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(ce.InvoiceNo)), '''') IS NOT NULL
),
ReceiptAgg AS
(
    SELECT
        ResolvedContractNo AS ContractNo,
        ReceiptDate = MAX(CollectionDate),
        ReceiptAmount = CAST(ROUND(SUM(LineAmount), 2) AS DECIMAL(18, 2))
    FROM CollectionLines
    GROUP BY ResolvedContractNo
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
        RACName = COALESCE(NULLIF(rac.[Name], ''''), ''''),
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
        WorkbookNo = ISNULL(wb.WorkbookNo, '''')
    FROM ReceiptAgg ra
    INNER JOIN dbo.Contracts c
        ON c.ContractNo = ra.ContractNo
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
        c.ContractNo,
        sf.AHQRate,
        sf.RACRate,
        sf.BaseRate
    FROM ContractBase cb
    INNER JOIN dbo.Contracts c
        ON c.Id = cb.Id
    OUTER APPLY
    (
        SELECT TOP (1)
            sf.AHQRate,
            sf.RACRate,
            sf.BaseRate
        FROM dbo.SharingFormulas sf
        WHERE sf.CmdId = c.CmdId
          AND sf.BaseId = c.BaseId
          AND sf.ClassId = c.ClassId
          AND (sf.IsDeleted = 0 OR sf.IsDeleted IS NULL)
          AND (sf.Status = 1 OR sf.Status IS NULL)
        ORDER BY sf.ApplicableDate DESC
    ) sf
),
Calculated AS
(
    SELECT
        cb.*,
        ra.ReceiptDate,
        ra.ReceiptAmount,
        Ratio =
            CASE
                WHEN cb.CurrentRentPA IS NULL THEN NULL
                ELSE CAST(ROUND(ISNULL(cb.CurrentRentPA, 0) - ra.ReceiptAmount, 2) AS DECIMAL(18, 2))
            END,
        Govt =
            CASE
                WHEN NULLIF(cb.CurrentRentPA, 0) IS NULL THEN 0
                ELSE CAST(ROUND(ISNULL(cb.GovtSharePA, 0) * (ra.ReceiptAmount / NULLIF(cb.CurrentRentPA, 0)), 2) AS DECIMAL(18, 2))
            END,
        lsf.AHQRate,
        lsf.RACRate,
        lsf.BaseRate
    FROM ReceiptAgg ra
    INNER JOIN ContractBase cb
        ON cb.ContractNo = ra.ContractNo
    LEFT JOIN LatestSharingFormula lsf
        ON lsf.ContractNo = cb.ContractNo
),
ShareCalc AS
(
    SELECT
        *,
        PAF = CAST(ROUND(ReceiptAmount - Govt, 2) AS DECIMAL(18, 2))
    FROM Calculated
)
SELECT
    SN = ROW_NUMBER() OVER (ORDER BY ReceiptDate DESC, ContractNo),
    Id,
    ContractNo,
    RACName,
    Base = COALESCE(NULLIF(BaseFullName, ''''), NULLIF(BaseName, ''''), ''''),
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
    AHQ = CAST(ROUND(PAF * ISNULL(AHQRate, 0) / 100.0, 2) AS DECIMAL(18, 2)),
    RAC = CAST(ROUND(PAF * ISNULL(RACRate, 0) / 100.0, 2) AS DECIMAL(18, 2)),
    BaseShare =
        CAST(
            ROUND(
                PAF
                - ROUND(PAF * ISNULL(AHQRate, 0) / 100.0, 2)
                - ROUND(PAF * ISNULL(RACRate, 0) / 100.0, 2),
                2
            ) AS DECIMAL(18, 2)
        ),
    Workbook = ISNULL(WorkbookNo, ''''),
    CAId = ContractNo,
    CAArea1 = VaArea,
    CAArea2 = COALESCE(GroupArea, AreaBase, VaArea)
FROM ShareCalc
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
