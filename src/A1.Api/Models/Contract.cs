using System;

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
        public string GovtShareCondition { get; set; } = string.Empty;
        public decimal? PAFShare { get; set; }
        public bool Status { get; set; }
    }
}

