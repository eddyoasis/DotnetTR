using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models.ViewModels
{
    public class SubmitRequestViewModel
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Approver email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Approver Email")]
        public string ApprovalEmail { get; set; } = string.Empty;
        
        public TradingLimitRequest? Request { get; set; }
    }
}