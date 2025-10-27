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
                    TotalPRs = await _context.PurchaseRequisitions.CountAsync(),
                    TotalPOs = await _context.PurchaseOrders.CountAsync(),
                    PendingApprovals = await _context.PurchaseRequisitions
                        .CountAsync(pr => pr.CurrentStatus != WorkflowStatus.Draft &&
                                         pr.CurrentStatus != WorkflowStatus.Approved &&
                                         pr.CurrentStatus != WorkflowStatus.Rejected),
                    CompletedOrders = await _context.PurchaseRequisitions
                        .CountAsync(pr => pr.CurrentStatus == WorkflowStatus.Approved),
                    RecentPRs = await _context.PurchaseRequisitions
                        .Include(pr => pr.Items)
                        .OrderByDescending(pr => pr.CreatedDate)
                        .Take(5)
                        .ToListAsync(),
                    RecentPOs = await _context.PurchaseOrders
                        .Include(po => po.Items)
                        .OrderByDescending(po => po.CreatedDate)
                        .Take(5)
                        .ToListAsync()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                var fallbackViewModel = new DashboardViewModel
                {
                    TotalPRs = 0,
                    TotalPOs = 0,
                    PendingApprovals = 0,
                    CompletedOrders = 0,
                    RecentPRs = new List<PurchaseRequisition>(),
                    RecentPOs = new List<PurchaseOrder>()
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
