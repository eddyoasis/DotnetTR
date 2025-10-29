namespace TradingLimitMVC.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalTradingLimitRequests { get; set; }
        public int PendingApprovals { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public List<TradingLimitRequest> RecentRequests { get; set; } = new();
        public List<TradingLimitRequest> MyPendingRequests { get; set; } = new();
    }
}
