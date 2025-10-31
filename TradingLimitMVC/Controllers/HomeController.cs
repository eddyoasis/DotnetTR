using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Models.ViewModels;
using System.Diagnostics;


namespace TradingLimitMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;


        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = new DashboardViewModel
                {
                    TotalTradingLimitRequests = await _context.TradingLimitRequests.CountAsync(),
                    PendingApprovals = await _context.TradingLimitRequests
                        .CountAsync(tlr => tlr.Status == "Submitted"),
                    ApprovedRequests = await _context.TradingLimitRequests
                        .CountAsync(tlr => tlr.Status == "Approved"),
                    RejectedRequests = await _context.TradingLimitRequests
                        .CountAsync(tlr => tlr.Status == "Rejected"),
                    RecentRequests = await _context.TradingLimitRequests
                        .OrderByDescending(tlr => tlr.CreatedDate)
                        .Take(5)
                        .ToListAsync(),
                    MyPendingRequests = new List<TradingLimitRequest>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                var fallbackViewModel = new DashboardViewModel
                {
                    TotalTradingLimitRequests = 0,
                    PendingApprovals = 0,
                    ApprovedRequests = 0,
                    RejectedRequests = 0,
                    RecentRequests = new List<TradingLimitRequest>(),
                    MyPendingRequests = new List<TradingLimitRequest>()
                };
                TempData["Error"] = "Error loading dashboard data. Please check your database connection.";
                return View(fallbackViewModel);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }


}
