namespace TradingLimitMVC.Services
{
    //public class BaseService
    //{
    //    public static string Username { get; set; }
    //    public static string Email { get; set; }
    //    public static string Department { get; set; }
    //    public static string JobTitle { get; set; }
    //}

    public static class BaseService
    {
        public static string? Username { get; set; }
        public static string? Email { get; set; }
        public static string? Department { get; set; }
        public static string? JobTitle { get; set; }
        public static void Clear()
        {
            Username = null;
            Email = null;
            Department = null;
            JobTitle = null;
        }
    }
}
