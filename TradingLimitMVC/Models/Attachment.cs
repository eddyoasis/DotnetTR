using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class Attachment
    {
        public int Id { get; set; }

        public int? RequestId { get; set; }
        
        [StringLength(50)]
        public string? RequestType { get; set; } // "TradingLimit" or other types

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string FilePath { get; set; } = string.Empty;

        [Display(Name = "File Size")]
        public long FileSize { get; set; }

        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }
    }
}
