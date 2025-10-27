using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingLimitMVC.Models
{
    public class PurchaseRequisitionItem
    {
        public int Id { get; set; }


        public int PurchaseRequisitionId { get; set; }

        [Required]
        public string Action { get; set; } = "Purchase";

        public string? Description { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Discount %")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercent { get; set; }

        [Display(Name = "Discount Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Display(Name = "Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string? GST { get; set; }

        [Display(Name = "Suggested Supplier")]
        public string? SuggestedSupplier { get; set; }

        [Display(Name = "Payment Terms")]
        public string? PaymentTerms { get; set; }

        [Display(Name = "Fixed Assets")]
        public bool IsFixedAsset { get; set; } = false;

        [Display(Name = "Assets Class")]
        public string? AssetsClass { get; set; }

        [Display(Name = "Maintenance From")]
        [DataType(DataType.Date)]
        public DateTime? MaintenanceFrom { get; set; }

        [Display(Name = "Maintenance To")]
        [DataType(DataType.Date)]
        public DateTime? MaintenanceTo { get; set; }

        // Navigation property
        public virtual PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    }


}
