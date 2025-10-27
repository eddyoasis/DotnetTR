namespace TradingLimitMVC.Models
{

    public class SAPDropdownItem
    {
        public int ID { get; set; }
        public int TypeID { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string DDID { get; set; } = string.Empty;
        public string DDName { get; set; } = string.Empty;

        public string? VendorName { get; set; }
        public string? VendorAddress { get; set; }
        public string? ContactPerson { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? FaxNumber { get; set; }

    }
}
