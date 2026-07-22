USE [A1Lands]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

/*
  fn_BasicContractInfo — Cat C / BTS share fix (v3)

  Diagnostic for C786AHQ-P786 showed:
    Stored: InitialRentPA=480000, GovtShare=6700, PAFShare=473300  (correct)
    Function: GovtShare=480000, PAFShare=0                         (wrong)

  Cause: rent-driven path did InitialRentPA × govRate.Rate/100 and the
  active Type=2 rate for Cat C is 100 (same as RV%), wiping PAF.

  Fix for Cat C / BTS:
    Prefer contract stored GovtShare / PAFShare when present.
    Else: GovtShare = InitialRentPA × Govt% (no ROUND of % to 0 decimals)
          PAFShare  = MAX(0, InitialRentPA − GovtShare)

  Cat A / Cat B: unchanged area × revenue-rate formulas.
*/

ALTER FUNCTION [dbo].[fn_BasicContractInfo](@AsOfDate date)
RETURNS TABLE
AS
RETURN
(
    SELECT
        c.Id,
        c.ContractNo,
        c.CommercialOperationDate,
        ContractState =
        CASE
            WHEN @AsOfDate < c.ContractStartDate THEN 'Pre-Mature'
            WHEN @AsOfDate > c.ContractEndDate THEN 'Terminated'
            WHEN DATEDIFF(MONTH, @AsOfDate, c.ContractEndDate) < 4 THEN 'Expiring'
            ELSE 'Valid'
        END,

        RRFY = pg.Fiscal,

        calc2.AHQShare,
        calc2.RACShare,
        calc2.BaseShare,

        FY =
        CASE
            WHEN @AsOfDate > c.ContractEndDate
            THEN
                CASE
                    WHEN MONTH(c.ContractEndDate) > 6
                        THEN CAST(YEAR(c.ContractEndDate) AS VARCHAR(4))
                             + '-' + RIGHT(CAST(YEAR(c.ContractEndDate) + 1 AS VARCHAR(4)), 2)
                    ELSE CAST(YEAR(c.ContractEndDate) - 1 AS VARCHAR(4))
                         + '-' + RIGHT(CAST(YEAR(c.ContractEndDate) AS VARCHAR(4)), 2)
                END
            ELSE
                CASE
                    WHEN MONTH(@AsOfDate) > 6
                        THEN CAST(YEAR(@AsOfDate) AS VARCHAR(4))
                             + '-' + RIGHT(CAST(YEAR(@AsOfDate) + 1 AS VARCHAR(4)), 2)
                    ELSE CAST(YEAR(@AsOfDate) - 1 AS VARCHAR(4))
                         + '-' + RIGHT(CAST(YEAR(@AsOfDate) AS VARCHAR(4)), 2)
                END
        END,

        ISNULL(base.AreaBase, -99) AS AreaBase,
        ISNULL(base.RateBase, -99) AS GroupRate,

        CAST(ROUND(rvRate.Rate, 0) AS BIGINT) AS RentalValueRate,
        rvRate.ApplicableDate AS Auto_RentalValueRateAppDate,

        CAST(ROUND(govRate.Rate, 4) AS DECIMAL(18, 4)) AS Auto_GovtSharePercent,
        govRate.ApplicableDate AS Auto_GovtShareRateAppDate,

        calc1.RentalValue,
        calc1.GovtShare,
        calc1.PAFShare,

        Viability =
        CASE
            WHEN c.InitialRentPA IS NOT NULL
                 AND calc1.GovtShare IS NOT NULL
                 AND c.InitialRentPA <= calc1.GovtShare
            THEN N'Unviable'
            WHEN c.InitialRentPA IS NOT NULL
                 AND calc1.GovtShare IS NOT NULL
            THEN N'Viable'
            ELSE NULL
        END,

        ISNULL(inv.DueCount, 0) AS due,
        ISNULL(paid.PaidCount, 0) AS paid,
        ISNULL(inv.DueCount, 0) - ISNULL(paid.PaidCount, 0) AS rcvable,
        CASE
            WHEN ISNULL(paid.PaidCount, 0) = 0
                THEN 'Unpaid'
            WHEN ISNULL(paid.PaidCount, 0) < ISNULL(inv.DueCount, 0)
                THEN 'Partial Paid'
            WHEN ISNULL(paid.PaidCount, 0) = ISNULL(inv.DueCount, 0)
                THEN 'Full Paid'
            WHEN ISNULL(paid.PaidCount, 0) > ISNULL(inv.DueCount, 0)
                THEN 'Over Paid'
            ELSE NULL
        END AS invoices,
        TenantNo
    FROM dbo.Contracts c

    LEFT JOIN dbo.Commands cmd
        ON cmd.Id = c.CmdId
        AND (cmd.IsDeleted = 0 OR cmd.IsDeleted IS NULL)

    LEFT JOIN dbo.Bases b
        ON b.Id = c.BaseId
        AND (b.IsDeleted = 0 OR b.IsDeleted IS NULL)

    LEFT JOIN dbo.Classes cls
        ON cls.Id = c.ClassId
        AND (cls.IsDeleted = 0 OR cls.IsDeleted IS NULL)

    OUTER APPLY
    (
        SELECT TOP (1) pg.*
        FROM dbo.vw_LatestRevenueRates pg
        WHERE pg.Id = c.GrpId
          AND TRY_CAST(LEFT(pg.Fiscal, 4) AS INT) <=
              CASE WHEN MONTH(@AsOfDate) > 6 THEN YEAR(@AsOfDate)
                   ELSE YEAR(@AsOfDate) - 1 END
        ORDER BY TRY_CAST(LEFT(pg.Fiscal, 4) AS INT) DESC
    ) pg

    OUTER APPLY
    (
        SELECT TOP (1) rv.*
        FROM dbo.RentalValueGovtShareRates rv
        WHERE rv.CmdId = c.CmdId
          AND rv.BaseId = c.BaseId
          AND rv.ClassId = c.ClassId
          AND rv.Type = 1
          AND (rv.IsDeleted = 0 OR rv.IsDeleted IS NULL)
          AND (rv.Status = 1 OR rv.Status IS NULL)
          AND rv.ApplicableDate <= @AsOfDate
          AND (rv.DeactiveDate IS NULL OR rv.DeactiveDate >= @AsOfDate)
        ORDER BY rv.ApplicableDate DESC
    ) rvRate

    OUTER APPLY
    (
        SELECT TOP (1) gv.*
        FROM dbo.RentalValueGovtShareRates gv
        WHERE gv.CmdId = c.CmdId
          AND gv.BaseId = c.BaseId
          AND gv.ClassId = c.ClassId
          AND gv.Type = 2
          AND (gv.IsDeleted = 0 OR gv.IsDeleted IS NULL)
          AND (gv.Status = 1 OR gv.Status IS NULL)
          AND gv.ApplicableDate <= @AsOfDate
          AND (gv.DeactiveDate IS NULL OR gv.DeactiveDate >= @AsOfDate)
        ORDER BY gv.ApplicableDate DESC
    ) govRate

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

    CROSS APPLY
    (
        SELECT
            IsRentDrivenClass =
                CASE
                    WHEN c.ClassId IN (3, 4, 6) THEN 1
                    WHEN UPPER(REPLACE(LTRIM(RTRIM(ISNULL(cls.Code, ''))), ' ', ''))
                         IN ('C', 'CATC', 'BTS') THEN 1
                    WHEN UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) LIKE '%CAT%C%' THEN 1
                    WHEN UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) = 'C' THEN 1
                    WHEN UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) LIKE '%BTS%' THEN 1
                    ELSE 0
                END,
            AreaBase = COALESCE(pg.Area, c.GroupArea)
    ) clsFlag

    CROSS APPLY
    (
        SELECT
            AreaBase = clsFlag.AreaBase,
            RateBase =
                CASE
                    WHEN clsFlag.IsRentDrivenClass = 1
                     AND clsFlag.AreaBase IS NOT NULL
                     AND clsFlag.AreaBase <> 0
                     AND c.InitialRentPA IS NOT NULL
                    THEN CAST(c.InitialRentPA AS float) / CAST(clsFlag.AreaBase AS float)
                    ELSE pg.Rate
                END,
            IsRentDrivenClass = clsFlag.IsRentDrivenClass
    ) base

    /* ---- Cat C / BTS: resolve GovtShare first (prefer stored contract value) ---- */
    CROSS APPLY
    (
        SELECT
            GovtShare =
                CASE
                    WHEN base.IsRentDrivenClass = 1
                     AND c.GovtShare IS NOT NULL
                    THEN CAST(ROUND(c.GovtShare, 0) AS BIGINT)

                    WHEN base.IsRentDrivenClass = 1
                     AND c.InitialRentPA IS NOT NULL
                     AND govRate.Rate IS NOT NULL
                     /* Do NOT treat 100% Type=2 as govt when RV rate is also 100 —
                        that usually means RV% was saved on the govt-share row by mistake.
                        Fall back to stored/null rather than wiping PAF. */
                     AND NOT (
                            ROUND(govRate.Rate, 4) = 100
                        AND (rvRate.Rate IS NULL OR ROUND(rvRate.Rate, 4) = 100)
                         )
                    THEN CAST(ROUND(c.InitialRentPA * govRate.Rate / 100.0, 0) AS BIGINT)

                    WHEN base.IsRentDrivenClass = 1
                     AND c.InitialRentPA IS NOT NULL
                     AND govRate.Rate IS NOT NULL
                     AND ROUND(govRate.Rate, 4) <> 100
                    THEN CAST(ROUND(c.InitialRentPA * govRate.Rate / 100.0, 0) AS BIGINT)

                    /* Cat A / B (and others) — existing logic */
                    WHEN base.IsRentDrivenClass = 0
                     AND base.AreaBase IS NOT NULL
                     AND base.RateBase IS NOT NULL
                     AND rvRate.Rate IS NOT NULL
                     AND govRate.Rate IS NOT NULL
                     AND ISNULL(govRate.Config, '') NOT IN ('Annual Rent', 'Annual Rate')
                    THEN CAST(ROUND(base.AreaBase * base.RateBase * ROUND(rvRate.Rate, 0) * govRate.Rate / 10000.0, 0) AS BIGINT)

                    WHEN base.IsRentDrivenClass = 0
                     AND govRate.Rate IS NOT NULL
                     AND ISNULL(govRate.Config, '') IN ('Annual Rent', 'Annual Rate')
                    THEN CAST(ROUND(c.InitialRentPA * govRate.Rate / 100.0, 0) AS BIGINT)
                END
    ) g

    CROSS APPLY
    (
        SELECT
            RentalValue =
                CASE
                    WHEN base.IsRentDrivenClass = 1
                     AND c.InitialRentPA IS NOT NULL
                    THEN CAST(
                        ROUND(c.InitialRentPA * ISNULL(rvRate.Rate, 100) / 100.0, 0) AS BIGINT)

                    WHEN base.AreaBase IS NOT NULL
                     AND base.RateBase IS NOT NULL
                     AND rvRate.Rate IS NOT NULL
                    THEN CAST(ROUND(base.AreaBase * base.RateBase * ROUND(rvRate.Rate, 0) / 100.0, 0) AS BIGINT)
                END,

            GovtShare = g.GovtShare,

            PAFShare =
                CASE
                    /* Prefer stored PAF for Cat C / BTS */
                    WHEN base.IsRentDrivenClass = 1
                     AND c.PAFShare IS NOT NULL
                    THEN CAST(ROUND(c.PAFShare, 0) AS BIGINT)

                    WHEN base.IsRentDrivenClass = 1
                     AND c.InitialRentPA IS NOT NULL
                    THEN CAST(
                        CASE
                            WHEN ROUND(c.InitialRentPA, 0) - ISNULL(g.GovtShare, 0) < 0
                            THEN 0
                            ELSE ROUND(c.InitialRentPA, 0) - ISNULL(g.GovtShare, 0)
                        END
                    AS BIGINT)

                    WHEN base.AreaBase IS NOT NULL
                     AND base.RateBase IS NOT NULL
                     AND rvRate.Rate IS NOT NULL
                     AND govRate.Rate IS NOT NULL
                    THEN CAST(
                        ROUND(base.AreaBase * base.RateBase * ROUND(rvRate.Rate, 0) / 100.0, 0)
                        -
                        ROUND(base.AreaBase * base.RateBase * ROUND(rvRate.Rate, 0) * govRate.Rate / 10000.0, 0)
                    AS BIGINT)
                END
    ) calc1

    CROSS APPLY
    (
        SELECT
            AHQRaw = ISNULL(calc1.PAFShare, 0) * ISNULL(sf.AHQRate, 0) / 100.0,
            RACRaw = ISNULL(calc1.PAFShare, 0) * ISNULL(sf.RACRate, 0) / 100.0
    ) r
    CROSS APPLY
    (
        SELECT
            AHQShare = CAST(FLOOR(r.AHQRaw) AS BIGINT),
            RACShare = CAST(FLOOR(r.RACRaw) AS BIGINT),
            BaseShare =
                CAST(
                    ISNULL(calc1.PAFShare, 0)
                    - FLOOR(r.AHQRaw)
                    - FLOOR(r.RACRaw)
                AS BIGINT)
    ) calc2

    OUTER APPLY
    (
        SELECT PaidCount = COUNT(*)
        FROM dbo.ContractInvoicesEdit ci
        WHERE ci.ContractNo = c.ContractNo
          AND ci.SubInvoiceNo IS NULL
          AND ci.IsFinalized = 1
          AND EXISTS
          (
              SELECT 1
              FROM dbo.Receipts r
              WHERE (r.IsDeleted = 0 OR r.IsDeleted IS NULL)
                AND r.FinalizedByAhq IS NOT NULL
                AND r.LinesJson LIKE '%"' + ci.InvoiceNo + '"%'
          )
    ) paid

    OUTER APPLY
    (
        SELECT DueCount = COUNT(*)
        FROM dbo.ContractInvoicesEdit ci
        WHERE ci.ContractNo = c.ContractNo
          AND ci.SubInvoiceNo IS NULL
          AND ci.IsFinalized = 1
    ) inv

    WHERE (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
);
GO
