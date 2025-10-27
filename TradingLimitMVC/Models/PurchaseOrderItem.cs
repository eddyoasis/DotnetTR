using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingLimitMVC.Models
{
    public class PurchaseOrderItem
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }

        public string? Description { get; set; }

        public string? Details { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Discount %")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercent { get; set; }

        [Display(Name = "Discount $")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        public string? GST { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Display(Name = "PR No")]
        public string? PRNo { get; set; }

        // Navigation property
        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    }


}
