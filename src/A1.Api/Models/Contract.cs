using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class Contract : BaseEntity
    {
        public string ContractNo { get; set; } = string.Empty;
        public int CmdId { get; set; }
        public int BaseId { get; set; }
        public int ClassId { get; set; }
        public int GrpId { get; set; }
        public string TenantNo { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string NatureOfBusiness { get; set; } = string.Empty;
        public DateTime ContractStartDate { get; set; }
        public DateTime ContractEndDate { get; set; }
        public DateTime? CommercialOperationDate { get; set; }
        public decimal InitialRentPM { get; set; }
        public decimal InitialRentPA { get; set; }
        public int PaymentTermMonths { get; set; }
        public decimal? IncreaseRatePercent { get; set; }
        public int? IncreaseIntervalMonths { get; set; }
        public int? SDRateMonths { get; set; }
        public decimal? SecurityDepositAmount { get; set; }
        public decimal? RentalValue { get; set; }
        public decimal? GovtShare { get; set; }
        public string? Term { get; set; } = string.Empty;
        public string? RiseTermType { get; set; }
        public DateTime? RiseDate { get; set; }
        public int? RiseYear { get; set; }
        public decimal? PAFShare { get; set; }
        public decimal? GroupArea { get; set; }
        public decimal? GroupRate { get; set; }
        public decimal? RentalValueRate { get; set; }
        public decimal? VaArea { get; set; }
        public bool Status { get; set; }
        public string? userIPAddress { get; set; }
        public string? Remarks { get; set; }

        [NotMapped]
        public List<ContractRiseTerm>? ContractRiseTerms { get; set; }
    }
}

