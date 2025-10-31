namespace TradingLimitMVC.Models.ViewModels
{
    public class TimeZoneTestViewModel
    {
        public DateTime UtcNow { get; set; }
        public DateTime LocalTime { get; set; }
        public int OffsetHours { get; set; }
        public string FormattedLocal { get; set; } = string.Empty;
        public string FormattedShort { get; set; } = string.Empty;
        public string FormattedDate { get; set; } = string.Empty;
    }
}