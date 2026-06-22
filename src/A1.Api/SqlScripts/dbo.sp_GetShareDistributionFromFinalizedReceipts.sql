USE [A1Lands]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_GetShareDistributionFromFinalizedReceipts]
    @AsOfDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH ReceiptLines AS
    (
        SELECT
            r.Id AS ReceiptId,
            r.[Date] AS ReceiptDate,
            ResolvedContractNo =
                COALESCE(
                    NULLIF(LTRIM(RTRIM(line.contractNo)), ''),
                    NULLIF(LTRIM(RTRIM(line.ContractNo)), ''),
                    CASE
                        WHEN CHARINDEX('|', COALESCE(line.invoiceKey, line.InvoiceKey, '')) > 0
                            THEN NULLIF(
                                LTRIM(RTRIM(
                                    LEFT(
                                        COALESCE(line.invoiceKey, line.InvoiceKey),
                                        CHARINDEX('|', COALESCE(line.invoiceKey, line.InvoiceKey)) - 1
                                    )
                                )),
                                ''
                            )
                    END
                ),
            ResolvedInvoiceNo =
                COALESCE(
                    NULLIF(LTRIM(RTRIM(line.invoiceNo)), ''),
                    NULLIF(LTRIM(RTRIM(line.InvoiceNo)), ''),
                    CASE
                        WHEN CHARINDEX('|', COALESCE(line.invoiceKey, line.InvoiceKey, '')) > 0
                            THEN NULLIF(
                                LTRIM(RTRIM(
                                    SUBSTRING(
                                        COALESCE(line.invoiceKey, line.InvoiceKey),
                                        CHARINDEX('|', COALESCE(line.invoiceKey, line.InvoiceKey)) + 1,
                                        4000
                                    )
                                )),
                                ''
                            )
                        ELSE NULLIF(LTRIM(RTRIM(COALESCE(line.invoiceKey, line.InvoiceKey))), '')
                    END
                ),
            LineAmount =
                CASE
                    WHEN ISNULL(nums.ParsedTotal, 0) > 0
                        THEN nums.ParsedTotal
                    WHEN ISNULL(nums.ParsedAmount, 0) <= 0
                        THEN 0
                    ELSE
                        nums.ParsedAmount
                        * CASE WHEN ISNULL(nums.ParsedQuantity, 0) > 0 THEN nums.ParsedQuantity ELSE 1 END
                        * (1.0 - CASE WHEN ISNULL(nums.ParsedDiscount, 0) < 0 THEN 0 ELSE nums.ParsedDiscount END / 100.0)
                        * (1.0 + CASE WHEN ISNULL(nums.ParsedTax, 0) < 0 THEN 0 ELSE nums.ParsedTax END / 100.0)
                END
        FROM dbo.Receipts r
        CROSS APPLY OPENJSON(r.LinesJson)
        WITH
        (
            contractNo NVARCHAR(100) '$.contractNo',
            ContractNo NVARCHAR(100) '$.ContractNo',
            invoiceNo NVARCHAR(100) '$.invoiceNo',
            InvoiceNo NVARCHAR(100) '$.InvoiceNo',
            invoiceKey NVARCHAR(200) '$.invoiceKey',
            InvoiceKey NVARCHAR(200) '$.InvoiceKey',
            amount NVARCHAR(100) '$.amount',
            Amount NVARCHAR(100) '$.Amount',
            quantity NVARCHAR(100) '$.quantity',
            Quantity NVARCHAR(100) '$.Quantity',
            discount NVARCHAR(100) '$.discount',
            Discount NVARCHAR(100) '$.Discount',
            tax NVARCHAR(100) '$.tax',
            Tax NVARCHAR(100) '$.Tax',
            total NVARCHAR(100) '$.total',
            Total NVARCHAR(100) '$.Total'
        ) line
        CROSS APPLY
        (
            SELECT
                ParsedTotal = COALESCE(
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.total, ',', ''))), '')),
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.Total, ',', ''))), ''))
                ),
                ParsedAmount = COALESCE(
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.amount, ',', ''))), '')),
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.Amount, ',', ''))), ''))
                ),
                ParsedQuantity = COALESCE(
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.quantity, ',', ''))), '')),
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.Quantity, ',', ''))), ''))
                ),
                ParsedDiscount = COALESCE(
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.discount, ',', ''))), '')),
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.Discount, ',', ''))), ''))
                ),
                ParsedTax = COALESCE(
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.tax, ',', ''))), '')),
                    TRY_CONVERT(DECIMAL(18, 4), NULLIF(LTRIM(RTRIM(REPLACE(line.Tax, ',', ''))), ''))
                )
        ) nums
        WHERE (r.IsDeleted = 0 OR r.IsDeleted IS NULL)
          AND r.FinalizedByAhq = 1
          AND ISJSON(r.LinesJson) = 1
          AND (@AsOfDate IS NULL OR r.[Date] IS NULL OR r.[Date] <= @AsOfDate)
    ),
    ValidReceiptLines AS
    (
        SELECT
            rl.ResolvedContractNo AS ContractNo,
            rl.ResolvedInvoiceNo AS InvoiceNo,
            rl.ReceiptDate,
            rl.LineAmount
        FROM ReceiptLines rl
        INNER JOIN dbo.ContractInvoicesEdit ci
            ON ci.ContractNo = rl.ResolvedContractNo
           AND ci.InvoiceNo = rl.ResolvedInvoiceNo
           AND ci.SubInvoiceNo IS NULL
           AND ci.IsFinalized = 1
           AND (ci.IsDeleted = 0 OR ci.IsDeleted IS NULL)
        WHERE rl.ResolvedContractNo IS NOT NULL
          AND rl.ResolvedInvoiceNo IS NOT NULL
          AND rl.LineAmount > 0
    ),
    ReceiptAgg AS
    (
        SELECT
            ContractNo,
            ReceiptDate = MAX(ReceiptDate),
            ReceiptAmount = CAST(ROUND(SUM(LineAmount), 2) AS DECIMAL(18, 2))
        FROM ValidReceiptLines
        GROUP BY ContractNo
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
            b.[Name] AS BaseName,
            b.FullName AS BaseFullName,
            t.OwnerName AS TenantOwnerName,
            t.BusinessName AS TenantBusinessName,
            bci.RRFY,
            bci.AreaBase,
            bci.GroupRate,
            bci.RentalValue,
            bci.GovtShare AS GovtSharePA,
            bci.PAFShare AS PAFSharePA
        FROM ReceiptAgg ra
        INNER JOIN dbo.Contracts c
            ON c.ContractNo = ra.ContractNo
           AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
        LEFT JOIN dbo.Bases b
            ON b.Id = c.BaseId
           AND (b.IsDeleted = 0 OR b.IsDeleted IS NULL)
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
        Id,
        ContractNo,
        Base = COALESCE(NULLIF(BaseFullName, ''), NULLIF(BaseName, ''), ''),
        CAId = ContractNo,
        TenantAndBusiness =
            LTRIM(RTRIM(
                COALESCE(NULLIF(TenantNo, ''), '')
                + CASE
                    WHEN COALESCE(NULLIF(TenantOwnerName, ''), NULLIF(TenantBusinessName, ''), NULLIF(BusinessName, '')) IS NULL
                        THEN ''
                    WHEN COALESCE(NULLIF(TenantNo, ''), '') = ''
                        THEN COALESCE(NULLIF(TenantOwnerName, ''), NULLIF(TenantBusinessName, ''), NULLIF(BusinessName, ''))
                    ELSE ' - ' + COALESCE(NULLIF(TenantOwnerName, ''), NULLIF(TenantBusinessName, ''), NULLIF(BusinessName, ''))
                  END
            )),
        CAArea1 = VaArea,
        CAArea2 = COALESCE(GroupArea, AreaBase, VaArea),
        RevenueRate = GroupRate,
        RRFY,
        RentalValue,
        AnnualRent = InitialRentPA,
        CurrentRentPA,
        GovtSharePA,
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
        Workbook = ''
    FROM ShareCalc
    ORDER BY ReceiptDate DESC, ContractNo;
END
GO
