using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingLimitMVC.Models
{
    public class PurchaseOrder
    {
        public int Id { get; set; }

        [Display(Name = "PO Reference No")]
        public string? POReference { get; set; }
        [Display(Name = "PO No")]
        public string? PONo { get; set; }
        [Display(Name = "Issue Date")]
        [DataType(DataType.Date)]
        public DateTime IssueDate { get; set; }
        [Display(Name = "Delivery Date")]
        [DataType(DataType.Date)]
        public DateTime? DeliveryDate { get; set; }
        [Display(Name = "PO Originator")]
        public string? POOriginator { get; set; }
        [Display(Name = "Order Issued By")]
        public string? OrderIssuedBy { get; set; }
        [Display(Name = "Payment Terms")]
        public string? PaymentTerms { get; set; }
        [Required]
        public string Company { get; set; } = string.Empty;
        [Display(Name = "Delivery Address")]
        public string? DeliveryAddress { get; set; }
        [Required]
        public string Vendor { get; set; } = string.Empty;
        [Display(Name = "Vendor Address")]
        public string? VendorAddress { get; set; }
        public string? Attention { get; set; }
        [Display(Name = "Phone No")]
        public string? PhoneNo { get; set; }
        public string? Email { get; set; }
        [Display(Name = "Fax No")]
        public string? FaxNo { get; set; }
        // Link to PR
        [Display(Name = "PR Reference")]
        public string? PRReference { get; set; }

        [Display(Name = "Selected PR ID")]
        public int? SelectedPRId { get; set; }
        public int? PurchaseRequisitionId { get; set; }
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string? Remarks { get; set; }
        // Computed Properties
        //[NotMapped]
        //public decimal TotalAmount => Items?.Sum(i => i.Amount) ?? 0;
        //[NotMapped]
        //public decimal GST => Items?.Sum(i => CalculateGST(i.Amount, i.GST)) ?? 0;
        //private decimal CalculateGST(decimal amount, string? gstRate)
        //{
        //    if (string.IsNullOrEmpty(gstRate)) return 0;
        //    if (!gstRate.Contains('%')) return 0;
        //    var rateString = gstRate.Replace("%", "");
        //    if (decimal.TryParse(rateString, out decimal rate))
        //    {
        //        return amount * (rate / 100);
        //    }
        //    return 0;
        //}
        //[NotMapped]
        //public decimal GrandTotal => TotalAmount + GST;

        [NotMapped]
        public string FormattedTotal => $"{Currency ?? "SGD"} {GrandTotal:N2}";
        [NotMapped]
        public decimal SubTotal => Items?.Sum(i => {
            var itemSubtotal = i.Quantity * i.UnitPrice;
            var discount = i.DiscountPercent > 0
                ? (itemSubtotal * i.DiscountPercent / 100)
                : i.DiscountAmount;
            return itemSubtotal - discount;
        }) ?? 0;
        [NotMapped]
        public decimal GST
        {
            get
            {
                var firstItem = Items?.FirstOrDefault();
                if (firstItem == null || string.IsNullOrEmpty(firstItem.GST)) return 0;
                var gstString = firstItem.GST.Replace("%", "").Trim();
                if (decimal.TryParse(gstString, out decimal rate))
                {
                    return SubTotal * (rate / 100);
                }
                return 0;
            }
        }
        [NotMapped]
        public decimal TotalAmount => SubTotal;
        [NotMapped]
        public decimal GrandTotal => SubTotal + GST;

        [Display(Name = "CS Prepared By")]
        public string? CSPreparedBy { get; set; }

        [Display(Name = "Payment Status")]
        public string? PaymentStatus { get; set; } = "Not Pay";

        [Display(Name = "CS Remarks")]
        public string? CSRemarks { get; set; }

        // IT Department fields
        [Display(Name = "IT Personnel")]
        public string? ITPersonnel { get; set; }

        [Display(Name = "IT Contract")]
        public string? ITContract { get; set; }

        [Display(Name = "IT Remarks")]
        public string? ITRemarks { get; set; }

        // Maintenance fields for CS Department
        [Display(Name = "Maintenance From")]
        public DateTime? MaintenanceFrom { get; set; }

        [Display(Name = "Maintenance To")]
        public DateTime? MaintenanceTo { get; set; }

        [Display(Name = "Maintenance Notes")]
        public string? MaintenanceNotes { get; set; }

        // Status tracking
        [Display(Name = "PO Status")]
        public string? POStatus { get; set; } = "Draft";

        [Display(Name = "Action Time")]
        public DateTime? ActionTime { get; set; }

        [Display(Name = "Action By")]
        public string? ActionBy { get; set; }


        [Display(Name = "Current Status")]
        public POWorkflowStatus CurrentStatus { get; set; } = POWorkflowStatus.Draft;

        [Display(Name = "Submitted Date")]

        public DateTime? SubmittedDate { get; set; }

        [Display(Name = "Submitted By")]

        public string? SubmittedBy { get; set; }

        public int CurrentApprovalStep { get; set; } = 0;
        [Display(Name = "Total Approval Steps")]

        public int TotalApprovalSteps { get; set; } = 0;

        [Display(Name = "Final Approval Date")]

        public DateTime? FinalApprovalDate { get; set; }

        [Display(Name = "Final Approver")]

        public string? FinalApprover { get; set; }

        [Display(Name = "Currency")]
        [MaxLength(3)]
        public string? Currency { get; set; } = "SGD";

        

        // Navigation properties

        public virtual ICollection<POApprovalWorkflowStep> WorkflowSteps { get; set; } = new List<POApprovalWorkflowStep>();

        public virtual ICollection<PurchaseOrderApproval> Approvals { get; set; } = new List<PurchaseOrderApproval>();

        // Change to List for better model binding support
        public virtual List<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
        public virtual PurchaseRequisition? PurchaseRequisition { get; set; }

       

    }
    public enum POStatus
    {
        Draft = 0,
        Issued = 1,
        Acknowledged = 2,
        PartiallyReceived = 3,
        Completed = 4,
        Cancelled = 5
    }
    // Enum for PO workflow status

    public enum POWorkflowStatus
    {
        Draft = 0,
        Submitted = 1,
        FinanceOfficerApproval = 2,
        FinanceHODApproval = 3,
        DirectorApproval = 4,
        Approved = 5,
        Rejected = 6,
        Issued = 7,
        Completed = 8,
        Cancelled = 9,
        PendingApproval = 10
    }
}
