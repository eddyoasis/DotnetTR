using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class PurchaseOrderApproval
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        [Required]
        public string ApproverName { get; set; } = string.Empty;
        [Required]
        public string ApproverEmail { get; set; } = string.Empty;
        public ApprovalStatus Status { get; set; }
        public string? Comments { get; set; }
        public DateTime ApprovalDate { get; set; }
        public int ApprovalLevel { get; set; }
        public string? Department { get; set; }
        public ApprovalMethod ApprovalMethod { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    }
}
