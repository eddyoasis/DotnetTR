namespace TradingLimitMVC.Models.AppSettings
{
    public class LDAPAppSetting
    {
        public bool IsBypass { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public string BaseDn { get; set; } = string.Empty;
    }
}
