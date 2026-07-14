USE [A1Lands]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
    Grid list for Cash & Bank with COA / bank display fields and TR child counts.
    Replaces EF join logic in CashAndBanksController.GetAll.
*/
CREATE OR ALTER PROCEDURE [dbo].[sp_GetCashAndBanks]
    @PageNumber           INT = 1,
    @PageSize             INT = 0,
    @ParentCashAndBankId  INT = NULL,
    @TopLevelOnly         BIT = 0,
    @TotalCount           INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber IS NULL OR @PageNumber <= 0
        SET @PageNumber = 1;

    ;WITH Filtered AS
    (
        SELECT cb.*
        FROM dbo.CashAndBanks cb
        WHERE (cb.IsDeleted = 0 OR cb.IsDeleted IS NULL)
          AND (
                (@ParentCashAndBankId IS NOT NULL AND @ParentCashAndBankId > 0
                 AND cb.ParentCashAndBankId = @ParentCashAndBankId)
             OR ((@ParentCashAndBankId IS NULL OR @ParentCashAndBankId <= 0)
                 AND (@TopLevelOnly = 0 OR cb.ParentCashAndBankId IS NULL))
              )
    ),
    ChildCounts AS
    (
        SELECT
            child.ParentCashAndBankId,
            COUNT(*) AS ChildCount
        FROM dbo.CashAndBanks child
        WHERE (child.IsDeleted = 0 OR child.IsDeleted IS NULL)
          AND child.ParentCashAndBankId IS NOT NULL
        GROUP BY child.ParentCashAndBankId
    ),
    ReferencedAccounts AS
    (
        SELECT DISTINCT t.PaidFromAccountId AS AccountId
        FROM dbo.InterAccTransfers t
        WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
        UNION
        SELECT DISTINCT t.ReceivedInAccountId
        FROM dbo.InterAccTransfers t
        WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
    )
    SELECT @TotalCount = COUNT(*)
    FROM Filtered;

    IF @PageSize > 0
    BEGIN
        ;WITH Filtered AS
        (
            SELECT cb.*
            FROM dbo.CashAndBanks cb
            WHERE (cb.IsDeleted = 0 OR cb.IsDeleted IS NULL)
              AND (
                    (@ParentCashAndBankId IS NOT NULL AND @ParentCashAndBankId > 0
                     AND cb.ParentCashAndBankId = @ParentCashAndBankId)
                 OR ((@ParentCashAndBankId IS NULL OR @ParentCashAndBankId <= 0)
                     AND (@TopLevelOnly = 0 OR cb.ParentCashAndBankId IS NULL))
                  )
        ),
        ChildCounts AS
        (
            SELECT
                child.ParentCashAndBankId,
                COUNT(*) AS ChildCount
            FROM dbo.CashAndBanks child
            WHERE (child.IsDeleted = 0 OR child.IsDeleted IS NULL)
              AND child.ParentCashAndBankId IS NOT NULL
            GROUP BY child.ParentCashAndBankId
        ),
        ReferencedAccounts AS
        (
            SELECT DISTINCT t.PaidFromAccountId AS AccountId
            FROM dbo.InterAccTransfers t
            WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
            UNION
            SELECT DISTINCT t.ReceivedInAccountId
            FROM dbo.InterAccTransfers t
            WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
        )
        SELECT
            f.Id,
            f.AcctId,
            f.Name,
            f.CoaId,
            f.Currency,
            f.Mode,
            f.IBAN,
            f.BankListsId,
            f.Status,
            f.ParentCashAndBankId,
            f.ActionDate,
            f.ActionBy,
            f.Action,
            f.IsDeleted,
            CoaDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(coa.AcctId, ''))), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM(ISNULL(coa.AcctName, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(coa.AcctId)) + N' - ' + LTRIM(RTRIM(coa.AcctName))
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(coa.AcctName, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(coa.AcctName))
                    ELSE ISNULL(coa.ControlAccount, N'')
                END,
            BankDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(bl.Code, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(ISNULL(bl.Name, ''))) + N' (' + LTRIM(RTRIM(bl.Code)) + N')'
                    ELSE LTRIM(RTRIM(ISNULL(bl.Name, '')))
                END,
            ChildCount = CASE WHEN f.ParentCashAndBankId IS NULL THEN ISNULL(cc.ChildCount, 0) ELSE 0 END,
            IsReferencedByInterAccTransfer =
                CASE WHEN ra.AccountId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
        FROM Filtered f
        LEFT JOIN dbo.ChartOfAccounts coa
            ON coa.Id = f.CoaId
           AND (coa.IsDeleted = 0 OR coa.IsDeleted IS NULL)
        LEFT JOIN dbo.BankLists bl
            ON bl.Id = f.BankListsId
           AND (bl.IsDeleted = 0 OR bl.IsDeleted IS NULL)
        LEFT JOIN ChildCounts cc
            ON cc.ParentCashAndBankId = f.Id
        LEFT JOIN ReferencedAccounts ra
            ON ra.AccountId = f.Id
        ORDER BY f.Id DESC
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;
    END
    ELSE
    BEGIN
        ;WITH Filtered AS
        (
            SELECT cb.*
            FROM dbo.CashAndBanks cb
            WHERE (cb.IsDeleted = 0 OR cb.IsDeleted IS NULL)
              AND (
                    (@ParentCashAndBankId IS NOT NULL AND @ParentCashAndBankId > 0
                     AND cb.ParentCashAndBankId = @ParentCashAndBankId)
                 OR ((@ParentCashAndBankId IS NULL OR @ParentCashAndBankId <= 0)
                     AND (@TopLevelOnly = 0 OR cb.ParentCashAndBankId IS NULL))
                  )
        ),
        ChildCounts AS
        (
            SELECT
                child.ParentCashAndBankId,
                COUNT(*) AS ChildCount
            FROM dbo.CashAndBanks child
            WHERE (child.IsDeleted = 0 OR child.IsDeleted IS NULL)
              AND child.ParentCashAndBankId IS NOT NULL
            GROUP BY child.ParentCashAndBankId
        ),
        ReferencedAccounts AS
        (
            SELECT DISTINCT t.PaidFromAccountId AS AccountId
            FROM dbo.InterAccTransfers t
            WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
            UNION
            SELECT DISTINCT t.ReceivedInAccountId
            FROM dbo.InterAccTransfers t
            WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
        )
        SELECT
            f.Id,
            f.AcctId,
            f.Name,
            f.CoaId,
            f.Currency,
            f.Mode,
            f.IBAN,
            f.BankListsId,
            f.Status,
            f.ParentCashAndBankId,
            f.ActionDate,
            f.ActionBy,
            f.Action,
            f.IsDeleted,
            CoaDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(coa.AcctId, ''))), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM(ISNULL(coa.AcctName, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(coa.AcctId)) + N' - ' + LTRIM(RTRIM(coa.AcctName))
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(coa.AcctName, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(coa.AcctName))
                    ELSE ISNULL(coa.ControlAccount, N'')
                END,
            BankDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(bl.Code, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(ISNULL(bl.Name, ''))) + N' (' + LTRIM(RTRIM(bl.Code)) + N')'
                    ELSE LTRIM(RTRIM(ISNULL(bl.Name, '')))
                END,
            ChildCount = CASE WHEN f.ParentCashAndBankId IS NULL THEN ISNULL(cc.ChildCount, 0) ELSE 0 END,
            IsReferencedByInterAccTransfer =
                CASE WHEN ra.AccountId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
        FROM Filtered f
        LEFT JOIN dbo.ChartOfAccounts coa
            ON coa.Id = f.CoaId
           AND (coa.IsDeleted = 0 OR coa.IsDeleted IS NULL)
        LEFT JOIN dbo.BankLists bl
            ON bl.Id = f.BankListsId
           AND (bl.IsDeleted = 0 OR bl.IsDeleted IS NULL)
        LEFT JOIN ChildCounts cc
            ON cc.ParentCashAndBankId = f.Id
        LEFT JOIN ReferencedAccounts ra
            ON ra.AccountId = f.Id
        ORDER BY f.Id DESC;
    END
END
GO
