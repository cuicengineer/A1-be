-- IsFinalized: used by sp_GetContractInvoiceSchedule (header rows: SubInvoiceNo IS NULL).
IF COL_LENGTH('dbo.ContractInvoicesEdit', 'IsFinalized') IS NULL
    ALTER TABLE dbo.ContractInvoicesEdit ADD IsFinalized BIT NULL;
GO

-- Header rows must use NULL SubInvoiceNo (sp_GetContractInvoiceSchedule joins on e.SubInvoiceNo IS NULL).
UPDATE dbo.ContractInvoicesEdit
SET SubInvoiceNo = NULL
WHERE SubInvoiceNo = '';
GO
