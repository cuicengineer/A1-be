USE [A1Lands]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
    Grid list for Institutional Bank Accounts (BankAccounts) with RAC / Unit names from AccRacBase.
    Replaces EF join logic in BankAccountsController.GetAll.
*/
CREATE OR ALTER PROCEDURE [dbo].[sp_get_instbankacc]
    @PageNumber          INT = 1,
    @PageSize            INT = 0,
    @AccessLevel         VARCHAR(20) = 'ahq',
    @ScopeCmdId          INT = NULL,
    @ScopeBaseId         INT = NULL,
    @AllowedBaseIdsCsv   NVARCHAR(MAX) = NULL,
    @TotalCount          INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber IS NULL OR @PageNumber <= 0
        SET @PageNumber = 1;

    IF @AccessLevel IS NULL OR LTRIM(RTRIM(@AccessLevel)) = ''
        SET @AccessLevel = 'ahq';

    ;WITH Filtered AS
    (
        SELECT ba.*
        FROM dbo.BankAccounts ba
        WHERE (ba.IsDeleted = 0 OR ba.IsDeleted IS NULL)
          AND (
                @AccessLevel = 'ahq'
             OR (
                    @AccessLevel = 'command'
                AND @ScopeCmdId IS NOT NULL
                AND ba.RAC = @ScopeCmdId
                        AND (
                                (@ScopeBaseId IS NOT NULL AND ba.[Base] = @ScopeBaseId)
                             OR (
                                    @ScopeBaseId IS NULL
                                AND @AllowedBaseIdsCsv IS NOT NULL
                                AND LTRIM(RTRIM(@AllowedBaseIdsCsv)) <> ''
                                AND ba.[Base] IN (
                                        SELECT TRY_CAST(LTRIM(RTRIM(value)) AS INT)
                                        FROM STRING_SPLIT(@AllowedBaseIdsCsv, ',')
                                        WHERE TRY_CAST(LTRIM(RTRIM(value)) AS INT) IS NOT NULL
                                    )
                                )
                            )
                )
             OR (
                    @AccessLevel = 'base'
                AND @ScopeBaseId IS NOT NULL
                AND ba.[Base] = @ScopeBaseId
                )
              )
    )
    SELECT @TotalCount = COUNT(*)
    FROM Filtered;

    IF @PageSize > 0
    BEGIN
        ;WITH Filtered AS
        (
            SELECT ba.*
            FROM dbo.BankAccounts ba
            WHERE (ba.IsDeleted = 0 OR ba.IsDeleted IS NULL)
              AND (
                    @AccessLevel = 'ahq'
                 OR (
                        @AccessLevel = 'command'
                    AND @ScopeCmdId IS NOT NULL
                    AND ba.RAC = @ScopeCmdId
                        AND (
                                (@ScopeBaseId IS NOT NULL AND ba.[Base] = @ScopeBaseId)
                             OR (
                                    @ScopeBaseId IS NULL
                                AND @AllowedBaseIdsCsv IS NOT NULL
                                AND LTRIM(RTRIM(@AllowedBaseIdsCsv)) <> ''
                                AND ba.[Base] IN (
                                        SELECT TRY_CAST(LTRIM(RTRIM(value)) AS INT)
                                        FROM STRING_SPLIT(@AllowedBaseIdsCsv, ',')
                                        WHERE TRY_CAST(LTRIM(RTRIM(value)) AS INT) IS NOT NULL
                                    )
                                )
                            )
                    )
                 OR (
                        @AccessLevel = 'base'
                    AND @ScopeBaseId IS NOT NULL
                    AND ba.[Base] = @ScopeBaseId
                    )
                  )
        )
        SELECT
            f.Id,
            f.OpeningDate,
            CmdId = f.RAC,
            BaseId = f.[Base],
            f.FundingSource,
            f.FundName,
            f.TitleOfAccount,
            f.BankName,
            f.BranchCode,
            f.BranchAddress,
            f.IBAN,
            f.Currency,
            f.AccountType,
            f.SignatoryDate,
            f.Signatory1,
            f.Signatory2,
            f.Signatory3,
            f.StatusDate,
            f.Remarks,
            f.Authority,
            f.Reference,
            f.CreatedDate,
            f.AccStatus,
            f.ActionDate,
            f.ActionBy,
            f.Action,
            f.IsDeleted,
            CmdName = LTRIM(RTRIM(ISNULL(rac.[NAME], ''))),
            BaseName = LTRIM(RTRIM(ISNULL(un.[NAME], '')))
        FROM Filtered f
        LEFT JOIN dbo.AccRacBase rac
            ON rac.Id = f.RAC
           AND (rac.IsDeleted = 0 OR rac.IsDeleted IS NULL)
        LEFT JOIN dbo.AccRacBase un
            ON un.Id = f.[Base]
           AND (un.IsDeleted = 0 OR un.IsDeleted IS NULL)
        ORDER BY f.Id DESC
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;
    END
    ELSE
    BEGIN
        ;WITH Filtered AS
        (
            SELECT ba.*
            FROM dbo.BankAccounts ba
            WHERE (ba.IsDeleted = 0 OR ba.IsDeleted IS NULL)
              AND (
                    @AccessLevel = 'ahq'
                 OR (
                        @AccessLevel = 'command'
                    AND @ScopeCmdId IS NOT NULL
                    AND ba.RAC = @ScopeCmdId
                        AND (
                                (@ScopeBaseId IS NOT NULL AND ba.[Base] = @ScopeBaseId)
                             OR (
                                    @ScopeBaseId IS NULL
                                AND @AllowedBaseIdsCsv IS NOT NULL
                                AND LTRIM(RTRIM(@AllowedBaseIdsCsv)) <> ''
                                AND ba.[Base] IN (
                                        SELECT TRY_CAST(LTRIM(RTRIM(value)) AS INT)
                                        FROM STRING_SPLIT(@AllowedBaseIdsCsv, ',')
                                        WHERE TRY_CAST(LTRIM(RTRIM(value)) AS INT) IS NOT NULL
                                    )
                                )
                            )
                    )
                 OR (
                        @AccessLevel = 'base'
                    AND @ScopeBaseId IS NOT NULL
                    AND ba.[Base] = @ScopeBaseId
                    )
                  )
        )
        SELECT
            f.Id,
            f.OpeningDate,
            CmdId = f.RAC,
            BaseId = f.[Base],
            f.FundingSource,
            f.FundName,
            f.TitleOfAccount,
            f.BankName,
            f.BranchCode,
            f.BranchAddress,
            f.IBAN,
            f.Currency,
            f.AccountType,
            f.SignatoryDate,
            f.Signatory1,
            f.Signatory2,
            f.Signatory3,
            f.StatusDate,
            f.Remarks,
            f.Authority,
            f.Reference,
            f.CreatedDate,
            f.AccStatus,
            f.ActionDate,
            f.ActionBy,
            f.Action,
            f.IsDeleted,
            CmdName = LTRIM(RTRIM(ISNULL(rac.[NAME], ''))),
            BaseName = LTRIM(RTRIM(ISNULL(un.[NAME], '')))
        FROM Filtered f
        LEFT JOIN dbo.AccRacBase rac
            ON rac.Id = f.RAC
           AND (rac.IsDeleted = 0 OR rac.IsDeleted IS NULL)
        LEFT JOIN dbo.AccRacBase un
            ON un.Id = f.[Base]
           AND (un.IsDeleted = 0 OR un.IsDeleted IS NULL)
        ORDER BY f.Id DESC;
    END
END
GO
