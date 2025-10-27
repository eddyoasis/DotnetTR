namespace TradingLimitMVC.Models.ViewModels
{
    public class PRFilterViewModel
    {
        public string? PRReference { get; set; }
        public string? Company { get; set; }
        public string? Department { get; set; }
        public string? SubmittedBy { get; set; }
        public WorkflowStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public decimal? AmountFrom { get; set; }
        public decimal? AmountTo { get; set; }
        // Sorting
        public string SortBy { get; set; } = "CreatedDate";
        public string SortOrder { get; set; } = "desc";
        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class PRIndexViewModel
    {
        public List<PurchaseRequisition> PurchaseRequisitions { get; set; } = new();
        public PRFilterViewModel Filters { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<string> Companies { get; set; } = new();
        public List<string> Departments { get; set; } = new();
    }
}
