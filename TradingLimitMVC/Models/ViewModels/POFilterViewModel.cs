namespace TradingLimitMVC.Models.ViewModels
{
    public class POFilterViewModel
    {
        public string? POReference { get; set; }
        public string? PRReference { get; set; }
        public string? Vendor { get; set; }
        public string? Company { get; set; }
        public string? POOriginator { get; set; }
        public POWorkflowStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public decimal? AmountFrom { get; set; }
        public decimal? AmountTo { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; } = "desc";
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class POIndexViewModel
    {
        public List<PurchaseOrder> PurchaseOrders { get; set; } = new();
        public POFilterViewModel Filters { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<string> Vendors { get; set; } = new();
        public List<string> Companies { get; set; } = new();
    }
}
