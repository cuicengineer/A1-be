using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class JournalEntry : BaseEntity
    {
        [Column(TypeName = "date")]
        public DateTime EntryDate { get; set; }

        [MaxLength(50)]
        public string VrNo { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDebit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCredit { get; set; }

        /// <summary>Deprecated — line items are stored in JournalEntriesLines.</summary>
        public string? LinesJson { get; set; }

        public string? AttachmentsJson { get; set; }

        /// <summary>When true, entry is locked (read-only) except for privileged unlock.</summary>
        public bool IsLock { get; set; }

        [NotMapped]
        public List<JournalEntryLine>? Lines { get; set; }
    }
}
