using System.ComponentModel.DataAnnotations.Schema;

namespace A1.Api.Models
{
    public class LockDate : BaseEntity
    {
        [Column("LockingDate")]
        public DateTime? LockingDate { get; set; }

       
    }
}
