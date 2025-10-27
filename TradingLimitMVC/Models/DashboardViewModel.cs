namespace TradingLimitMVC.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalPRs { get; set; }
        public int PendingApprovals { get; set; }
        public int TotalPOs { get; set; }
        public int CompletedOrders { get; set; }
        public List<PurchaseRequisition> RecentPRs { get; set; } = new();
        public List<PurchaseOrder> RecentPOs { get; set; } = new();
    }
}
