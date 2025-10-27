using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class POStatusHistory
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseOrderId { get; set; }

        [Required]
        [StringLength(100)]
        public string Status { get; set; } = string.Empty;

        [Required]
        public DateTime ActionTime { get; set; } = DateTime.Now;

        [Required]
        [StringLength(100)]
        public string ActionBy { get; set; } = string.Empty;

        public string? Remarks { get; set; }

        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    }
}
