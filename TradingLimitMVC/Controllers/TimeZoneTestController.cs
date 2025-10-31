using Microsoft.AspNetCore.Mvc;
using TradingLimitMVC.Helpers;
using TradingLimitMVC.Models.ViewModels;

namespace TradingLimitMVC.Controllers
{
    public class TimeZoneTestController : Controller
    {
        public IActionResult Index()
        {
            var model = new TimeZoneTestViewModel
            {
                UtcNow = DateTime.UtcNow,
                LocalTime = DateTimeHelper.GetCurrentLocalTime(),
                OffsetHours = DateTimeHelper.GetTimezoneOffsetHours(),
                FormattedLocal = DateTimeHelper.FormatToDisplayString(DateTime.UtcNow),
                FormattedShort = DateTimeHelper.FormatToShortString(DateTime.UtcNow),
                FormattedDate = DateTimeHelper.FormatToShortDateString(DateTime.UtcNow)
            };

            return View(model);
        }
    }
}