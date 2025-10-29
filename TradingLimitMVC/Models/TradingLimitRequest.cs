using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingLimitMVC.Models
{
    public class TradingLimitRequest
    {
        public int Id { get; set; }

        [Display(Name = "Request ID")]
        public string? RequestId { get; set; }

        [Display(Name = "Request Date")]
        [DataType(DataType.Date)]
        [Required(ErrorMessage = "Request Date is required")]
        public DateTime RequestDate { get; set; }

        [Display(Name = "TR Code")]
        [Required(ErrorMessage = "TR Code is required")]
        [MaxLength(50)]
        public string TRCode { get; set; } = string.Empty;

        [Display(Name = "Limit End Date")]
        [DataType(DataType.Date)]
        [Required(ErrorMessage = "Limit End Date is required")]
        public DateTime LimitEndDate { get; set; }

        [Display(Name = "Client Code")]
        [Required(ErrorMessage = "Client Code is required")]
        [MaxLength(50)]
        public string ClientCode { get; set; } = string.Empty;

        [Display(Name = "Request Type")]
        [Required(ErrorMessage = "Request Type is required")]
        [MaxLength(100)]
        public string RequestType { get; set; } = string.Empty;

        [Display(Name = "Brief Description")]
        [Required(ErrorMessage = "Brief Description is required")]
        [MaxLength(1000)]
        public string BriefDescription { get; set; } = string.Empty;

        [Display(Name = "GL Current Limit")]
        [Column(TypeName = "decimal(18,2)")]
        [Required(ErrorMessage = "GL Current Limit is required")]
        [Range(0, double.MaxValue, ErrorMessage = "GL Current Limit must be a positive number")]
        public decimal GLCurrentLimit { get; set; }

        [Display(Name = "GL Proposed Limit")]
        [Column(TypeName = "decimal(18,2)")]
        [Required(ErrorMessage = "GL Proposed Limit is required")]
        [Range(0, double.MaxValue, ErrorMessage = "GL Proposed Limit must be a positive number")]
        public decimal GLProposedLimit { get; set; }

        [Display(Name = "Current Current Limit")]
        [Column(TypeName = "decimal(18,2)")]
        [Required(ErrorMessage = "Current Current Limit is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Current Current Limit must be a positive number")]
        public decimal CurrentCurrentLimit { get; set; }

        [Display(Name = "Current Proposed Limit")]
        [Column(TypeName = "decimal(18,2)")]
        [Required(ErrorMessage = "Current Proposed Limit is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Current Proposed Limit must be a positive number")]
        public decimal CurrentProposedLimit { get; set; }

        // Audit fields
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Display(Name = "Modified Date")]
        public DateTime? ModifiedDate { get; set; }

        [Display(Name = "Modified By")]
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Status tracking
        [Display(Name = "Status")]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        [Display(Name = "Submitted Date")]
        public DateTime? SubmittedDate { get; set; }

        [Display(Name = "Submitted By")]
        [MaxLength(100)]
        public string? SubmittedBy { get; set; }

        // Legacy approval fields (for backward compatibility)
        [Display(Name = "Approver Email")]
        [MaxLength(200)]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string? ApprovalEmail { get; set; }

        [Display(Name = "Approved Date")]
        public DateTime? ApprovedDate { get; set; }

        [Display(Name = "Approved By")]
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approval Comments")]
        [MaxLength(500)]
        public string? ApprovalComments { get; set; }

        // Multi-step approval workflow
        public virtual ApprovalWorkflow? ApprovalWorkflow { get; set; }

        // Navigation property for attachments
        public virtual ICollection<TradingLimitRequestAttachment> Attachments { get; set; } = new List<TradingLimitRequestAttachment>();

        // Helper properties
        [NotMapped]
        [Display(Name = "Has Supporting Documents")]
        public bool HasSupportingDocuments => Attachments?.Any() == true;

        [NotMapped]
        [Display(Name = "Uses Multi-Step Approval")]
        public bool HasMultiStepApproval => ApprovalWorkflow != null;

        [NotMapped]
        [Display(Name = "Current Approval Step")]
        public string? CurrentApprovalStep => ApprovalWorkflow?.CurrentActiveStep?.ApproverName ?? ApprovalWorkflow?.CurrentActiveStep?.ApproverEmail;

        [NotMapped]
        [Display(Name = "Approval Progress")]
        public double ApprovalProgress => ApprovalWorkflow?.CompletionPercentage ?? 0;

        [NotMapped]
        [Display(Name = "Pending Approvers")]
        public IEnumerable<ApprovalStep> PendingApprovalSteps => ApprovalWorkflow?.ApprovalSteps?.Where(s => s.IsActive) ?? Enumerable.Empty<ApprovalStep>();
    }

    public class TradingLimitRequestAttachment
    {
        public int Id { get; set; }

        [Required]
        public int TradingLimitRequestId { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime UploadDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string? UploadedBy { get; set; }

        // Navigation property
        public virtual TradingLimitRequest TradingLimitRequest { get; set; } = null!;
    }
}