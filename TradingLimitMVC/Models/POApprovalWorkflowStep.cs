using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class POApprovalWorkflowStep
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseOrderId { get; set; }

        public int StepOrder { get; set; }

        [Required]
        [StringLength(100)]
        public string ApproverRole { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ApproverName { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string ApproverEmail { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Department { get; set; }

        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

        public DateTime? ActionDate { get; set; }

        [StringLength(500)]
        public string? Comments { get; set; }

        public bool IsRequired { get; set; } = true;

        // Navigation
        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    }
}
