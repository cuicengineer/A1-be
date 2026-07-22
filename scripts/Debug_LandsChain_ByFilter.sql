USE [A1Lands]
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
  =============================================================================
  DEBUG LANDS CHAIN — RAC / Base / Class / Property / Contract / Invoice
  =============================================================================
  File: scripts/Debug_LandsChain_ByFilter.sql

  Set ANY combination of filters below (leave unused as NULL), then run all.
  At least one filter is required (safety).

  Examples:
    -- By contract
    @ContractNo = N'C786AHQ-P786'

    -- By invoice
    @InvoiceNo = N'INV-....'

    -- By property number
    @PropertyNo = N'AHQ-786C'

    -- By RAC + Base + Class
    @CmdId = 3, @BaseId = 6, @ClassId = 4
    -- or by name/code
    @RacName = N'AHQ', @BaseName = N'...', @ClassCode = N'C'

  Output sections:
    0  Resolved scope (what matched)
    1  Properties
    2  Property groups + linkings
    3  Revenue rates
    4  Govt share / rental value rates + sharing formula
    5  Contracts (+ as-of shares from fn_BasicContractInfo)
    6  Finalized agreement invoices (ContractInvoicesEdit)
    7  Collections
    8  Receipts (+ lines when possible)
    9  Share distribution (workbook assignments + SP snapshot)
   10  Counts summary
  =============================================================================
*/

/* ===================== FILTERS (edit these) ===================== */
DECLARE @CmdId       INT            = NULL;          -- RAC id
DECLARE @RacName     NVARCHAR(100)  = NULL;          -- Commands.Name or Abb (e.g. N'AHQ')
DECLARE @BaseId      INT            = NULL;
DECLARE @BaseName    NVARCHAR(100)  = NULL;          -- Bases.Name / FullName / Code
DECLARE @ClassId     INT            = NULL;
DECLARE @ClassCode   NVARCHAR(50)   = NULL;          -- Classes.Code or Name (e.g. N'C')
DECLARE @PropertyId  INT            = NULL;          -- RentalProperties.Id
DECLARE @PropertyNo  NVARCHAR(100)  = NULL;          -- RentalProperties.PId
DECLARE @ContractNo  NVARCHAR(100)  = NULL;
DECLARE @InvoiceNo   NVARCHAR(100)  = NULL;          -- header or sub invoice
DECLARE @AsOfDate    DATE           = CAST(GETDATE() AS DATE);
DECLARE @TopRows     INT            = 500;           -- cap per result set
/* ================================================================ */

IF @CmdId IS NULL AND NULLIF(LTRIM(RTRIM(@RacName)), N'') IS NULL
   AND @BaseId IS NULL AND NULLIF(LTRIM(RTRIM(@BaseName)), N'') IS NULL
   AND @ClassId IS NULL AND NULLIF(LTRIM(RTRIM(@ClassCode)), N'') IS NULL
   AND @PropertyId IS NULL AND NULLIF(LTRIM(RTRIM(@PropertyNo)), N'') IS NULL
   AND NULLIF(LTRIM(RTRIM(@ContractNo)), N'') IS NULL
   AND NULLIF(LTRIM(RTRIM(@InvoiceNo)), N'') IS NULL
BEGIN
    RAISERROR(N'Set at least one filter: CmdId/RacName, BaseId/BaseName, ClassId/ClassCode, PropertyId/PropertyNo, ContractNo, or InvoiceNo.', 16, 1);
    RETURN;
END;

/* Resolve RAC / Base / Class ids from names when needed */
IF @CmdId IS NULL AND NULLIF(LTRIM(RTRIM(@RacName)), N'') IS NOT NULL
BEGIN
    SELECT TOP (1) @CmdId = cmd.Id
    FROM dbo.Commands cmd
    WHERE (cmd.IsDeleted = 0 OR cmd.IsDeleted IS NULL)
      AND (
            cmd.Name = @RacName
         OR cmd.Abb = @RacName
         OR UPPER(LTRIM(RTRIM(cmd.Name))) = UPPER(LTRIM(RTRIM(@RacName)))
         OR UPPER(LTRIM(RTRIM(ISNULL(cmd.Abb, N'')))) = UPPER(LTRIM(RTRIM(@RacName)))
          )
    ORDER BY cmd.Id;
END;

IF @BaseId IS NULL AND NULLIF(LTRIM(RTRIM(@BaseName)), N'') IS NOT NULL
BEGIN
    SELECT TOP (1) @BaseId = b.Id
    FROM dbo.Bases b
    WHERE (b.IsDeleted = 0 OR b.IsDeleted IS NULL)
      AND (
            b.Name = @BaseName
         OR b.FullName = @BaseName
         OR b.Code = @BaseName
         OR UPPER(LTRIM(RTRIM(b.Name))) = UPPER(LTRIM(RTRIM(@BaseName)))
         OR UPPER(LTRIM(RTRIM(ISNULL(b.FullName, N'')))) = UPPER(LTRIM(RTRIM(@BaseName)))
         OR UPPER(LTRIM(RTRIM(ISNULL(b.Code, N'')))) = UPPER(LTRIM(RTRIM(@BaseName)))
          )
      AND (@CmdId IS NULL OR b.Cmd = @CmdId)
    ORDER BY b.Id;
END;

IF @ClassId IS NULL AND NULLIF(LTRIM(RTRIM(@ClassCode)), N'') IS NOT NULL
BEGIN
    SELECT TOP (1) @ClassId = cls.Id
    FROM dbo.Classes cls
    WHERE (cls.IsDeleted = 0 OR cls.IsDeleted IS NULL)
      AND (
            cls.Code = @ClassCode
         OR cls.Name = @ClassCode
         OR UPPER(REPLACE(LTRIM(RTRIM(ISNULL(cls.Code, N''))), N' ', N''))
            = UPPER(REPLACE(LTRIM(RTRIM(@ClassCode)), N' ', N''))
         OR UPPER(LTRIM(RTRIM(ISNULL(cls.Name, N'')))) = UPPER(LTRIM(RTRIM(@ClassCode)))
          )
    ORDER BY cls.Id;
END;

/* Seed scope from filters */
IF OBJECT_ID('tempdb..#SeedContracts') IS NOT NULL DROP TABLE #SeedContracts;
IF OBJECT_ID('tempdb..#SeedProps') IS NOT NULL DROP TABLE #SeedProps;
IF OBJECT_ID('tempdb..#SeedGroups') IS NOT NULL DROP TABLE #SeedGroups;
IF OBJECT_ID('tempdb..#ScopeContracts') IS NOT NULL DROP TABLE #ScopeContracts;
IF OBJECT_ID('tempdb..#ScopeProps') IS NOT NULL DROP TABLE #ScopeProps;
IF OBJECT_ID('tempdb..#ScopeGroups') IS NOT NULL DROP TABLE #ScopeGroups;

CREATE TABLE #SeedContracts (ContractId INT NOT NULL PRIMARY KEY);
CREATE TABLE #SeedProps     (PropertyId INT NOT NULL PRIMARY KEY);
CREATE TABLE #SeedGroups    (GroupId INT NOT NULL PRIMARY KEY);

/* From invoice */
IF NULLIF(LTRIM(RTRIM(@InvoiceNo)), N'') IS NOT NULL
BEGIN
    INSERT INTO #SeedContracts (ContractId)
    SELECT DISTINCT cie.ContractId
    FROM dbo.ContractInvoicesEdit cie
    WHERE (cie.IsDeleted = 0 OR cie.IsDeleted IS NULL)
      AND (
            cie.InvoiceNo = @InvoiceNo
         OR cie.SubInvoiceNo = @InvoiceNo
          )
      AND cie.ContractId > 0;

    INSERT INTO #SeedContracts (ContractId)
    SELECT DISTINCT COALESCE(ce.ContractId, c.Id)
    FROM dbo.CollectionEntries ce
    LEFT JOIN dbo.Contracts c
        ON c.ContractNo = ce.ContractNo
       AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
    WHERE (ce.IsDeleted = 0 OR ce.IsDeleted IS NULL)
      AND ce.InvoiceNo = @InvoiceNo
      AND COALESCE(ce.ContractId, c.Id) IS NOT NULL
      AND NOT EXISTS (
            SELECT 1 FROM #SeedContracts s WHERE s.ContractId = COALESCE(ce.ContractId, c.Id)
          );
END;

/* From contract no */
IF NULLIF(LTRIM(RTRIM(@ContractNo)), N'') IS NOT NULL
BEGIN
    INSERT INTO #SeedContracts (ContractId)
    SELECT c.Id
    FROM dbo.Contracts c
    WHERE (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
      AND c.ContractNo = @ContractNo
      AND NOT EXISTS (SELECT 1 FROM #SeedContracts s WHERE s.ContractId = c.Id);
END;

/* From property id / no */
IF @PropertyId IS NOT NULL
    INSERT INTO #SeedProps (PropertyId) VALUES (@PropertyId);

IF NULLIF(LTRIM(RTRIM(@PropertyNo)), N'') IS NOT NULL
BEGIN
    INSERT INTO #SeedProps (PropertyId)
    SELECT rp.Id
    FROM dbo.RentalProperties rp
    WHERE (rp.IsDeleted = 0 OR rp.IsDeleted IS NULL)
      AND rp.PId = @PropertyNo
      AND NOT EXISTS (SELECT 1 FROM #SeedProps s WHERE s.PropertyId = rp.Id);
END;

/* Expand: contract → group → properties; property → groups → contracts; RAC/Base/Class filters */
CREATE TABLE #ScopeContracts (ContractId INT NOT NULL PRIMARY KEY);
CREATE TABLE #ScopeProps     (PropertyId INT NOT NULL PRIMARY KEY);
CREATE TABLE #ScopeGroups    (GroupId INT NOT NULL PRIMARY KEY);

INSERT INTO #ScopeContracts (ContractId)
SELECT ContractId FROM #SeedContracts;

INSERT INTO #ScopeProps (PropertyId)
SELECT PropertyId FROM #SeedProps;

/* Contracts from seed → groups */
INSERT INTO #ScopeGroups (GroupId)
SELECT DISTINCT c.GrpId
FROM dbo.Contracts c
INNER JOIN #ScopeContracts sc ON sc.ContractId = c.Id
WHERE c.GrpId > 0
  AND NOT EXISTS (SELECT 1 FROM #ScopeGroups g WHERE g.GroupId = c.GrpId);

/* Properties from seed groups */
INSERT INTO #ScopeProps (PropertyId)
SELECT DISTINCT l.PropId
FROM dbo.PropertyGroupLinkings l
INNER JOIN #ScopeGroups g ON g.GroupId = l.GrpId
WHERE (l.IsDeleted = 0 OR l.IsDeleted IS NULL)
  AND NOT EXISTS (SELECT 1 FROM #ScopeProps p WHERE p.PropertyId = l.PropId);

/* Groups from seed properties */
INSERT INTO #ScopeGroups (GroupId)
SELECT DISTINCT l.GrpId
FROM dbo.PropertyGroupLinkings l
INNER JOIN #ScopeProps p ON p.PropertyId = l.PropId
WHERE (l.IsDeleted = 0 OR l.IsDeleted IS NULL)
  AND NOT EXISTS (SELECT 1 FROM #ScopeGroups g WHERE g.GroupId = l.GrpId);

/* Contracts for those groups */
INSERT INTO #ScopeContracts (ContractId)
SELECT DISTINCT c.Id
FROM dbo.Contracts c
INNER JOIN #ScopeGroups g ON g.GroupId = c.GrpId
WHERE (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
  AND NOT EXISTS (SELECT 1 FROM #ScopeContracts sc WHERE sc.ContractId = c.Id);

/* If only RAC/Base/Class filters (no seed ids), fill from those scopes */
IF NOT EXISTS (SELECT 1 FROM #ScopeContracts)
   AND NOT EXISTS (SELECT 1 FROM #ScopeProps)
   AND NOT EXISTS (SELECT 1 FROM #ScopeGroups)
BEGIN
    INSERT INTO #ScopeProps (PropertyId)
    SELECT rp.Id
    FROM dbo.RentalProperties rp
    WHERE (rp.IsDeleted = 0 OR rp.IsDeleted IS NULL)
      AND (@CmdId IS NULL OR rp.CmdId = @CmdId)
      AND (@BaseId IS NULL OR rp.BaseId = @BaseId)
      AND (@ClassId IS NULL OR rp.ClassId = @ClassId);

    INSERT INTO #ScopeGroups (GroupId)
    SELECT pg.Id
    FROM dbo.PropertyGroups pg
    WHERE (pg.IsDeleted = 0 OR pg.IsDeleted IS NULL)
      AND (@CmdId IS NULL OR pg.CmdId = @CmdId)
      AND (@BaseId IS NULL OR pg.BaseId = @BaseId)
      AND (@ClassId IS NULL OR pg.ClassId = @ClassId);

    INSERT INTO #ScopeContracts (ContractId)
    SELECT c.Id
    FROM dbo.Contracts c
    WHERE (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
      AND (@CmdId IS NULL OR c.CmdId = @CmdId)
      AND (@BaseId IS NULL OR c.BaseId = @BaseId)
      AND (@ClassId IS NULL OR c.ClassId = @ClassId);
END
ELSE
BEGIN
    /* Apply RAC/Base/Class as additional narrowing when seed exists */
    IF @CmdId IS NOT NULL OR @BaseId IS NOT NULL OR @ClassId IS NOT NULL
    BEGIN
        DELETE sc
        FROM #ScopeContracts sc
        INNER JOIN dbo.Contracts c ON c.Id = sc.ContractId
        WHERE (@CmdId IS NOT NULL AND c.CmdId <> @CmdId)
           OR (@BaseId IS NOT NULL AND c.BaseId <> @BaseId)
           OR (@ClassId IS NOT NULL AND c.ClassId <> @ClassId);

        DELETE sp
        FROM #ScopeProps sp
        INNER JOIN dbo.RentalProperties rp ON rp.Id = sp.PropertyId
        WHERE (@CmdId IS NOT NULL AND rp.CmdId <> @CmdId)
           OR (@BaseId IS NOT NULL AND rp.BaseId <> @BaseId)
           OR (@ClassId IS NOT NULL AND rp.ClassId <> @ClassId);

        DELETE sg
        FROM #ScopeGroups sg
        INNER JOIN dbo.PropertyGroups pg ON pg.Id = sg.GroupId
        WHERE (@CmdId IS NOT NULL AND pg.CmdId <> @CmdId)
           OR (@BaseId IS NOT NULL AND pg.BaseId <> @BaseId)
           OR (@ClassId IS NOT NULL AND pg.ClassId <> @ClassId);
    END;
END;

/* Re-expand linkings after narrowing */
INSERT INTO #ScopeProps (PropertyId)
SELECT DISTINCT l.PropId
FROM dbo.PropertyGroupLinkings l
INNER JOIN #ScopeGroups g ON g.GroupId = l.GrpId
WHERE (l.IsDeleted = 0 OR l.IsDeleted IS NULL)
  AND NOT EXISTS (SELECT 1 FROM #ScopeProps p WHERE p.PropertyId = l.PropId);

INSERT INTO #ScopeGroups (GroupId)
SELECT DISTINCT l.GrpId
FROM dbo.PropertyGroupLinkings l
INNER JOIN #ScopeProps p ON p.PropertyId = l.PropId
WHERE (l.IsDeleted = 0 OR l.IsDeleted IS NULL)
  AND NOT EXISTS (SELECT 1 FROM #ScopeGroups g WHERE g.GroupId = l.GrpId);

INSERT INTO #ScopeContracts (ContractId)
SELECT DISTINCT c.Id
FROM dbo.Contracts c
INNER JOIN #ScopeGroups g ON g.GroupId = c.GrpId
WHERE (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
  AND (@CmdId IS NULL OR c.CmdId = @CmdId)
  AND (@BaseId IS NULL OR c.BaseId = @BaseId)
  AND (@ClassId IS NULL OR c.ClassId = @ClassId)
  AND NOT EXISTS (SELECT 1 FROM #ScopeContracts sc WHERE sc.ContractId = c.Id);

/* ===================== 0. RESOLVED SCOPE ===================== */
SELECT
    N'0. FILTERS & SCOPE' AS [Section],
    @AsOfDate AS AsOfDate,
    @CmdId AS CmdId,
    @RacName AS RacNameFilter,
    cmd.Name AS RacName,
    cmd.Abb AS RacAbb,
    @BaseId AS BaseId,
    @BaseName AS BaseNameFilter,
    b.Name AS BaseName,
    b.FullName AS BaseFullName,
    @ClassId AS ClassId,
    @ClassCode AS ClassCodeFilter,
    cls.Code AS ClassCode,
    cls.Name AS ClassName,
    @PropertyId AS PropertyIdFilter,
    @PropertyNo AS PropertyNoFilter,
    @ContractNo AS ContractNoFilter,
    @InvoiceNo AS InvoiceNoFilter,
    (SELECT COUNT(*) FROM #ScopeProps) AS ScopePropertyCount,
    (SELECT COUNT(*) FROM #ScopeGroups) AS ScopeGroupCount,
    (SELECT COUNT(*) FROM #ScopeContracts) AS ScopeContractCount
FROM (SELECT 1 AS x) dummy
LEFT JOIN dbo.Commands cmd ON cmd.Id = @CmdId
LEFT JOIN dbo.Bases b ON b.Id = @BaseId
LEFT JOIN dbo.Classes cls ON cls.Id = @ClassId;

/* ===================== 1. PROPERTIES ===================== */
SELECT TOP (@TopRows)
    N'1. PROPERTIES' AS [Section],
    rp.Id,
    rp.PId AS PropertyNo,
    rp.CmdId,
    cmd.Name AS RacName,
    rp.BaseId,
    b.Name AS BaseName,
    rp.ClassId,
    cls.Code AS ClassCode,
    cls.Name AS ClassName,
    rp.UoM,
    rp.Area,
    rp.Location,
    rp.PropertyType,
    rp.Status,
    rp.IsDeleted,
    rp.ActionDate
FROM #ScopeProps sp
INNER JOIN dbo.RentalProperties rp ON rp.Id = sp.PropertyId
LEFT JOIN dbo.Commands cmd ON cmd.Id = rp.CmdId
LEFT JOIN dbo.Bases b ON b.Id = rp.BaseId
LEFT JOIN dbo.Classes cls ON cls.Id = rp.ClassId
ORDER BY rp.Id DESC;

/* ===================== 2. GROUPS + LINKINGS ===================== */
SELECT TOP (@TopRows)
    N'2a. PROPERTY GROUPS' AS [Section],
    pg.Id AS GroupId,
    pg.GId AS GroupNo,
    pg.CmdId,
    cmd.Name AS RacName,
    pg.BaseId,
    b.Name AS BaseName,
    pg.ClassId,
    cls.Code AS ClassCode,
    pg.UoM,
    pg.Area,
    pg.Rate,
    pg.Location,
    pg.Status,
    pg.IsDeleted
FROM #ScopeGroups sg
INNER JOIN dbo.PropertyGroups pg ON pg.Id = sg.GroupId
LEFT JOIN dbo.Commands cmd ON cmd.Id = pg.CmdId
LEFT JOIN dbo.Bases b ON b.Id = pg.BaseId
LEFT JOIN dbo.Classes cls ON cls.Id = pg.ClassId
ORDER BY pg.Id DESC;

SELECT TOP (@TopRows)
    N'2b. GROUP LINKINGS' AS [Section],
    l.Id AS LinkingId,
    l.GrpId AS GroupId,
    pg.GId AS GroupNo,
    l.PropId AS PropertyId,
    rp.PId AS PropertyNo,
    l.Area AS LinkingArea,
    l.Price,
    l.Status,
    l.IsDeleted
FROM #ScopeGroups sg
INNER JOIN dbo.PropertyGroupLinkings l ON l.GrpId = sg.GroupId
LEFT JOIN dbo.PropertyGroups pg ON pg.Id = l.GrpId
LEFT JOIN dbo.RentalProperties rp ON rp.Id = l.PropId
WHERE (l.IsDeleted = 0 OR l.IsDeleted IS NULL)
ORDER BY l.GrpId, l.PropId;

/* ===================== 3. REVENUE RATES ===================== */
SELECT TOP (@TopRows)
    N'3. REVENUE RATES' AS [Section],
    rr.Id,
    rr.PropertyId,
    rp.PId AS PropertyNo,
    rr.CmdId,
    rr.BaseId,
    rr.Rate,
    rr.Fiscal,
    rr.ApplicableDate,
    rr.DeactiveDate,
    rr.RateScope,
    rr.Status,
    rr.IsDeleted
FROM dbo.RevenueRates rr
INNER JOIN #ScopeProps sp ON sp.PropertyId = rr.PropertyId
LEFT JOIN dbo.RentalProperties rp ON rp.Id = rr.PropertyId
WHERE (rr.IsDeleted = 0 OR rr.IsDeleted IS NULL)
ORDER BY rr.PropertyId, rr.ApplicableDate DESC, rr.Id DESC;

/* ===================== 4. GOVT / RV RATES + SHARING FORMULA ===================== */
SELECT TOP (@TopRows)
    N'4a. RENTAL VALUE RATES (Type=1)' AS [Section],
    rv.Id,
    rv.CmdId,
    rv.BaseId,
    rv.ClassId,
    cls.Code AS ClassCode,
    rv.Type,
    rv.Rate,
    rv.Config,
    rv.ApplicableDate,
    rv.DeactiveDate,
    rv.Status
FROM dbo.RentalValueGovtShareRates rv
LEFT JOIN dbo.Classes cls ON cls.Id = rv.ClassId
WHERE (rv.IsDeleted = 0 OR rv.IsDeleted IS NULL)
  AND rv.Type = 1
  AND (
        EXISTS (
            SELECT 1
            FROM #ScopeContracts sc
            INNER JOIN dbo.Contracts c ON c.Id = sc.ContractId
            WHERE c.CmdId = rv.CmdId AND c.BaseId = rv.BaseId AND c.ClassId = rv.ClassId
        )
     OR EXISTS (
            SELECT 1
            FROM #ScopeProps sp
            INNER JOIN dbo.RentalProperties rp ON rp.Id = sp.PropertyId
            WHERE rp.CmdId = rv.CmdId AND rp.BaseId = rv.BaseId AND rp.ClassId = rv.ClassId
        )
     OR EXISTS (
            SELECT 1
            FROM #ScopeGroups sg
            INNER JOIN dbo.PropertyGroups pg ON pg.Id = sg.GroupId
            WHERE pg.CmdId = rv.CmdId AND pg.BaseId = rv.BaseId AND pg.ClassId = rv.ClassId
        )
      )
ORDER BY rv.CmdId, rv.BaseId, rv.ClassId, rv.ApplicableDate DESC;

SELECT TOP (@TopRows)
    N'4b. GOVT SHARE RATES (Type=2)' AS [Section],
    gv.Id,
    gv.CmdId,
    gv.BaseId,
    gv.ClassId,
    cls.Code AS ClassCode,
    gv.Type,
    gv.Rate,
    gv.Config,
    gv.ApplicableDate,
    gv.DeactiveDate,
    gv.Status
FROM dbo.RentalValueGovtShareRates gv
LEFT JOIN dbo.Classes cls ON cls.Id = gv.ClassId
WHERE (gv.IsDeleted = 0 OR gv.IsDeleted IS NULL)
  AND gv.Type = 2
  AND (
        EXISTS (
            SELECT 1
            FROM #ScopeContracts sc
            INNER JOIN dbo.Contracts c ON c.Id = sc.ContractId
            WHERE c.CmdId = gv.CmdId AND c.BaseId = gv.BaseId AND c.ClassId = gv.ClassId
        )
     OR EXISTS (
            SELECT 1
            FROM #ScopeProps sp
            INNER JOIN dbo.RentalProperties rp ON rp.Id = sp.PropertyId
            WHERE rp.CmdId = gv.CmdId AND rp.BaseId = gv.BaseId AND rp.ClassId = gv.ClassId
        )
      )
ORDER BY gv.CmdId, gv.BaseId, gv.ClassId, gv.ApplicableDate DESC;

SELECT TOP (@TopRows)
    N'4c. SHARING FORMULAS' AS [Section],
    sf.Id,
    sf.CmdId,
    sf.BaseId,
    sf.ClassId,
    cls.Code AS ClassCode,
    sf.AHQRate,
    sf.RACRate,
    sf.BaseRate,
    sf.ApplicableDate,
    sf.DeactiveDate,
    sf.Status,
    sf.Description
FROM dbo.SharingFormulas sf
LEFT JOIN dbo.Classes cls ON cls.Id = sf.ClassId
WHERE (sf.IsDeleted = 0 OR sf.IsDeleted IS NULL)
  AND (
        EXISTS (
            SELECT 1
            FROM #ScopeContracts sc
            INNER JOIN dbo.Contracts c ON c.Id = sc.ContractId
            WHERE c.CmdId = sf.CmdId AND c.BaseId = sf.BaseId AND c.ClassId = sf.ClassId
        )
     OR EXISTS (
            SELECT 1
            FROM #ScopeProps sp
            INNER JOIN dbo.RentalProperties rp ON rp.Id = sp.PropertyId
            WHERE rp.CmdId = sf.CmdId AND rp.BaseId = sf.BaseId AND rp.ClassId = sf.ClassId
        )
      )
ORDER BY sf.CmdId, sf.BaseId, sf.ClassId, sf.ApplicableDate DESC;

/* ===================== 5. CONTRACTS + AS-OF SHARES ===================== */
SELECT TOP (@TopRows)
    N'5. CONTRACTS (+ as-of shares)' AS [Section],
    c.Id,
    c.ContractNo,
    c.CmdId,
    cmd.Name AS RacName,
    c.BaseId,
    b.Name AS BaseName,
    c.ClassId,
    cls.Code AS ClassCode,
    c.GrpId,
    pg.GId AS GroupNo,
    c.TenantNo,
    c.BusinessName,
    c.ContractStartDate,
    c.ContractEndDate,
    c.InitialRentPA,
    c.RentalValue AS Stored_RentalValue,
    c.GovtShare AS Stored_GovtShare,
    c.PAFShare AS Stored_PAFShare,
    bci.RentalValue AS Fn_RentalValue,
    bci.GovtShare AS Fn_GovtShare,
    bci.PAFShare AS Fn_PAFShare,
    bci.AHQShare,
    bci.RACShare,
    bci.BaseShare,
    bci.ContractState,
    bci.Viability,
    c.Status,
    c.ApprovalStatus,
    c.IsArchive
FROM #ScopeContracts sc
INNER JOIN dbo.Contracts c ON c.Id = sc.ContractId
LEFT JOIN dbo.Commands cmd ON cmd.Id = c.CmdId
LEFT JOIN dbo.Bases b ON b.Id = c.BaseId
LEFT JOIN dbo.Classes cls ON cls.Id = c.ClassId
LEFT JOIN dbo.PropertyGroups pg ON pg.Id = c.GrpId
OUTER APPLY (
    SELECT TOP (1) x.*
    FROM dbo.fn_BasicContractInfo(@AsOfDate) x
    WHERE x.Id = c.Id
) bci
WHERE (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
ORDER BY c.Id DESC;

/* ===================== 6. FINALIZED AGREEMENT INVOICES ===================== */
SELECT TOP (@TopRows)
    N'6. FINALIZED AGREEMENT INVOICES' AS [Section],
    cie.Id,
    cie.ContractId,
    cie.ContractNo,
    cie.InvoiceNo,
    cie.SubInvoiceNo,
    CASE WHEN cie.SubInvoiceNo IS NULL THEN N'HEADER' ELSE N'LINE' END AS RowType,
    cie.IsFinalized,
    cie.PeriodStart,
    cie.PeriodEnd,
    cie.DueDate,
    cie.TotalRent,
    cie.AmountReceivable,
    cie.AmountReceived,
    cie.AmountPending,
    cie.InvoiceStatus,
    cie.CmdId,
    cie.BaseId,
    cie.ClassId,
    cie.BusinessName,
    cie.ItemwithCode,
    cie.Description,
    cie.IsLocked,
    cie.IsDeleted
FROM dbo.ContractInvoicesEdit cie
INNER JOIN #ScopeContracts sc ON sc.ContractId = cie.ContractId
WHERE (cie.IsDeleted = 0 OR cie.IsDeleted IS NULL)
  AND (
        @InvoiceNo IS NULL
     OR cie.InvoiceNo = @InvoiceNo
     OR cie.SubInvoiceNo = @InvoiceNo
      )
ORDER BY cie.ContractNo, cie.InvoiceNo, cie.SubInvoiceNo, cie.Id;

/* ===================== 7. COLLECTIONS ===================== */
SELECT TOP (@TopRows)
    N'7. COLLECTIONS' AS [Section],
    ce.Id,
    ce.ContractId,
    ce.ContractNo,
    ce.InvoiceNo,
    ce.TenantNo,
    ce.TenantBusiness,
    ce.Status,
    ce.Amount,
    ce.ReceivableAmount,
    ce.DueAmount,
    ce.BalanceAmount,
    ce.CollectionDate,
    ce.VrNo,
    ce.VrDate,
    ce.ReceiptId,
    ce.ClassId,
    ce.Remarks,
    ce.IsDeleted
FROM dbo.CollectionEntries ce
WHERE (ce.IsDeleted = 0 OR ce.IsDeleted IS NULL)
  AND (
        EXISTS (
            SELECT 1 FROM #ScopeContracts sc
            WHERE sc.ContractId = ce.ContractId
               OR EXISTS (
                    SELECT 1 FROM dbo.Contracts c
                    WHERE c.Id = sc.ContractId AND c.ContractNo = ce.ContractNo
                  )
        )
     OR (@InvoiceNo IS NOT NULL AND ce.InvoiceNo = @InvoiceNo)
      )
ORDER BY COALESCE(ce.VrDate, ce.CollectionDate) DESC, ce.Id DESC;

/* ===================== 8. RECEIPTS ===================== */
SELECT TOP (@TopRows)
    N'8a. RECEIPTS' AS [Section],
    r.Id,
    r.[Date],
    r.Reference,
    r.VrNo,
    r.RecordType,
    r.PayeeName,
    r.PayeePartyCode,
    r.GrandTotal,
    r.FinalizedByAhq,
    r.Description,
    r.IsDeleted,
    r.ActionDate
FROM dbo.Receipts r
WHERE (r.IsDeleted = 0 OR r.IsDeleted IS NULL)
  AND (
        EXISTS (
            SELECT 1
            FROM dbo.CollectionEntries ce
            INNER JOIN #ScopeContracts sc
                ON sc.ContractId = ce.ContractId
                OR EXISTS (
                    SELECT 1 FROM dbo.Contracts c
                    WHERE c.Id = sc.ContractId AND c.ContractNo = ce.ContractNo
                )
            WHERE ce.ReceiptId = r.Id
              AND (ce.IsDeleted = 0 OR ce.IsDeleted IS NULL)
        )
     OR EXISTS (
            SELECT 1
            FROM dbo.ReceiptLines rl
            INNER JOIN #ScopeContracts sc
                ON TRY_CAST(rl.ContractId AS INT) = sc.ContractId
                OR EXISTS (
                    SELECT 1 FROM dbo.Contracts c
                    WHERE c.Id = sc.ContractId AND c.ContractNo = rl.ContractNo
                )
            WHERE rl.ReceiptId = r.Id
              AND (rl.IsDeleted = 0 OR rl.IsDeleted IS NULL)
        )
     OR (
            @InvoiceNo IS NOT NULL
        AND (
                EXISTS (
                    SELECT 1 FROM dbo.CollectionEntries ce
                    WHERE ce.ReceiptId = r.Id AND ce.InvoiceNo = @InvoiceNo
                )
             OR EXISTS (
                    SELECT 1 FROM dbo.ReceiptLines rl
                    WHERE rl.ReceiptId = r.Id AND rl.InvoiceNo = @InvoiceNo
                )
            )
         )
      )
ORDER BY r.[Date] DESC, r.Id DESC;

SELECT TOP (@TopRows)
    N'8b. RECEIPT LINES' AS [Section],
    rl.Id,
    rl.ReceiptId,
    rl.[LineNo],
    rl.ContractId,
    rl.ContractNo,
    rl.InvoiceNo,
    rl.CollectionEntryId,
    rl.Amount,
    rl.Total,
    rl.PartyName,
    rl.Account,
    rl.RacId,
    rl.BaseId,
    r.Reference AS ReceiptReference,
    r.FinalizedByAhq
FROM dbo.ReceiptLines rl
INNER JOIN dbo.Receipts r ON r.Id = rl.ReceiptId
WHERE (rl.IsDeleted = 0 OR rl.IsDeleted IS NULL)
  AND (r.IsDeleted = 0 OR r.IsDeleted IS NULL)
  AND (
        EXISTS (
            SELECT 1
            FROM #ScopeContracts sc
            WHERE TRY_CAST(rl.ContractId AS INT) = sc.ContractId
               OR EXISTS (
                    SELECT 1
                    FROM dbo.Contracts c
                    WHERE c.Id = sc.ContractId
                      AND c.ContractNo = rl.ContractNo
                  )
        )
     OR (@InvoiceNo IS NOT NULL AND rl.InvoiceNo = @InvoiceNo)
      )
ORDER BY rl.ReceiptId DESC, rl.[LineNo];

/* ===================== 9. SHARE DISTRIBUTION ===================== */
SELECT TOP (@TopRows)
    N'9a. SHARE DISTRIBUTION WORKBOOK ASSIGNMENTS' AS [Section],
    wb.Id,
    wb.ContractId,
    c.ContractNo,
    wb.WorkbookNo,
    wb.WorkbookSerial,
    wb.WorkbookCreatedDate,
    wb.IsDeleted,
    wb.ActionDate
FROM dbo.ShareDistributionWorkbookAssignments wb
INNER JOIN #ScopeContracts sc ON sc.ContractId = wb.ContractId
LEFT JOIN dbo.Contracts c ON c.Id = wb.ContractId
WHERE (wb.IsDeleted = 0 OR wb.IsDeleted IS NULL)
ORDER BY wb.WorkbookCreatedDate DESC, wb.WorkbookSerial DESC;

/*
  9b — lightweight share-distribution view from collections (no fragile INSERT-EXEC).
  For full SP output, run separately:
    EXEC dbo.sp_GetShareDistributionFromFinalizedReceipts @AsOfDate = 'yyyy-mm-dd';
*/
SELECT TOP (@TopRows)
    N'9b. SHARE DIST (from Received collections)' AS [Section],
    c.Id AS ContractId,
    c.ContractNo,
    cmd.Name AS RacName,
    b.Name AS BaseName,
    cls.Code AS ClassCode,
    wb.WorkbookNo,
    COUNT(ce.Id) AS ReceivedCollectionLines,
    SUM(CAST(ISNULL(ce.Amount, 0) AS DECIMAL(18, 4))) AS TotalReceivedAmount,
    MIN(CAST(COALESCE(ce.VrDate, ce.CollectionDate) AS DATE)) AS FirstCollectionDate,
    MAX(CAST(COALESCE(ce.VrDate, ce.CollectionDate) AS DATE)) AS LastCollectionDate,
    bci.PAFShare AS Fn_PAFShare,
    bci.AHQShare AS Fn_AHQShare,
    bci.RACShare AS Fn_RACShare,
    bci.BaseShare AS Fn_BaseShare
FROM #ScopeContracts sc
INNER JOIN dbo.Contracts c ON c.Id = sc.ContractId
LEFT JOIN dbo.Commands cmd ON cmd.Id = c.CmdId
LEFT JOIN dbo.Bases b ON b.Id = c.BaseId
LEFT JOIN dbo.Classes cls ON cls.Id = c.ClassId
LEFT JOIN dbo.ShareDistributionWorkbookAssignments wb
    ON wb.ContractId = c.Id
   AND (wb.IsDeleted = 0 OR wb.IsDeleted IS NULL)
LEFT JOIN dbo.CollectionEntries ce
    ON (ce.ContractId = c.Id OR ce.ContractNo = c.ContractNo)
   AND (ce.IsDeleted = 0 OR ce.IsDeleted IS NULL)
   AND LTRIM(RTRIM(UPPER(ISNULL(ce.Status, N'')))) = N'RECEIVED'
   AND ISNULL(ce.Amount, 0) > 0
OUTER APPLY (
    SELECT TOP (1) x.PAFShare, x.AHQShare, x.RACShare, x.BaseShare
    FROM dbo.fn_BasicContractInfo(@AsOfDate) x
    WHERE x.Id = c.Id
) bci
WHERE (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
GROUP BY
    c.Id, c.ContractNo, cmd.Name, b.Name, cls.Code, wb.WorkbookNo,
    bci.PAFShare, bci.AHQShare, bci.RACShare, bci.BaseShare
ORDER BY c.ContractNo;

/* ===================== 10. COUNTS ===================== */
SELECT
    N'10. COUNTS SUMMARY' AS [Section],
    (SELECT COUNT(*) FROM #ScopeProps) AS Properties,
    (SELECT COUNT(*) FROM #ScopeGroups) AS Groups,
    (SELECT COUNT(*) FROM dbo.PropertyGroupLinkings l
        INNER JOIN #ScopeGroups g ON g.GroupId = l.GrpId
        WHERE l.IsDeleted = 0 OR l.IsDeleted IS NULL) AS GroupLinkings,
    (SELECT COUNT(*) FROM dbo.RevenueRates rr
        INNER JOIN #ScopeProps p ON p.PropertyId = rr.PropertyId
        WHERE rr.IsDeleted = 0 OR rr.IsDeleted IS NULL) AS RevenueRates,
    (SELECT COUNT(*) FROM #ScopeContracts) AS Contracts,
    (SELECT COUNT(*) FROM dbo.ContractInvoicesEdit cie
        INNER JOIN #ScopeContracts sc ON sc.ContractId = cie.ContractId
        WHERE (cie.IsDeleted = 0 OR cie.IsDeleted IS NULL)
          AND cie.IsFinalized = 1
          AND cie.SubInvoiceNo IS NULL) AS FinalizedInvoiceHeaders,
    (SELECT COUNT(*) FROM dbo.CollectionEntries ce
        WHERE (ce.IsDeleted = 0 OR ce.IsDeleted IS NULL)
          AND EXISTS (
                SELECT 1 FROM #ScopeContracts sc
                WHERE sc.ContractId = ce.ContractId
                   OR EXISTS (SELECT 1 FROM dbo.Contracts c WHERE c.Id = sc.ContractId AND c.ContractNo = ce.ContractNo)
              )) AS Collections,
    (SELECT COUNT(*) FROM dbo.ShareDistributionWorkbookAssignments wb
        INNER JOIN #ScopeContracts sc ON sc.ContractId = wb.ContractId
        WHERE wb.IsDeleted = 0 OR wb.IsDeleted IS NULL) AS ShareWorkbookAssignments;

/* Cleanup */
DROP TABLE #SeedContracts;
DROP TABLE #SeedProps;
DROP TABLE #SeedGroups;
DROP TABLE #ScopeContracts;
DROP TABLE #ScopeProps;
DROP TABLE #ScopeGroups;
GO
