USE [A1Lands]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

/*
    KPI Overview / dashboard property summary with optional as-of date.
    API: GET /api/Dashboards/property-summary?asOfDate=yyyy-MM-dd
    When @AsOfDate is omitted or NULL, behaviour matches the previous GETDATE()-based logic.
*/
ALTER PROCEDURE [dbo].[GetPropertyDashboardSummary]
    @CmdId INT = NULL,
    @BaseId INT = NULL,
    @AsOfDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EffectiveAsOfDate DATE = ISNULL(@AsOfDate, CAST(GETDATE() AS DATE));

    ---------------------------------------------------
    -- Result Set 1: Property Summary
    -- (property counts / areas; rate views are "latest" snapshots)
    ---------------------------------------------------
    SELECT
        'PropertySummary' AS DataSetName,
        p.CmdId,
        p.BaseId,
        p.ClassId,
        p.uom,
        CAST(SUM(p.area) AS INT) AS Area,
        COUNT(*) AS PropertyCount,
        ROUND(SUM(ISNULL(p.Rate, 0)) / 1000000.0, 6) AS ClassRevenue_Million,
        SUM(COUNT(*)) OVER (PARTITION BY p.CmdId, p.BaseId) AS TotalProperties,
        ROUND(
            SUM(SUM(ISNULL(p.Rate, 0))) OVER (PARTITION BY p.CmdId, p.BaseId) / 1000000.0,
            6
        ) AS TotalRevenue_Million
    FROM dbo.vw_PropertyLatestRates p
    WHERE (@CmdId IS NULL OR p.CmdId = @CmdId)
      AND (@BaseId IS NULL OR p.BaseId = @BaseId)
    GROUP BY p.CmdId, p.BaseId, p.ClassId, p.UOM
    ORDER BY p.CmdId, p.BaseId, p.ClassId;

    ---------------------------------------------------
    -- Result Set 1b: Group Summary
    ---------------------------------------------------
    SELECT
        'GroupSummary' AS DataSetName,
        COUNT(p.grpid) AS PropertyCount,
        p.CmdId,
        p.BaseId,
        p.ClassId,
        p.uom,
        SUM(p.Area) AS area,
        ROUND(SUM(ISNULL(p.Rate, 0)) / 1000000.0, 6) AS ClassRevenue_Million,
        SUM(COUNT(*)) OVER (PARTITION BY p.CmdId, p.BaseId) AS TotalProperties,
        ROUND(
            SUM(SUM(ISNULL(p.Rate, 0))) OVER (PARTITION BY p.CmdId, p.BaseId) / 1000000.0,
            6
        ) AS TotalRevenue_Million
    FROM dbo.vw_PropertyGroupsLatestRates p
    GROUP BY p.CmdId, p.BaseId, p.ClassId, p.UOM
    ORDER BY p.CmdId, p.BaseId, p.ClassId;

    ---------------------------------------------------
    -- Result Set 2: Contracts Summary (as of @EffectiveAsOfDate)
    ---------------------------------------------------
    SELECT
        'ContractsSummary' AS DataSetName,
        c.CmdId,
        c.BaseId,
        COUNT(*) AS TotalContracts,
        SUM(CASE
            WHEN c.Status = 1
             AND c.IsDeleted = 0
             AND c.ContractEndDate >= @EffectiveAsOfDate
            THEN 1 ELSE 0 END) AS ActiveContracts,
        SUM(CASE
            WHEN c.Status = 1
             AND c.IsDeleted = 0
             AND c.ContractEndDate < @EffectiveAsOfDate
            THEN 1 ELSE 0 END) AS ExpiredContracts,
        SUM(CASE
            WHEN c.Status = 1
             AND c.IsDeleted = 0
             AND c.ContractEndDate BETWEEN @EffectiveAsOfDate AND DATEADD(DAY, 30, @EffectiveAsOfDate)
            THEN 1 ELSE 0 END) AS BorderLineContracts,
        SUM(CASE
            WHEN c.Status <> 1
             AND c.IsDeleted = 0
            THEN 1 ELSE 0 END) AS ClosedContracts,
        SUM(CASE
            WHEN c.ApprovalStatus = 1
            THEN 1 ELSE 0 END) AS AHQApprvd,
        SUM(CASE
            WHEN c.ApprovalStatus = 0 OR c.ApprovalStatus IS NULL
            THEN 1 ELSE 0 END) AS AHQPending,
        SUM(CASE
            WHEN c.IsArchive = 1
            THEN 1 ELSE 0 END) AS Inactive,
        SUM(CASE
            WHEN c.IsArchive <> 1
            THEN 1 ELSE 0 END) AS Active,
        SUM(CASE
            WHEN f.Viability = N'Viable'
            THEN 1 ELSE 0 END) AS Viable,
        SUM(CASE
            WHEN f.Viability = N'Unviable'
            THEN 1 ELSE 0 END) AS UnViable
    FROM dbo.Contracts c WITH (NOLOCK)
    INNER JOIN dbo.fn_BasicContractInfo(@EffectiveAsOfDate) f
        ON c.ContractNo = f.ContractNo
    WHERE c.IsDeleted = 0
      AND (@CmdId IS NULL OR c.CmdId = @CmdId)
      AND (@BaseId IS NULL OR c.BaseId = @BaseId)
    GROUP BY c.CmdId, c.BaseId
    ORDER BY c.CmdId, c.BaseId;

    ---------------------------------------------------
    -- Result Set 3: Govt vs PAF Share (rates applicable on/before as-of date)
    ---------------------------------------------------
    ;WITH LatestShareRates AS
    (
        SELECT
            s.ClassId,
            s.CmdId,
            s.BaseId,
            s.Type,
            s.Rate,
            ROW_NUMBER() OVER
            (
                PARTITION BY s.ClassId, s.Type, s.CmdId, s.BaseId
                ORDER BY s.ApplicableDate DESC, s.Id DESC
            ) AS rn
        FROM dbo.RentalValueGovtShareRates s WITH (NOLOCK)
        WHERE s.IsDeleted = 0
          AND s.Status = 1
          AND s.ApplicableDate <= @EffectiveAsOfDate
          AND (@CmdId IS NULL OR s.CmdId = @CmdId)
          AND (@BaseId IS NULL OR s.BaseId = @BaseId)
    )
    SELECT
        'GovtPAFShare' AS DataSetName,
        CmdId,
        BaseId,
        ClassId,
        SUM(CASE
            WHEN Type = 1 AND rn = 1
            THEN Rate ELSE 0 END) AS GovtShare,
        SUM(CASE
            WHEN Type = 2 AND rn = 1
            THEN Rate ELSE 0 END) AS PAFShare
    FROM LatestShareRates
    WHERE rn = 1
    GROUP BY CmdId, BaseId, ClassId
    ORDER BY CmdId, BaseId, ClassId;
END;
GO
