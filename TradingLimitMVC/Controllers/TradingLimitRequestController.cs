using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Models.ViewModels;
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
                // Get request with full approval workflow data
                var request = await _context.TradingLimitRequests
                    .Include(t => t.Attachments)
                    .Include(t => t.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (request == null)
                {
                    return NotFound();
                }

                // Get available approvers from SAPDropdownItems where TypeName indicates approver roles
                var approvers = await _context.SAPDropdownItems
                    .Where(s => (s.TypeName.Contains("Manager") || s.TypeName.Contains("HOD") || 
                                s.TypeName.Contains("Approver") || s.TypeName.Contains("Director")) &&
                               !string.IsNullOrEmpty(s.Email))
                    .Select(s => new ApproverInfo
                    {
                        Id = s.ID,
                        Name = !string.IsNullOrEmpty(s.ContactPerson) ? s.ContactPerson : s.DDName,
                        Email = s.Email!,
                        Role = s.TypeName,
                        Department = s.DDName,
                        PhoneNumber = s.PhoneNumber ?? string.Empty
                    })
                    .ToListAsync();

                approvers.Add(new ApproverInfo
                {
                    Name = "eddy",
                    Email = "eddy.wang@kgi.com",
                    Role = "Developer"
                });

                approvers.Add(new ApproverInfo
                {
                    Name = "eddy2",
                    Email = "eddy.wang@kgi.com",
                    Role = "Developer2"
                });

                approvers.Add(new ApproverInfo
                {
                    Name = "eddy3",
                    Email = "eddy.wang@kgi.com",
                    Role = "Developer3"
                });

                ViewBag.AvailableApprovers = approvers;
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

        // GET: TradingLimitRequest/Submit/5
        [HttpGet("Submit/{id}")]
        public async Task<IActionResult> Submit(int id)
        {
            try
            {
                var request = await _tradingLimitRequestService.GetByIdAsync(id);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Trading Limit Request not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if request can be submitted (should be in Draft status)
                if (request.Status != "Draft")
                {
                    TempData["ErrorMessage"] = "This request cannot be submitted in its current status.";
                    return RedirectToAction("Details", new { id });
                }

                var viewModel = new SubmitRequestViewModel
                {
                    Id = id,
                    Request = request
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading submit page for trading limit request with ID {Id}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the submit page.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: TradingLimitRequest/Submit
        [HttpPost("Submit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitPost(SubmitRequestViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // If model is invalid, reload the request and return to view
                    model.Request = await _tradingLimitRequestService.GetByIdAsync(model.Id);
                    return View("Submit", model);
                }

                var userName = GetCurrentUserName();
                var result = await _tradingLimitRequestService.SubmitAsync(model.Id, userName, model.ApprovalEmail);
                
                if (result)
                {
                    TempData["SuccessMessage"] = "Trading Limit Request submitted successfully for approval.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Trading Limit Request not found or cannot be submitted.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting trading limit request with ID {Id}", model.Id);
                TempData["ErrorMessage"] = "An error occurred while submitting the trading limit request.";
            }

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // GET: TradingLimitRequest/SubmitMultiApproval/5
        // Redirect to Details page since multi-approval is now integrated there
        [HttpGet("SubmitMultiApproval/{id}")]
        public IActionResult SubmitMultiApproval(int id)
        {
            // Redirect to Details page where multi-approval functionality is now integrated
            TempData["ShowMultiApprovalSetup"] = "true";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: TradingLimitRequest/SubmitMultiApproval
        [HttpPost("SubmitMultiApproval")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitMultiApprovalPost(MultiApprovalSubmitViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Log the validation errors for debugging
                    foreach (var error in ModelState.Where(x => x.Value?.Errors.Count > 0))
                    {
                        _logger.LogWarning("Model validation error for {Key}: {Errors}", 
                            error.Key, string.Join(", ", error.Value?.Errors.Select(e => e.ErrorMessage) ?? Array.Empty<string>()));
                    }

                    // Reload request and return to view
                    model.Request = await _tradingLimitRequestService.GetByIdAsync(model.Id);
                    return View("SubmitMultiApproval", model);
                }

                // Validate that we have at least one approver
                if (!model.ApprovalSteps.Any() || model.ApprovalSteps.All(s => string.IsNullOrWhiteSpace(s.Email)))
                {
                    ModelState.AddModelError("ApprovalSteps", "At least one approver is required.");
                    model.Request = await _tradingLimitRequestService.GetByIdAsync(model.Id);
                    return View("SubmitMultiApproval", model);
                }

                var userName = GetCurrentUserName();
                
                // Convert view model to service request objects
                var approvers = model.ApprovalSteps
                    .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                    .Select((s, index) => new ApprovalStepRequest
                    {
                        StepNumber = s.StepNumber > 0 ? s.StepNumber : index + 1,
                        Email = s.Email,
                        Name = s.Name,
                        Role = s.Role,
                        IsRequired = s.IsRequired,
                        DueDate = s.DueDate,
                        MinimumAmountThreshold = s.MinimumAmountThreshold,
                        MaximumAmountThreshold = s.MaximumAmountThreshold,
                        RequiredDepartment = s.RequiredDepartment,
                        ApprovalConditions = s.ApprovalConditions
                    }).ToList();

                var result = await _tradingLimitRequestService.SubmitWithMultiApprovalAsync(
                    model.Id, userName, approvers, model.WorkflowType);
                
                if (result)
                {
                    TempData["SuccessMessage"] = "Trading Limit Request submitted successfully with multi-approval workflow.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Trading Limit Request not found or cannot be submitted.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting trading limit request with multi-approval workflow for ID {Id}", model.Id);
                TempData["ErrorMessage"] = "An error occurred while submitting the trading limit request.";
            }

            return RedirectToAction(nameof(Details), new { id = model.Id });
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