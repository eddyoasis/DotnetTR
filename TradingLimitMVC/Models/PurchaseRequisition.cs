using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Mail;

namespace TradingLimitMVC.Models
{
    public class PurchaseRequisition
    {
        public int Id { get; set; }

        [Display(Name = "Exchange Rate to SGD")]
        public decimal ExchangeRateToSGD { get; set; } = 1.0m;

        [Display(Name = "Contract Value (SGD)")]

        [NotMapped]
        public decimal ContractValueSGD
        {
            get
            {
                if (QuotationCurrency == "SGD" || ExchangeRateToSGD == 0)
                    return TotalAmount;
                return TotalAmount * ExchangeRateToSGD;
            }
        }

        [Display(Name = "Is Fixed Asset")]
        public bool IsFixedAsset { get; set; }
        [Display(Name = "PR Reference")]
        public string? PRReference { get; set; } = string.Empty;
        [Display(Name = "PR Internal No")]
        public string? PRInternalNo { get; set; }
        [Required]
        public string Company { get; set; } = string.Empty;
        [Required]
        public string Department { get; set; } = string.Empty;
        [Display(Name = "IT Related")]
        public bool IsITRelated { get; set; }
        [Display(Name = "No PO Required")]
        public bool NoPORequired { get; set; }
        [Display(Name = "Expected Delivery Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedDeliveryDate { get; set; }
        [Display(Name = "Delivery Address")]
        public string? DeliveryAddress { get; set; }
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [Display(Name = "Contact Phone No")]
        [RegularExpression(@"^[\d\s\+\-\(\)]+$", ErrorMessage = "Phone number can only contain numbers and symbols (+, -, (, ), spaces)")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? ContactPhoneNo { get; set; }

        [Display(Name = "Quotation Currency")]
        public string? QuotationCurrency { get; set; }

        [Display(Name = "Short Description")]
        [Required(ErrorMessage = "Short Description is required")]
        public string? ShortDescription { get; set; }
        [Display(Name = "Type of Purchase")]
        public string? TypeOfPurchase { get; set; }

        [Display(Name = "Reason")]
        [Required(ErrorMessage = "Reason is required")]
        public string? Reason { get; set; }

        [Display(Name = "Signed/PDF")]
        public bool SignedPDF { get; set; }
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string? Remarks { get; set; }
        // Enhanced workflow fields
        [Display(Name = "Current Status")]
        public WorkflowStatus CurrentStatus { get; set; } = WorkflowStatus.Draft;
        [Display(Name = "Submitted Date")]
        public DateTime? SubmittedDate { get; set; }
        [Display(Name = "Submitted By")]
        public string? SubmittedBy { get; set; } = string.Empty;

        public string? SubmittedByEmail { get; set; }
        [Display(Name = "Current Approver")]
        public string? CurrentApprover { get; set; }
        [Display(Name = "Approval Priority")]
        public ApprovalPriority Priority { get; set; } = ApprovalPriority.Normal;
        [Display(Name = "Final Approval Date")]
        public DateTime? FinalApprovalDate { get; set; }
        [Display(Name = "Final Approver")]
        public string? FinalApprover { get; set; }

        [Display(Name = "Current Approval Step")]
        public int CurrentApprovalStep { get; set; } = 0;
        [Display(Name = "Total Approval Steps")]
        public int TotalApprovalSteps { get; set; }
        // Rejection handling
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }
        [Display(Name = "Rejected By")]
        public string? RejectedBy { get; set; }
        [Display(Name = "Rejected Date")]
        public DateTime? RejectedDate { get; set; }
        [Display(Name = "PO Reference")]
        public string? POReference { get; set; }
        [Display(Name = "PO Generated")]
        public bool POGenerated { get; set; } = false;
        [Display(Name = "PO Generated Date")]
        public DateTime? POGeneratedDate { get; set; }

        // Distribution validation
        [Display(Name = "Distribution Type")]
        public DistributionType DistributionType { get; set; } = DistributionType.Percentage;
        [Display(Name = "Distribution Total")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DistributionTotal { get; set; }
        [Display(Name = "Distribution Currency")]
        public string? DistributionCurrency { get; set; }
        // Navigation property for workflow
        public virtual ICollection<ApprovalWorkflowStep> WorkflowSteps { get; set; } = new List<ApprovalWorkflowStep>();
        public decimal TotalAmount { get; set; }

        [Display(Name = "Expense Code")]
        [StringLength(50)]
        public string? ExpenseCode { get; set; }

        [Display(Name = "Project Code")]
        [StringLength(50)]
        public string? ProjectCode { get; set; }

        [Display(Name = "Vendor Full Address")]
        public string? VendorFullAddress { get; set; }

        [Display(Name = "Distribution Validation Status")]
        public bool IsDistributionValid { get; set; } = false;



        // Add computed property for distribution validation
        [NotMapped]
        public string DistributionValidationMessage
        {
            get
            {
                if (CostCenters?.Any() != true) return "No cost centers defined";

                if (DistributionType == DistributionType.Percentage)
                {
                    var totalPercent = CostCenters.Sum(cc => cc.Percentage ?? 0);
                    if (Math.Abs(totalPercent - 100) > 0.01m)
                        return $"Percentages must sum to 100% (current: {totalPercent}%)";
                }
                else
                {
                    var totalAmount = CostCenters.Sum(cc => cc.Amount ?? 0);
                    if (Math.Abs(totalAmount - TotalAmount) > 0.01m)
                        return $"Distribution total (${totalAmount:N2}) must equal PR total (${TotalAmount:N2})";
                }

                return "Valid";
            }
        }
        [NotMapped]
        public string StatusDisplayName => CurrentStatus switch
        {
            WorkflowStatus.Draft => "Draft",
            WorkflowStatus.Submitted => "Submitted - Awaiting Manager Approval",
            WorkflowStatus.ManagerApproval => "Manager Approved - Awaiting HOD Approval",
            WorkflowStatus.HODApproval => "HOD Review - Multi-Department Approval",
            WorkflowStatus.FinanceOfficerApproval => "Finance Officer Review",
            WorkflowStatus.FinanceHODApproval => "Finance HOD Review",
            WorkflowStatus.CFOApproval => "CFO/COO Review",
            WorkflowStatus.CEOApproval => "CEO Review",
            WorkflowStatus.Approved => "Fully Approved",
            WorkflowStatus.Rejected => "Rejected",
            WorkflowStatus.RequiresModification => "Requires Modification",
            _ => CurrentStatus.ToString()
        };
        [NotMapped]

        public string NextApproverInfo => GetNextApproverInfo();
        private string GetNextApproverInfo()
        {
            var pendingSteps = WorkflowSteps?
                .Where(s => s.Status == ApprovalStatus.Pending)
                .OrderBy(s => s.StepOrder);
            if (pendingSteps?.Any() == true)
            {
                var nextStep = pendingSteps.First();
                return $"{nextStep.ApproverRole} - {nextStep.ApproverName}";
            }
            return "No pending approvals";
        }
        // Navigation properties
        public virtual ICollection<CostCenter> CostCenters { get; set; } = new List<CostCenter>();
        public virtual ICollection<PurchaseRequisitionItem> Items { get; set; } = new List<PurchaseRequisitionItem>();
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public virtual ICollection<PurchaseRequisitionApproval> Approvals { get; set; } = new List<PurchaseRequisitionApproval>();



    }
    public enum WorkflowStatus
    {
        Draft = 0,
        Submitted = 1,
        ManagerApproval = 2,
        HODApproval = 3,
        FinanceOfficerApproval = 4,
        FinanceHODApproval = 5,
        CFOApproval = 6,
        CEOApproval = 7,
        Approved = 8,
        Rejected = 9,
        RequiresModification = 10,
        Cancelled = 11,
        FinanceApproval = 12,
        DirectorApproval = 13,
        Pending = 14
    }
    public enum ApprovalPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3
    }
    public enum DistributionType
    {
        [Display(Name = "Percentage")]
        Percentage = 0,
        [Display(Name = "Fixed Amount")]
        Amount = 1
    }
}
