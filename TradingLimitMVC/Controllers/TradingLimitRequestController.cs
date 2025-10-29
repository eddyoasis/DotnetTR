using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Services;
using System.Security.Claims;

namespace TradingLimitMVC.Controllers
{
    [Route("TradingLimitRequest")]
    public class TradingLimitRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITradingLimitRequestService _tradingLimitRequestService;
        private readonly ILogger<TradingLimitRequestController> _logger;
        private readonly IWebHostEnvironment _environment;

        public TradingLimitRequestController(
            ApplicationDbContext context,
            ITradingLimitRequestService tradingLimitRequestService,
            ILogger<TradingLimitRequestController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _tradingLimitRequestService = tradingLimitRequestService;
            _logger = logger;
            _environment = environment;
        }

        // GET: TradingLimitRequest
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var requests = await _tradingLimitRequestService.GetAllAsync();
                return View(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trading limit requests");
                TempData["ErrorMessage"] = "An error occurred while loading the trading limit requests.";
                return View(new List<TradingLimitRequest>());
            }
        }

        // GET: TradingLimitRequest/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var request = await _tradingLimitRequestService.GetByIdAsync(id);
                if (request == null)
                {
                    return NotFound();
                }
                return View(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trading limit request with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the trading limit request.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: TradingLimitRequest/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            var model = new TradingLimitRequest
            {
                RequestDate = DateTime.Today,
                LimitEndDate = DateTime.Today.AddMonths(12)
            };
            return View(model);
        }

        // POST: TradingLimitRequest/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TRCode,RequestDate,LimitEndDate,ClientCode,RequestType,BriefDescription,GLCurrentLimit,GLProposedLimit,CurrentCurrentLimit,CurrentProposedLimit")] TradingLimitRequest tradingLimitRequest)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Set audit fields
                    var userName = GetCurrentUserName();
                    tradingLimitRequest.CreatedBy = userName;
                    tradingLimitRequest.CreatedDate = DateTime.Now;

                    var createdRequest = await _tradingLimitRequestService.CreateAsync(tradingLimitRequest);
                    TempData["SuccessMessage"] = "Trading Limit Request created successfully.";
                    return RedirectToAction(nameof(Details), new { id = createdRequest.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating trading limit request");
                TempData["ErrorMessage"] = "An error occurred while creating the trading limit request.";
            }

            return View(tradingLimitRequest);
        }

        // GET: TradingLimitRequest/Edit/5
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var request = await _tradingLimitRequestService.GetByIdAsync(id);
                if (request == null)
                {
                    return NotFound();
                }

                // Check if request can be edited (only drafts can be edited)
                if (request.Status != "Draft")
                {
                    TempData["ErrorMessage"] = "Only draft requests can be edited.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                return View(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trading limit request for edit with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the trading limit request.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: TradingLimitRequest/Edit/5
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RequestId,TRCode,RequestDate,LimitEndDate,ClientCode,RequestType,BriefDescription,GLCurrentLimit,GLProposedLimit,CurrentCurrentLimit,CurrentProposedLimit,Status,CreatedBy,CreatedDate")] TradingLimitRequest tradingLimitRequest)
        {
            if (id != tradingLimitRequest.Id)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Set audit fields
                    var userName = GetCurrentUserName();
                    tradingLimitRequest.ModifiedBy = userName;
                    tradingLimitRequest.ModifiedDate = DateTime.Now;

                    var updatedRequest = await _tradingLimitRequestService.UpdateAsync(tradingLimitRequest);
                    TempData["SuccessMessage"] = "Trading Limit Request updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = updatedRequest.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trading limit request with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the trading limit request.";
            }

            return View(tradingLimitRequest);
        }

        // GET: TradingLimitRequest/Delete/5
        [HttpGet("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var request = await _tradingLimitRequestService.GetByIdAsync(id);
                if (request == null)
                {
                    return NotFound();
                }

                // Check if request can be deleted (only drafts can be deleted)
                if (request.Status != "Draft")
                {
                    TempData["ErrorMessage"] = "Only draft requests can be deleted.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                return View(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trading limit request for delete with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the trading limit request.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: TradingLimitRequest/Delete/5
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var result = await _tradingLimitRequestService.DeleteAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = "Trading Limit Request deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Trading Limit Request not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trading limit request with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the trading limit request.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: TradingLimitRequest/Submit/5
        [HttpPost("Submit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            try
            {
                var userName = GetCurrentUserName();
                var result = await _tradingLimitRequestService.SubmitAsync(id, userName);
                
                if (result)
                {
                    TempData["SuccessMessage"] = "Trading Limit Request submitted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Trading Limit Request not found or cannot be submitted.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting trading limit request with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while submitting the trading limit request.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: TradingLimitRequest/MyRequests
        [HttpGet("MyRequests")]
        public async Task<IActionResult> MyRequests()
        {
            try
            {
                var userName = GetCurrentUserName();
                var requests = await _tradingLimitRequestService.GetByUserAsync(userName);
                return View(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user's trading limit requests");
                TempData["ErrorMessage"] = "An error occurred while loading your trading limit requests.";
                return View(new List<TradingLimitRequest>());
            }
        }

        // Helper method to get current user name
        private string GetCurrentUserName()
        {
            return User?.Identity?.Name ?? BaseService.Username ?? "System";
        }
    }
}