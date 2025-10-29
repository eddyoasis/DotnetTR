using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(500)]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RevokedAt { get; set; }

        [StringLength(500)]
        public string? RevokedBy { get; set; }

        [StringLength(1000)]
        public string? RevokeReason { get; set; }

        [StringLength(500)]
        public string? ReplacedByToken { get; set; }

        [StringLength(50)]
        public string DeviceId { get; set; } = string.Empty;

        [StringLength(255)]
        public string IpAddress { get; set; } = string.Empty;

        [StringLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        // Computed properties
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsRevoked => RevokedAt != null;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}