using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class ApprovalNotification
    {
        public int Id { get; set; }

        [Required]
        public int RequestId { get; set; }
        [Required]
        [StringLength(50)]
        public string RequestType { get; set; } = string.Empty; // "TradingLimit" or other types
        [Required]
        [StringLength(200)]
        public string RecipientEmail { get; set; } = string.Empty;
        [Required]
        [StringLength(100)]
        public string RecipientName { get; set; } = string.Empty;
        [Required]
        public NotificationType Type { get; set; }
        [Required]
        public DateTime SentDate { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadDate { get; set; }
        [StringLength(500)]
        public string? Message { get; set; }
    }
    public enum NotificationType
    {
        [Display(Name = "Approval Required")]
        ApprovalRequired = 1,
        [Display(Name = "Approved")]
        Approved = 2,
        [Display(Name = "Rejected")]
        Rejected = 3,
        [Display(Name = "Returned for Revision")]
        ReturnedForRevision = 4,
        [Display(Name = "Escalated")]
        Escalated = 5,
        [Display(Name = "Final Approval")]
        FinalApproval = 6
    }

}
