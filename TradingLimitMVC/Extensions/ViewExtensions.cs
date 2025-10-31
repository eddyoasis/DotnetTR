using TradingLimitMVC.Helpers;

namespace TradingLimitMVC.Extensions
{
    public static class ViewExtensions
    {
        /// <summary>
        /// Format DateTime for display in views with local timezone
        /// </summary>
        public static string ToLocalDisplay(this DateTime dateTime, string format = "dd/MM/yyyy HH:mm:ss")
        {
            return DateTimeHelper.FormatToDisplayString(dateTime, format);
        }
        
        /// <summary>
        /// Format nullable DateTime for display in views with local timezone
        /// </summary>
        public static string ToLocalDisplay(this DateTime? dateTime, string format = "dd/MM/yyyy HH:mm:ss", string nullValue = "N/A")
        {
            return DateTimeHelper.FormatToDisplayString(dateTime, format, nullValue);
        }
        
        /// <summary>
        /// Format DateTime for short display (date only) in views
        /// </summary>
        public static string ToLocalShortDate(this DateTime dateTime)
        {
            return DateTimeHelper.FormatToShortDateString(dateTime);
        }
        
        /// <summary>
        /// Format nullable DateTime for short display (date only) in views
        /// </summary>
        public static string ToLocalShortDate(this DateTime? dateTime, string nullValue = "N/A")
        {
            return DateTimeHelper.FormatToShortDateString(dateTime, nullValue);
        }
        
        /// <summary>
        /// Format DateTime for short display with time in views
        /// </summary>
        public static string ToLocalShort(this DateTime dateTime)
        {
            return DateTimeHelper.FormatToShortString(dateTime);
        }
        
        /// <summary>
        /// Format nullable DateTime for short display with time in views
        /// </summary>
        public static string ToLocalShort(this DateTime? dateTime, string nullValue = "N/A")
        {
            return DateTimeHelper.FormatToShortString(dateTime, nullValue);
        }
    }
}