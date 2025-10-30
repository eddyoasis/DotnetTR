using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingLimitMVC.Models
{
    /// <summary>
    /// Represents the approval workflow for a trading limit request with multiple approvers
    /// </summary>
    public class ApprovalWorkflow
    {
        public int Id { get; set; }

        [Required]
        public int TradingLimitRequestId { get; set; }

        [Display(Name = "Workflow Type")]
        [Required]
        [MaxLength(20)]
        public string WorkflowType { get; set; } = "Sequential"; // Sequential, Parallel, Conditional

        [Display(Name = "Current Step")]
        public int CurrentStep { get; set; } = 1;

        [Display(Name = "Overall Status")]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Approved, Rejected, OnHold

        [Display(Name = "Required Approvals")]
        public int RequiredApprovals { get; set; } = 1; // For parallel workflows

        [Display(Name = "Received Approvals")]
        public int ReceivedApprovals { get; set; } = 0;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "Completed Date")]
        public DateTime? CompletedDate { get; set; }

        // Navigation properties
        public virtual TradingLimitRequest TradingLimitRequest { get; set; } = null!;
        public virtual ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();

        // Helper properties
        [NotMapped]
        public bool IsCompleted => Status == "Approved" || Status == "Rejected";

        [NotMapped]
        public ApprovalStep? CurrentActiveStep => ApprovalSteps?.FirstOrDefault(s => s.StepNumber == CurrentStep);

        [NotMapped]
        public double CompletionPercentage
        {
            get
            {
                if (ApprovalSteps == null || !ApprovalSteps.Any()) return 0;
                var completedSteps = ApprovalSteps.Count(s => s.Status == "Approved" || s.Status == "Rejected");
                return (double)completedSteps / ApprovalSteps.Count * 100;
            }
        }
    }

    /// <summary>
    /// Represents an individual approval step within the workflow
    /// </summary>
    public class ApprovalStep
    {
        public int Id { get; set; }

        [Required]
        public int ApprovalWorkflowId { get; set; }

        [Display(Name = "Step Number")]
        public int StepNumber { get; set; }

        [Display(Name = "Approver Email")]
        [Required]
        [MaxLength(200)]
        [EmailAddress]
        public string ApproverEmail { get; set; } = string.Empty;

        [Display(Name = "Approver Name")]
        [MaxLength(100)]
        public string? ApproverName { get; set; }

        [Display(Name = "Approver Role")]
        [MaxLength(100)]
        public string? ApproverRole { get; set; }

        [Display(Name = "Approval Group")]
        public int? ApprovalGroupId { get; set; }

        [Display(Name = "Approval Group Name")]
        [MaxLength(100)]
        public string? ApprovalGroupName { get; set; }

        [Display(Name = "Step Status")]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Approved, Rejected, Skipped, OnHold

        [Display(Name = "Required")]
        public bool IsRequired { get; set; } = true;

        [Display(Name = "Comments")]
        [MaxLength(1000)]
        public string? Comments { get; set; }

        [Display(Name = "Action Date")]
        public DateTime? ActionDate { get; set; }

        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Assigned Date")]
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Reminder Sent")]
        public DateTime? LastReminderSent { get; set; }

        [Display(Name = "Escalation Level")]
        public int EscalationLevel { get; set; } = 0;

        // Additional approval criteria
        [Display(Name = "Minimum Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinimumAmountThreshold { get; set; }

        [Display(Name = "Maximum Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaximumAmountThreshold { get; set; }

        [Display(Name = "Department")]
        [MaxLength(100)]
        public string? RequiredDepartment { get; set; }

        [Display(Name = "Conditions")]
        [MaxLength(500)]
        public string? ApprovalConditions { get; set; }

        // Navigation property
        public virtual ApprovalWorkflow ApprovalWorkflow { get; set; } = null!;

        // Helper properties
        [NotMapped]
        public bool IsActive
        {
            get
            {
                // For sequential workflows, only the current step is active
                if (ApprovalWorkflow?.WorkflowType == "Sequential")
                {
                    return StepNumber == ApprovalWorkflow.CurrentStep && 
                           (Status == "Pending" || Status == "InProgress");
                }
                
                // For parallel workflows, any pending/in-progress step is active
                return Status == "Pending" || Status == "InProgress";
            }
        }

        [NotMapped]
        public bool IsCompleted => Status == "Approved" || Status == "Rejected" || Status == "Skipped";

        [NotMapped]
        public bool IsOverdue => DueDate.HasValue && DateTime.UtcNow > DueDate.Value && IsActive;

        [NotMapped]
        public TimeSpan? TimeToComplete
        {
            get
            {
                if (ActionDate.HasValue && AssignedDate != default)
                    return ActionDate.Value - AssignedDate;
                return null;
            }
        }
    }

    /// <summary>
    /// Enum for workflow types to ensure consistency
    /// </summary>
    public enum WorkflowType
    {
        Sequential,  // Steps must be completed in order
        Parallel,    // All steps can be completed simultaneously
        Conditional  // Steps depend on conditions or previous step outcomes
    }

    /// <summary>
    /// Enum for approval step statuses
    /// </summary>
    public enum ApprovalStepStatus
    {
        Pending,     // Waiting to be processed
        InProgress,  // Currently being reviewed
        Approved,    // Approved by this step
        Rejected,    // Rejected by this step
        Skipped,     // Skipped due to conditions
        OnHold       // Temporarily paused
    }
}