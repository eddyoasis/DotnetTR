using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public enum ApprovalStatusHOD
    {
        [Display(Name = "Pending")]
        Pending = 0,
        [Display(Name = "Approved")]
        Approved = 1,
        [Display(Name = "Rejected")]
        Rejected = 2,
        [Display(Name = "Requires Modification")]
        RequiresModification = 3,
        [Display(Name = "Delegated")]
        Delegated = 4
    }

}
