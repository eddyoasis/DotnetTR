using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class PurchaseRequisitionApproval
    {
        public int Id { get; set; }
        public int PurchaseRequisitionId { get; set; }
        [Required]
        [Display(Name = "Approver Name")]
        public string ApproverName { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        [Display(Name = "Approver Email")]
        public string ApproverEmail { get; set; } = string.Empty;
        [Required]
        [Display(Name = "Approval Status")]
        public ApprovalStatus Status { get; set; }
        [Required]
        [Display(Name = "Comments")]
        public string Comments { get; set; } = string.Empty;
        [Display(Name = "Approval Date")]
        public DateTime ApprovalDate { get; set; } = DateTime.Now;
        [Display(Name = "Approval Level")]
        public int ApprovalLevel { get; set; }
        [Display(Name = "Department")]
        public string? Department { get; set; }
        [Display(Name = "Employee ID")]
        public string? EmployeeId { get; set; }
        // Enhanced fields
        [Display(Name = "Approver Role")]
        [StringLength(100)]
        public string? ApproverRole { get; set; }
        [Display(Name = "Approval Method")]
        public ApprovalMethod ApprovalMethod { get; set; } = ApprovalMethod.Web;
        [Display(Name = "IP Address")]
        [StringLength(50)]
        public string? IPAddress { get; set; }
        [Display(Name = "Teams Message ID")]
        [StringLength(200)]
        public string? TeamsMessageId { get; set; }
        // Navigation property
        public virtual PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    }
    public enum ApprovalMethod
    {
        Web = 0,
        Teams = 1,
        Email = 2,
        Mobile = 3
    }

    public enum ApprovalStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        RequiresModification = 3,
        Skipped = 4,
        Delegated = 5
    }

}

