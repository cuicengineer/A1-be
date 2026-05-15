/*
  dbo.sp_GetRunningContractsRentAsOfDate @AsOfDate
  ------------------------------------------------
  Running contract: Status=1, not deleted, ContractStartDate <= @AsOfDate <= ContractEndDate

  Uniform (RiseTermType LIKE 'uniform%')
    Steps = FLOOR( DATEDIFF(MONTH, ContractStartDate, @AsOfDate) / IncreaseIntervalMonths )
    RentPM = InitialRentPM * (1 + IncreaseRatePercent/100) ^ steps
    Note: SQL Server DATEDIFF(MONTH,...) counts month boundaries (e.g. 31 Jan -> 28 Feb = 1).
          Change to day-based logic if finance requires full 30-day buckets.

  Fixed Date (RiseTermType LIKE 'fixed%date%' OR = 'fixed date')
    If @AsOfDate >= CAST(RiseDate AS date): single bump
    RentPM = InitialRentPM * (1 + IncreaseRatePercent/100)

  Output "CalculatedRentPA" = CalculatedRentPM * 12 (annual from monthly; align with your ledger if PA is stored differently).

  ContractRiseTerms (child rows) are not applied here — only contract header fields. Extend with a JOIN
  to ContractRiseTerms if you need stepped % by SequenceNo.

  RevenueRates / PropertyGroupLinking: not used in rent math; join separately for analytics.

  Index idea:
    ON Contracts (Status, IsDeleted, ContractEndDate, ContractStartDate)
    INCLUDE (CmdId, BaseId, GrpId, InitialRentPM, RiseTermType, RiseDate, IncreaseRatePercent, IncreaseIntervalMonths, ...);
*/

CREATE OR ALTER PROCEDURE dbo.sp_GetRunningContractsRentAsOfDate
    @AsOfDate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AsOf date = @AsOfDate;

    ;WITH Running AS (
        SELECT
            c.Id,
            c.ContractNo,
            c.CmdId,
            c.BaseId,
            c.GrpId,
            c.TenantNo,
            c.ContractStartDate,
            c.ContractEndDate,
            c.InitialRentPM,
            c.InitialRentPA,
            c.currentRentPA    AS StoredCurrentRentPA,
            c.RiseTermType,
            c.RiseDate,
            c.IncreaseRatePercent,
            c.IncreaseIntervalMonths,
            rt = LOWER(LTRIM(RTRIM(ISNULL(c.RiseTermType, N'')))),
            monthsElapsed = DATEDIFF(MONTH, c.ContractStartDate, @AsOf)
        FROM dbo.Contracts AS c
        WHERE c.Status = 1
          AND (c.IsDeleted = 0 OR c.IsDeleted IS NULL)
          AND CAST(c.ContractStartDate AS date) <= @AsOf
          AND CAST(c.ContractEndDate   AS date) >= @AsOf
    ),
    Calc AS (
        SELECT
            r.*,
            rateFrac = CAST(ISNULL(r.IncreaseRatePercent, 0) AS decimal(19, 8)) / 100.0,
            uniformSteps =
                CASE
                    WHEN r.rt LIKE N'uniform%'
                         AND NULLIF(r.IncreaseIntervalMonths, 0) IS NOT NULL
                         AND r.IncreaseRatePercent IS NOT NULL
                    THEN r.monthsElapsed / r.IncreaseIntervalMonths
                    ELSE 0
                END,
            fixedBump =
                CASE
                    WHEN (r.rt LIKE N'fixed%date%' OR r.rt = N'fixed date')
                         AND r.RiseDate IS NOT NULL
                         AND @AsOf >= CAST(r.RiseDate AS date)
                         AND r.IncreaseRatePercent IS NOT NULL
                    THEN 1
                    ELSE 0
                END
        FROM Running AS r
    )
    SELECT
        c.Id                              AS ContractId,
        c.ContractNo,
        @AsOf                             AS AsOfDate,
        c.CmdId,
        cmd.Name                          AS CommandName,
        c.BaseId,
        b.Name                            AS BaseName,
        c.GrpId,
        pg.GId                            AS PropertyGroupCode,
        c.TenantNo,
        c.ContractStartDate,
        c.ContractEndDate,
        c.RiseTermType,
        c.RiseDate,
        c.IncreaseIntervalMonths,
        c.IncreaseRatePercent,
        c.InitialRentPM,
        c.InitialRentPA,
        c.StoredCurrentRentPA,
        c.monthsElapsed,
        c.uniformSteps,
        c.fixedBump,
        CalculatedRentPM =
            CAST(
                CASE
                    WHEN c.rt LIKE N'uniform%' THEN
                        c.InitialRentPM * POWER(
                            CAST(1.0 AS decimal(19, 8)) + c.rateFrac,
                            CAST(c.uniformSteps AS float))
                    WHEN (c.rt LIKE N'fixed%date%' OR c.rt = N'fixed date') AND c.fixedBump = 1 THEN
                        c.InitialRentPM * (CAST(1.0 AS decimal(19, 8)) + c.rateFrac)
                    ELSE c.InitialRentPM
                END
            AS decimal(18, 4)),
        CalculatedRentPA =
            CAST(
                CASE
                    WHEN c.rt LIKE N'uniform%' THEN
                        c.InitialRentPM * POWER(
                            CAST(1.0 AS decimal(19, 8)) + c.rateFrac,
                            CAST(c.uniformSteps AS float))
                    WHEN (c.rt LIKE N'fixed%date%' OR c.rt = N'fixed date') AND c.fixedBump = 1 THEN
                        c.InitialRentPM * (CAST(1.0 AS decimal(19, 8)) + c.rateFrac)
                    ELSE c.InitialRentPM
                END * 12
            AS decimal(18, 4))
    FROM Calc AS c
    LEFT JOIN dbo.Commands cmd
        ON cmd.Id = c.CmdId AND (cmd.IsDeleted = 0 OR cmd.IsDeleted IS NULL)
    LEFT JOIN dbo.Bases b
        ON b.Id = c.BaseId AND (b.IsDeleted = 0 OR b.IsDeleted IS NULL)
    LEFT JOIN dbo.PropertyGroups pg
        ON pg.Id = c.GrpId AND (pg.IsDeleted = 0 OR pg.IsDeleted IS NULL)
    ORDER BY c.Id DESC;
END
GO
