USE [A1Lands]
GO

/*
  =============================================================================
  DEBUG: fn_BasicContractInfo — one contract, easy to read
  =============================================================================
  How to use:
    1. Set @ContractNo (and optionally @AsOfDate)
    2. Run the whole script
    3. Read the result sets top → bottom

  What each result set means:
    #1 Contract stored values     = what was saved on the contract form
    #2 Class detection            = is this Cat C / BTS (rent-driven) or A/B?
    #3 Rate lookups               = RV%, Govt%, Sharing Formula used as-of date
    #4 Function output            = what fn_BasicContractInfo returns
    #5 Compare & flags            = where it breaks (same case as C786AHQ-P786)

  Common flags:
    GOVT_WIPED_BY_100PCT_RATE  = Type=2 Rate is 100 (often RV% saved on govt row)
    PAF_ZERO_BECAUSE_GOVT_FULL = function GovtShare ≈ InitialRentPA → PAF 0
    STORED_OK_FUNCTION_WRONG   = contract PAF fine, function PAF 0 (as-of bug)
    MISSING_SHARING_FORMULA    = PAF > 0 but AHQ/RAC/Base all 0
    MISSING_INITIAL_RENT       = InitialRentPA null/0
  =============================================================================
*/

DECLARE @ContractNo NVARCHAR(100) = N'C786AHQ-P786';  -- << change this
DECLARE @AsOfDate   DATE          = CAST(GETDATE() AS DATE);  -- << or e.g. '2026-07-22'

/* -------------------------------------------------------------------------- */
/* #1 Contract stored values                                                  */
/* -------------------------------------------------------------------------- */
SELECT
    N'1. STORED ON CONTRACT' AS [Section],
    c.Id,
    c.ContractNo,
    c.CmdId,
    c.BaseId,
    c.ClassId,
    cls.Code AS ClassCode,
    cls.Name AS ClassName,
    c.GrpId,
    c.ContractStartDate,
    c.InitialRentPA,
    c.RentalValue   AS Stored_RentalValue,
    c.GovtShare     AS Stored_GovtShare,
    c.PAFShare      AS Stored_PAFShare,
    c.GroupArea,
    c.GroupRate
FROM dbo.Contracts c
LEFT JOIN dbo.Classes cls
    ON cls.Id = c.ClassId
WHERE c.ContractNo = @ContractNo
  AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL);

/* -------------------------------------------------------------------------- */
/* #2 Class detection (same rules as the function)                            */
/* -------------------------------------------------------------------------- */
SELECT
    N'2. CLASS DETECTION' AS [Section],
    c.ClassId,
    cls.Code,
    cls.Name,
    CASE
        WHEN c.ClassId IN (3, 4, 6) THEN 1
        WHEN UPPER(REPLACE(LTRIM(RTRIM(ISNULL(cls.Code, ''))), ' ', ''))
             IN ('C', 'CATC', 'BTS') THEN 1
        WHEN UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) LIKE '%CAT%C%' THEN 1
        WHEN UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) = 'C' THEN 1
        WHEN UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) LIKE '%BTS%' THEN 1
        ELSE 0
    END AS IsRentDrivenClass,  -- 1 = Cat C/BTS path, 0 = Cat A/B path
    CASE
        WHEN c.ClassId IN (3, 4, 6)
          OR UPPER(REPLACE(LTRIM(RTRIM(ISNULL(cls.Code, ''))), ' ', ''))
             IN ('C', 'CATC', 'BTS')
          OR UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) LIKE '%CAT%C%'
          OR UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) = 'C'
          OR UPPER(LTRIM(RTRIM(ISNULL(cls.Name, '')))) LIKE '%BTS%'
        THEN N'Uses Cat C/BTS (rent / stored) path'
        ELSE N'Uses Cat A/B (Area × RevenueRate) path'
    END AS PathUsed
FROM dbo.Contracts c
LEFT JOIN dbo.Classes cls ON cls.Id = c.ClassId
WHERE c.ContractNo = @ContractNo
  AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL);

/* -------------------------------------------------------------------------- */
/* #3 Rate lookups as of @AsOfDate (same filters as the function)             */
/* -------------------------------------------------------------------------- */
SELECT
    N'3. RATE LOOKUPS' AS [Section],
    c.ContractNo,
    @AsOfDate AS AsOfDate,

    /* Rental Value Rate (Type = 1) */
    rv.Id            AS RV_RateId,
    rv.Rate          AS RV_Rate_Percent,
    rv.ApplicableDate AS RV_ApplicableDate,
    rv.DeactiveDate   AS RV_DeactiveDate,
    rv.Config         AS RV_Config,

    /* Govt Share Rate (Type = 2) */
    gv.Id            AS Govt_RateId,
    gv.Rate          AS Govt_Rate_Percent,
    gv.Config        AS Govt_Config,
    gv.ApplicableDate AS Govt_ApplicableDate,
    gv.DeactiveDate   AS Govt_DeactiveDate,

    /* Sharing Formula */
    sf.AHQRate,
    sf.RACRate,
    sf.BaseRate,
    sf.ApplicableDate AS SharingFormula_ApplicableDate,

    /* Revenue rate from group */
    pg.Rate AS GroupRevenueRate,
    pg.Area AS GroupArea_FromView,
    pg.Fiscal AS GroupFiscal
FROM dbo.Contracts c
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
) rv
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
) gv
OUTER APPLY
(
    SELECT TOP (1) sf.AHQRate, sf.RACRate, sf.BaseRate, sf.ApplicableDate
    FROM dbo.SharingFormulas sf
    WHERE sf.CmdId = c.CmdId
      AND sf.BaseId = c.BaseId
      AND sf.ClassId = c.ClassId
      AND (sf.IsDeleted = 0 OR sf.IsDeleted IS NULL)
      AND (sf.Status = 1 OR sf.Status IS NULL)
    ORDER BY sf.ApplicableDate DESC
) sf
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
WHERE c.ContractNo = @ContractNo
  AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL);

/* -------------------------------------------------------------------------- */
/* #4 Function output                                                         */
/* -------------------------------------------------------------------------- */
SELECT
    N'4. FUNCTION OUTPUT' AS [Section],
    bci.Id,
    bci.ContractNo,
    bci.ContractState,
    bci.AreaBase,
    bci.GroupRate,
    bci.RentalValueRate,
    bci.Auto_GovtSharePercent,
    bci.RentalValue,
    bci.GovtShare   AS Fn_GovtShare,
    bci.PAFShare    AS Fn_PAFShare,
    bci.AHQShare,
    bci.RACShare,
    bci.BaseShare,
    bci.Viability,
    bci.FY,
    bci.RRFY
FROM dbo.Contracts c
CROSS APPLY dbo.fn_BasicContractInfo(@AsOfDate) bci
WHERE c.ContractNo = @ContractNo
  AND bci.Id = c.Id
  AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL);

/* -------------------------------------------------------------------------- */
/* #5 Compare stored vs function + problem flags                              */
/* -------------------------------------------------------------------------- */
SELECT
    N'5. COMPARE & FLAGS' AS [Section],
    c.ContractNo,
    c.InitialRentPA,
    c.GovtShare AS Stored_Govt,
    c.PAFShare  AS Stored_PAF,
    bci.GovtShare AS Fn_Govt,
    bci.PAFShare  AS Fn_PAF,
    bci.AHQShare,
    bci.RACShare,
    bci.BaseShare,

    /* Quick arithmetic check */
    CAST(ROUND(ISNULL(c.InitialRentPA, 0), 0) AS BIGINT)
        - CAST(ROUND(ISNULL(c.GovtShare, 0), 0) AS BIGINT) AS Expected_PAF_From_Stored,

    /* Flags — read these first when debugging */
    CASE
        WHEN c.InitialRentPA IS NULL OR c.InitialRentPA = 0
        THEN N'MISSING_INITIAL_RENT'
        WHEN bci.GovtShare IS NOT NULL
         AND c.InitialRentPA IS NOT NULL
         AND bci.GovtShare >= c.InitialRentPA
         AND ISNULL(bci.PAFShare, 0) = 0
        THEN N'PAF_ZERO_BECAUSE_GOVT_FULL'
        WHEN ISNULL(c.PAFShare, 0) > 0
         AND ISNULL(bci.PAFShare, 0) = 0
        THEN N'STORED_OK_FUNCTION_WRONG'
        WHEN ISNULL(bci.PAFShare, 0) > 0
         AND ISNULL(bci.AHQShare, 0) = 0
         AND ISNULL(bci.RACShare, 0) = 0
         AND ISNULL(bci.BaseShare, 0) = 0
        THEN N'MISSING_SHARING_FORMULA'
        WHEN ISNULL(bci.PAFShare, 0) > 0
         AND ISNULL(bci.AHQShare, 0) = 0
         AND ISNULL(bci.RACShare, 0) = 0
         AND ISNULL(bci.BaseShare, 0) = ISNULL(bci.PAFShare, 0)
        THEN N'PAF_OK_NO_AHQ_RAC_RATES_BASE_GETS_ALL'
        WHEN ISNULL(c.PAFShare, 0) = ISNULL(bci.PAFShare, 0)
         AND ISNULL(c.GovtShare, 0) = ISNULL(bci.GovtShare, 0)
        THEN N'OK_STORED_MATCHES_FUNCTION'
        ELSE N'CHECK_RATES_OR_CLASS_PATH'
    END AS PrimaryFlag,

    CASE
        WHEN gv.Rate IS NOT NULL
         AND ROUND(gv.Rate, 4) = 100
         AND (rv.Rate IS NULL OR ROUND(rv.Rate, 4) = 100)
        THEN N'YES — Type=2 Govt Rate is 100% (often RV% saved on wrong type)'
        ELSE N'No'
    END AS Flag_GovtRateLooksLikeRV100
FROM dbo.Contracts c
CROSS APPLY dbo.fn_BasicContractInfo(@AsOfDate) bci
OUTER APPLY
(
    SELECT TOP (1) rv.Rate
    FROM dbo.RentalValueGovtShareRates rv
    WHERE rv.CmdId = c.CmdId AND rv.BaseId = c.BaseId AND rv.ClassId = c.ClassId
      AND rv.Type = 1
      AND (rv.IsDeleted = 0 OR rv.IsDeleted IS NULL)
      AND (rv.Status = 1 OR rv.Status IS NULL)
      AND rv.ApplicableDate <= @AsOfDate
      AND (rv.DeactiveDate IS NULL OR rv.DeactiveDate >= @AsOfDate)
    ORDER BY rv.ApplicableDate DESC
) rv
OUTER APPLY
(
    SELECT TOP (1) gv.Rate
    FROM dbo.RentalValueGovtShareRates gv
    WHERE gv.CmdId = c.CmdId AND gv.BaseId = c.BaseId AND gv.ClassId = c.ClassId
      AND gv.Type = 2
      AND (gv.IsDeleted = 0 OR gv.IsDeleted IS NULL)
      AND (gv.Status = 1 OR gv.Status IS NULL)
      AND gv.ApplicableDate <= @AsOfDate
      AND (gv.DeactiveDate IS NULL OR gv.DeactiveDate >= @AsOfDate)
    ORDER BY gv.ApplicableDate DESC
) gv
WHERE c.ContractNo = @ContractNo
  AND bci.Id = c.Id
  AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL);
GO
