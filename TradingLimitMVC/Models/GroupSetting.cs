using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    /// <summary>
    /// Represents group settings for approval workflow management.
    /// Defines users and their roles within different groups (IWM, GSPS, Risk).
    /// </summary>
    public class GroupSetting
    {
        /// <summary>
        /// Primary key identifier for the group setting record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Group identifier: 1-IWM, 2-GSPS, 3-Risk
        /// </summary>
        [Display(Name = "Group")]
        [Required(ErrorMessage = "Group selection is required")]
        public int GroupID { get; set; }

        /// <summary>
        /// Type identifier: 1-Approver, 2-Endorser, 3-Observer
        /// </summary>
        [Display(Name = "Type")]
        [Required(ErrorMessage = "Type selection is required")]
        public int TypeID { get; set; }

        /// <summary>
        /// Username of the person assigned to this group setting.
        /// </summary>
        [Display(Name = "Username")]
        [Required(ErrorMessage = "Username is required")]
        [MaxLength(100, ErrorMessage = "Username cannot exceed 100 characters")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Username must be between 2 and 100 characters")]
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// Email address of the person assigned to this group setting.
        /// </summary>
        [Display(Name = "Email Address")]
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [MaxLength(200, ErrorMessage = "Email address cannot exceed 200 characters")]
        public string Email { get; set; } = string.Empty;

        #region Audit Fields

        /// <summary>
        /// Date and time when this record was created.
        /// </summary>
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Username of the person who created this record.
        /// </summary>
        [Display(Name = "Created By")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Date and time when this record was last modified.
        /// </summary>
        [Display(Name = "Last Modified")]
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Username of the person who last modified this record.
        /// </summary>
        [Display(Name = "Modified By")]
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        #endregion

        #region Helper Properties

        /// <summary>
        /// Gets the display name for the group based on GroupID.
        /// </summary>
        public string GroupName => GroupID switch
        {
            1 => "IWM",
            2 => "GSPS",
            3 => "Risk",
            _ => $"Group {GroupID}"
        };

        /// <summary>
        /// Gets the display name for the type based on TypeID.
        /// </summary>
        public string TypeName => TypeID switch
        {
            1 => "Approver",
            2 => "Endorser",
            3 => "Observer",
            _ => $"Type {TypeID}"
        };

        /// <summary>
        /// Gets a formatted display string for this group setting.
        /// </summary>
        public string DisplayText => $"{GroupName} - {TypeName}: {Username} ({Email})";

        #endregion
    }
}
