using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models.ViewModels
{
    public class ApprovalViewModel
    {
        public PurchaseRequisition PurchaseRequisition { get; set; } = null!;
        public PurchaseRequisitionApproval Approval { get; set; } = null!;
        public List<PurchaseRequisitionApproval> ApprovalHistory { get; set; } = new();


        //[Required(ErrorMessage = "Remarks are required for approval actions")]
        public string Remarks { get; set; } = string.Empty;

        // Display properties
        public string CurrentApproverLevel => GetApprovalLevelName(Approval?.ApprovalLevel ?? 1);
        public string NextApprovalLevel => GetNextApprovalLevelName(PurchaseRequisition?.CurrentStatus ?? WorkflowStatus.Submitted);
        public bool CanApprove => PurchaseRequisition?.CurrentStatus != WorkflowStatus.Draft &&
                                  PurchaseRequisition?.CurrentStatus != WorkflowStatus.Approved &&
                                  PurchaseRequisition?.CurrentStatus != WorkflowStatus.Rejected;

        private string GetApprovalLevelName(int level)
        {
            return level switch
            {
                1 => "Manager Approval",
                2 => "Director Approval",
                3 => "Finance Approval",
                _ => "Unknown Level"
            };
        }

        private string GetNextApprovalLevelName(WorkflowStatus status)
        {
            return status switch
            {
                WorkflowStatus.Submitted => "Manager Approval",
                WorkflowStatus.ManagerApproval => "Director Approval",
                WorkflowStatus.DirectorApproval => "Finance Approval",
                WorkflowStatus.FinanceApproval => "Final Approval",
                _ => "No Further Approval Required"
            };
        }
    }


}
