USE [A1Lands]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
    Grid list for Inter Account Transfers with paid-from / received-in account labels.
    Replaces EF join logic in InterAccTransfersController.GetAll.
*/
CREATE OR ALTER PROCEDURE [dbo].[sp_GetInterAccTransfers]
    @PageNumber INT = 1,
    @PageSize   INT = 0,
    @TotalCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber IS NULL OR @PageNumber <= 0
        SET @PageNumber = 1;

    SELECT @TotalCount = COUNT(*)
    FROM dbo.InterAccTransfers t
    WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL);

    IF @PageSize > 0
    BEGIN
        SELECT
            t.Id,
            t.TransferDate,
            t.VrNo,
            t.Description,
            t.Particulars,
            t.PaidFromAccountId,
            t.SettlementVrNo,
            t.PaidFromAmount,
            t.ReceivedInAccountId,
            t.ReceivedInAmount,
            t.TinFtn,
            t.Status,
            t.ActionDate,
            t.ActionBy,
            t.Action,
            t.IsDeleted,
            PaidFromAccountDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(paidFrom.AcctId, ''))), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM(ISNULL(paidFrom.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(paidFrom.AcctId)) + N' | ' + LTRIM(RTRIM(paidFrom.Name))
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(paidFrom.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(paidFrom.Name))
                    ELSE LTRIM(RTRIM(ISNULL(paidFrom.AcctId, '')))
                END,
            ReceivedInAccountDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(receivedIn.AcctId, ''))), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM(ISNULL(receivedIn.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(receivedIn.AcctId)) + N' | ' + LTRIM(RTRIM(receivedIn.Name))
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(receivedIn.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(receivedIn.Name))
                    ELSE LTRIM(RTRIM(ISNULL(receivedIn.AcctId, '')))
                END,
            PaidFromCurrency = LTRIM(RTRIM(ISNULL(paidFrom.Currency, ''))),
            ReceivedInCurrency = LTRIM(RTRIM(ISNULL(receivedIn.Currency, '')))
        FROM dbo.InterAccTransfers t
        LEFT JOIN dbo.CashAndBanks paidFrom
            ON paidFrom.Id = t.PaidFromAccountId
           AND (paidFrom.IsDeleted = 0 OR paidFrom.IsDeleted IS NULL)
        LEFT JOIN dbo.CashAndBanks receivedIn
            ON receivedIn.Id = t.ReceivedInAccountId
           AND (receivedIn.IsDeleted = 0 OR receivedIn.IsDeleted IS NULL)
        WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
        ORDER BY t.TransferDate DESC, t.Id DESC
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;
    END
    ELSE
    BEGIN
        SELECT
            t.Id,
            t.TransferDate,
            t.VrNo,
            t.Description,
            t.Particulars,
            t.PaidFromAccountId,
            t.SettlementVrNo,
            t.PaidFromAmount,
            t.ReceivedInAccountId,
            t.ReceivedInAmount,
            t.TinFtn,
            t.Status,
            t.ActionDate,
            t.ActionBy,
            t.Action,
            t.IsDeleted,
            PaidFromAccountDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(paidFrom.AcctId, ''))), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM(ISNULL(paidFrom.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(paidFrom.AcctId)) + N' | ' + LTRIM(RTRIM(paidFrom.Name))
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(paidFrom.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(paidFrom.Name))
                    ELSE LTRIM(RTRIM(ISNULL(paidFrom.AcctId, '')))
                END,
            ReceivedInAccountDisplay =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(receivedIn.AcctId, ''))), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM(ISNULL(receivedIn.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(receivedIn.AcctId)) + N' | ' + LTRIM(RTRIM(receivedIn.Name))
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(receivedIn.Name, ''))), '') IS NOT NULL
                        THEN LTRIM(RTRIM(receivedIn.Name))
                    ELSE LTRIM(RTRIM(ISNULL(receivedIn.AcctId, '')))
                END,
            PaidFromCurrency = LTRIM(RTRIM(ISNULL(paidFrom.Currency, ''))),
            ReceivedInCurrency = LTRIM(RTRIM(ISNULL(receivedIn.Currency, '')))
        FROM dbo.InterAccTransfers t
        LEFT JOIN dbo.CashAndBanks paidFrom
            ON paidFrom.Id = t.PaidFromAccountId
           AND (paidFrom.IsDeleted = 0 OR paidFrom.IsDeleted IS NULL)
        LEFT JOIN dbo.CashAndBanks receivedIn
            ON receivedIn.Id = t.ReceivedInAccountId
           AND (receivedIn.IsDeleted = 0 OR receivedIn.IsDeleted IS NULL)
        WHERE (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
        ORDER BY t.TransferDate DESC, t.Id DESC;
    END
END
GO
