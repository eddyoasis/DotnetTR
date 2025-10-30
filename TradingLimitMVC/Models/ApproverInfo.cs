namespace TradingLimitMVC.Models
{
    public class ApproverInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
    }
}