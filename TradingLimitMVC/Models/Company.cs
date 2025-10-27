using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Models
{
    public class Company
    {
        public int Id { get; set; }
        [Required]
        [StringLength(50)]
        [Display(Name = "Company Code")]
        public string CompanyCode { get; set; } = string.Empty;
        [Required]
        [StringLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Address { get; set; }
        [StringLength(100)]
        public string? City { get; set; }
        [StringLength(100)]
        public string? State { get; set; }
        [StringLength(20)]
        [Display(Name = "Postal Code")]
        public string? PostalCode { get; set; }
        [StringLength(100)]
        public string? Country { get; set; }
        [StringLength(50)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }
        [StringLength(50)]
        [Display(Name = "Tax ID")]
        public string? TaxID { get; set; }
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        [Display(Name = "Updated Date")]
        public DateTime? UpdatedDate { get; set; }
        // Computed property for display
        [Display(Name = "Full Address")]
        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Address)) parts.Add(Address);
                if (!string.IsNullOrEmpty(City)) parts.Add(City);
                if (!string.IsNullOrEmpty(State)) parts.Add(State);
                if (!string.IsNullOrEmpty(PostalCode)) parts.Add(PostalCode);
                if (!string.IsNullOrEmpty(Country)) parts.Add(Country);
                return string.Join(", ", parts);
            }
        }
    }
}
