namespace TradingLimitMVC.Models.AppSettings
{
    public class SmtpAppSetting
    {
        public string Host { get; set; }
        public string EmailFrom { get; set; }
        public List<string> EmailTo { get; set; }
        public bool IsTestEmailTo { get; set; }
    }
}
