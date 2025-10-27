using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public DateTime UpdatedDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = "System";
    }
}
