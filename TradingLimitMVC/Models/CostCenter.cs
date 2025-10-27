using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingLimitMVC.Models
{
    public class CostCenter
    {
        public int Id { get; set; }
        [Required]
        public int PurchaseRequisitionId { get; set; }
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        [Required]
        [StringLength(50)]
        public string Distribution { get; set; } = string.Empty;
        [Range(0, 100)]
        public decimal? Percentage { get; set; }
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Amount { get; set; }
        [Required]
        [StringLength(200)]
        public string Approver { get; set; } = string.Empty;
        [EmailAddress]
        [StringLength(200)]
        public string? ApproverEmail { get; set; }
        // Enhanced approval fields
        public ApprovalStatusHOD ApprovalStatus { get; set; } = ApprovalStatusHOD.Pending;
        [StringLength(100)]
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        [StringLength(500)]
        public string? ApprovalComments { get; set; }
        // Additional fields for better tracking
        [StringLength(100)]
        public string? ApproverRole { get; set; }
        public int ApprovalOrder { get; set; }
        public bool IsRequired { get; set; } = true;

        // Navigation property
        public virtual PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    }

}
