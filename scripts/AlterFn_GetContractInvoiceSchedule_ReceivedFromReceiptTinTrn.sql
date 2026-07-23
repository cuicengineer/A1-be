USE [A1Lands]
GO

/*
  Agreement Prov Invoice grid "Received":
    Sum ReceiptLines.Amount/Total for the same InvoiceNo where TinTrn (or TinFtn) is assigned.

  Apply inside dbo.fn_GetContractInvoiceSchedule:
    1) Prefer receipt TIN total for AmountReceived (over CollectionEntries / edit header).
    2) Recalculate AmountPending = Receivable - Received.

  Full function body with this change is also in:
    AlterFn_GetContractInvoiceSchedule_DaysToDueOverdue.sql

  API also overlays the same logic in ContractInvoiceScheduleController
  (OverlayReceivedFromReceiptTinTrnLinesAsync) so the grid stays correct even before
  the function is redeployed.
*/

PRINT 'Use AlterFn_GetContractInvoiceSchedule_DaysToDueOverdue.sql (or redeploy API) for Received = ReceiptLines TIN-TRN sum.';
GO
