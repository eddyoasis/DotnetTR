namespace TradingLimitMVC.Models
{
    public class ApprovalWorkflowStep
    {
        public int Id { get; set; }
        public int PurchaseRequisitionId { get; set; }
        // Workflow position
        public int StepOrder { get; set; }
        public string ApproverRole { get; set; } = string.Empty;
        public string ApproverName { get; set; } = string.Empty;
        public string ApproverEmail { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        // Approval details
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        public DateTime? ActionDate { get; set; }
        public string? Comments { get; set; }
        // Workflow control
        public bool IsRequired { get; set; } = true;
        public bool IsParallel { get; set; } = false; // For multi-HOD approvals
        public decimal? ApprovalAmount { get; set; } // For tracking cost center amounts
                                                     // Navigation
        public virtual PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    }
}
