using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models.ViewModels
{
    /// <summary>
    /// Represents a group of approvers that work together in a single approval step
    /// </summary>
    public class ApprovalStepGroup
    {
        /// <summary>
        /// Sequential step number in the approval workflow (1, 2, 3...)
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Group ID (1=IWM, 2=GSPS, 3=Risk)
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Display name of the group
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Type of approval within this step:
        /// - "ParallelAnyOne": Any one approver in the group can approve (OR logic)
        /// - "ParallelAll": All approvers in the group must approve (AND logic)
        /// - "Sequential": Approvers must approve in order within the group
        /// </summary>
        public string ApprovalType { get; set; } = "ParallelAnyOne";

        /// <summary>
        /// Number of approvals required from this group (e.g., 1 for any one, 2 for any two)
        /// </summary>
        public int RequiredApprovals { get; set; } = 1;

        /// <summary>
        /// List of approvers in this step group
        /// </summary>
        public List<ApproverInfo> Approvers { get; set; } = new List<ApproverInfo>();

        /// <summary>
        /// Due date for this approval step
        /// </summary>
        public DateTime DueDate { get; set; }

        /// <summary>
        /// Whether this step is mandatory or optional
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// Minimum amount threshold for this step to be active
        /// </summary>
        public decimal? MinimumAmountThreshold { get; set; }

        /// <summary>
        /// Maximum amount threshold for this step to be active
        /// </summary>
        public decimal? MaximumAmountThreshold { get; set; }

        /// <summary>
        /// Additional conditions or notes for this step
        /// </summary>
        public string? Conditions { get; set; }

        /// <summary>
        /// Status of this step: Pending, InProgress, Approved, Rejected
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Number of approvals received so far
        /// </summary>
        public int ApprovalsReceived { get; set; } = 0;

        /// <summary>
        /// Whether this step is currently active (can receive approvals)
        /// </summary>
        public bool IsActive => Status == "Pending" || Status == "InProgress";

        /// <summary>
        /// Whether this step is complete (approved or has enough approvals)
        /// </summary>
        public bool IsComplete => Status == "Approved" || ApprovalsReceived >= RequiredApprovals;

        /// <summary>
        /// Whether this step is blocked by previous steps
        /// </summary>
        public bool IsBlocked { get; set; } = false;

        /// <summary>
        /// Gets a user-friendly description of the approval requirements
        /// </summary>
        public string ApprovalDescription
        {
            get
            {
                return ApprovalType switch
                {
                    "ParallelAnyOne" => RequiredApprovals == 1 
                        ? $"Any one approver from {GroupName}" 
                        : $"Any {RequiredApprovals} approvers from {GroupName}",
                    "ParallelAll" => $"All approvers from {GroupName}",
                    "Sequential" => $"Sequential approval from {GroupName}",
                    _ => $"Approval from {GroupName}"
                };
            }
        }

        /// <summary>
        /// Gets the progress percentage for this step
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                if (RequiredApprovals == 0) return 0;
                return Math.Min(100, (double)ApprovalsReceived / RequiredApprovals * 100);
            }
        }
    }

    /// <summary>
    /// Represents the complete workflow with sequential steps and parallel approvers
    /// </summary>
    public class EnhancedApprovalWorkflow
    {
        /// <summary>
        /// Request ID this workflow belongs to
        /// </summary>
        public int RequestId { get; set; }

        /// <summary>
        /// List of sequential approval steps
        /// </summary>
        public List<ApprovalStepGroup> Steps { get; set; } = new List<ApprovalStepGroup>();

        /// <summary>
        /// Overall workflow status
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Current active step number
        /// </summary>
        public int CurrentStepNumber { get; set; } = 1;

        /// <summary>
        /// Total number of steps
        /// </summary>
        public int TotalSteps => Steps.Count;

        /// <summary>
        /// Overall progress percentage
        /// </summary>
        public double OverallProgress
        {
            get
            {
                if (TotalSteps == 0) return 0;
                var completedSteps = Steps.Count(s => s.IsComplete);
                return (double)completedSteps / TotalSteps * 100;
            }
        }

        /// <summary>
        /// Whether the entire workflow is complete
        /// </summary>
        public bool IsComplete => Steps.All(s => s.IsComplete);

        /// <summary>
        /// Whether any step has been rejected
        /// </summary>
        public bool IsRejected => Steps.Any(s => s.Status == "Rejected");

        /// <summary>
        /// Gets the currently active step
        /// </summary>
        public ApprovalStepGroup? GetCurrentActiveStep()
        {
            return Steps.FirstOrDefault(s => s.StepNumber == CurrentStepNumber && s.IsActive);
        }

        /// <summary>
        /// Gets all pending steps that are ready for approval
        /// </summary>
        public List<ApprovalStepGroup> GetPendingSteps()
        {
            return Steps.Where(s => s.IsActive && !s.IsBlocked).ToList();
        }
    }
}