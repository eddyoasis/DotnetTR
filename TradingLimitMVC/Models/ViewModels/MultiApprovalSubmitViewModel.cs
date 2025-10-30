using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models.ViewModels
{
    public class MultiApprovalSubmitViewModel
    {
        public int Id { get; set; }
        
        [Display(Name = "Workflow Type")]
        [Required(ErrorMessage = "Please select a workflow type")]
        public string WorkflowType { get; set; } = "Sequential";
        
        public TradingLimitRequest? Request { get; set; }
        
        public List<ApprovalStepViewModel> ApprovalSteps { get; set; } = new List<ApprovalStepViewModel>();
    }

    public class ApprovalStepViewModel
    {
        [Display(Name = "Step Number")]
        public int StepNumber { get; set; }
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Approver Email")]
        public string Email { get; set; } = string.Empty;
        
        [Display(Name = "Approver Name")]
        [MaxLength(100)]
        public string? Name { get; set; }
        
        [Display(Name = "Role/Department")]
        [MaxLength(100)]
        public string? Role { get; set; }
        
        [Display(Name = "Approval Group ID")]
        public int? ApprovalGroupId { get; set; }
        
        [Display(Name = "Approval Group Name")]
        [MaxLength(100)]
        public string? ApprovalGroupName { get; set; }
        
        [Display(Name = "Required")]
        public bool IsRequired { get; set; } = true;
        
        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }
        
        [Display(Name = "Min Amount Threshold")]
        [DataType(DataType.Currency)]
        public decimal? MinimumAmountThreshold { get; set; }
        
        [Display(Name = "Max Amount Threshold")]
        [DataType(DataType.Currency)]
        public decimal? MaximumAmountThreshold { get; set; }
        
        [Display(Name = "Department Requirement")]
        [MaxLength(100)]
        public string? RequiredDepartment { get; set; }
        
        [Display(Name = "Approval Conditions")]
        [MaxLength(500)]
        public string? ApprovalConditions { get; set; }
    }

    public class WorkflowTypeOption
    {
        public string Value { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}