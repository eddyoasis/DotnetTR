using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Services;
using System.Security.Claims;

namespace TradingLimitMVC.Controllers
{
    [Route("Approval")]
    public class ApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITradingLimitRequestService _tradingLimitRequestService;
        private readonly ILogger<ApprovalController> _logger;
        private readonly IGeneralService _generalService;

        public ApprovalController(
            ApplicationDbContext context,
            ITradingLimitRequestService tradingLimitRequestService,
            ILogger<ApprovalController> logger,
            IGeneralService generalService)
        {
            _context = context;
            _tradingLimitRequestService = tradingLimitRequestService;
            _logger = logger;
            _generalService = generalService;
        }

        // GET: Approval/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            try
        {
                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                var pendingRequests = await GetPendingApprovalsForUserAsync(currentUser);
                
                return View(pendingRequests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending approvals");
                TempData["ErrorMessage"] = "An error occurred while loading pending approvals.";
                return View(new List<TradingLimitRequest>());
            }
        }

        // GET: Approval/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id, string? returnUrl = null)
        {
            try
            {
                var request = await _context.TradingLimitRequests
                    .Include(t => t.Attachments)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (request == null)
                {
                    TempData["ErrorMessage"] = "Trading limit request not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if current user has permission to approve this request
                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                var canApprove = await CanUserApproveRequestAsync(currentUser, request);

                var viewModel = new ApprovalDetailsViewModel
                {
                    Request = request,
                    CanApprove = canApprove,
                    CurrentUserEmail = currentUser,
                    ReturnUrl = returnUrl
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving request details for approval: {RequestId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the request details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Approval/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(ApprovalActionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Invalid approval data.";
                    return RedirectToAction("Details", new { id = model.RequestId });
                }

                var request = await _context.TradingLimitRequests.FindAsync(model.RequestId);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Trading limit request not found.";
                    return RedirectToAction(nameof(Index));
                }

                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                var currentUserName = await _generalService.GetCurrentUserNameAsync();

                // Check permission
                var canApprove = await CanUserApproveRequestAsync(currentUser, request);
                if (!canApprove)
                {
                    TempData["ErrorMessage"] = "You do not have permission to approve this request.";
                    return RedirectToAction("Details", new { id = model.RequestId });
                }

                // Update request status
                request.Status = "Approved";
                request.ModifiedDate = DateTime.UtcNow;
                request.ModifiedBy = currentUserName;

                // Add approval record
                await AddApprovalRecordAsync(request.Id, currentUser, currentUserName, "Approved", model.Comments);

                await _context.SaveChangesAsync();

                // Send notification
                await SendApprovalNotificationAsync(request, NotificationType.Approved, model.Comments);

                TempData["SuccessMessage"] = $"Trading limit request {request.RequestId} has been approved successfully.";
                _logger.LogInformation("Request {RequestId} approved by {User}", request.RequestId, currentUser);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request: {RequestId}", model.RequestId);
                TempData["ErrorMessage"] = "An error occurred while processing the approval.";
                return RedirectToAction("Details", new { id = model.RequestId });
            }
        }

        // POST: Approval/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(ApprovalActionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Comments))
                {
                    TempData["ErrorMessage"] = "Comments are required when rejecting a request.";
                    return RedirectToAction("Details", new { id = model.RequestId });
                }

                var request = await _context.TradingLimitRequests.FindAsync(model.RequestId);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Trading limit request not found.";
                    return RedirectToAction(nameof(Index));
                }

                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                var currentUserName = await _generalService.GetCurrentUserNameAsync();

                // Check permission
                var canApprove = await CanUserApproveRequestAsync(currentUser, request);
                if (!canApprove)
                {
                    TempData["ErrorMessage"] = "You do not have permission to reject this request.";
                    return RedirectToAction("Details", new { id = model.RequestId });
                }

                // Update request status
                request.Status = "Rejected";
                request.ModifiedDate = DateTime.UtcNow;
                request.ModifiedBy = currentUserName;

                // Add approval record
                await AddApprovalRecordAsync(request.Id, currentUser, currentUserName, "Rejected", model.Comments);

                await _context.SaveChangesAsync();

                // Send notification
                await SendApprovalNotificationAsync(request, NotificationType.Rejected, model.Comments);

                TempData["SuccessMessage"] = $"Trading limit request {request.RequestId} has been rejected.";
                _logger.LogInformation("Request {RequestId} rejected by {User}", request.RequestId, currentUser);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request: {RequestId}", model.RequestId);
                TempData["ErrorMessage"] = "An error occurred while processing the rejection.";
                return RedirectToAction("Details", new { id = model.RequestId });
            }
        }

        // POST: Approval/RequestRevision
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRevision(ApprovalActionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Comments))
                {
                    TempData["ErrorMessage"] = "Comments are required when requesting revision.";
                    return RedirectToAction("Details", new { id = model.RequestId });
                }

                var request = await _context.TradingLimitRequests.FindAsync(model.RequestId);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Trading limit request not found.";
                    return RedirectToAction(nameof(Index));
                }

                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                var currentUserName = await _generalService.GetCurrentUserNameAsync();

                // Check permission
                var canApprove = await CanUserApproveRequestAsync(currentUser, request);
                if (!canApprove)
                {
                    TempData["ErrorMessage"] = "You do not have permission to request revision for this request.";
                    return RedirectToAction("Details", new { id = model.RequestId });
                }

                // Update request status
                request.Status = "Revision Required";
                request.ModifiedDate = DateTime.UtcNow;
                request.ModifiedBy = currentUserName;

                // Add approval record
                await AddApprovalRecordAsync(request.Id, currentUser, currentUserName, "Revision Required", model.Comments);

                await _context.SaveChangesAsync();

                // Send notification
                await SendApprovalNotificationAsync(request, NotificationType.ReturnedForRevision, model.Comments);

                TempData["SuccessMessage"] = $"Trading limit request {request.RequestId} has been returned for revision.";
                _logger.LogInformation("Request {RequestId} returned for revision by {User}", request.RequestId, currentUser);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting revision for request: {RequestId}", model.RequestId);
                TempData["ErrorMessage"] = "An error occurred while processing the revision request.";
                return RedirectToAction("Details", new { id = model.RequestId });
            }
        }

        #region Helper Methods

        private async Task<List<TradingLimitRequest>> GetPendingApprovalsForUserAsync(string userEmail)
        {
            // Get requests that are pending approval and user has permission to approve
            var pendingStatuses = new[] { "Submitted", "Pending Approval" };
            
            var requests = await _context.TradingLimitRequests
                .Where(r => pendingStatuses.Contains(r.Status))
                .Include(r => r.Attachments)
                .OrderByDescending(r => r.SubmittedDate)
                .ToListAsync();

            // Filter based on user approval permissions
            var approvableRequests = new List<TradingLimitRequest>();
            foreach (var request in requests)
            {
                if (await CanUserApproveRequestAsync(userEmail, request))
                {
                    approvableRequests.Add(request);
                }
            }

            return approvableRequests;
        }

        private async Task<bool> CanUserApproveRequestAsync(string userEmail, TradingLimitRequest request)
        {
            // Implement approval permission logic based on your business rules
            // For now, we'll check if user is not the creator and has appropriate role
            
            if (string.IsNullOrEmpty(userEmail) || request.CreatedBy?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true)
            {
                return false; // User cannot approve their own requests
            }

            var userJobTitle = await _generalService.GetCurrentUserJobTitleAsync();
            var userDepartment = await _generalService.GetCurrentUserDepartmentAsync();

            // Define approval hierarchy based on amount or other criteria
            // This is a simplified example - you should implement your actual business rules
            var isManager = userJobTitle.Contains("Manager", StringComparison.OrdinalIgnoreCase) ||
                           userJobTitle.Contains("Director", StringComparison.OrdinalIgnoreCase) ||
                           userJobTitle.Contains("Head", StringComparison.OrdinalIgnoreCase) ||
                           userJobTitle.Contains("HOD", StringComparison.OrdinalIgnoreCase);

            return isManager;
        }

        private Task AddApprovalRecordAsync(int requestId, string approverEmail, string approverName, string action, string? comments)
        {
            // You might want to create an ApprovalHistory table to track all approval actions
            // For now, we'll use the existing ApprovalNotification table to track this
            var approvalRecord = new ApprovalNotification
            {
                RequestId = requestId,
                RequestType = "TradingLimit",
                RecipientEmail = approverEmail,
                RecipientName = approverName,
                Type = action switch
                {
                    "Approved" => NotificationType.Approved,
                    "Rejected" => NotificationType.Rejected,
                    "Revision Required" => NotificationType.ReturnedForRevision,
                    _ => NotificationType.ApprovalRequired
                },
                Message = comments,
                SentDate = DateTime.UtcNow,
                IsRead = true,
                ReadDate = DateTime.UtcNow
            };

            _context.ApprovalNotifications.Add(approvalRecord);
            return Task.CompletedTask;
        }

        private Task SendApprovalNotificationAsync(TradingLimitRequest request, NotificationType notificationType, string? comments)
        {
            try
            {
                // Send notification to request creator
                if (!string.IsNullOrEmpty(request.CreatedBy))
                {
                    var notification = new ApprovalNotification
                    {
                        RequestId = request.Id,
                        RequestType = "TradingLimit",
                        RecipientEmail = request.CreatedBy,
                        RecipientName = request.CreatedBy, // You might want to get the actual name
                        Type = notificationType,
                        Message = comments,
                        SentDate = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.ApprovalNotifications.Add(notification);
                }

                // You can implement email sending logic here
                _logger.LogInformation("Approval notification sent for request {RequestId}: {NotificationType}", 
                    request.RequestId, notificationType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending approval notification for request {RequestId}", request.RequestId);
                // Don't throw - notification failure shouldn't block the approval process
            }

            return Task.CompletedTask;
        }

        #endregion
    }

    // View Models for the approval process
    public class ApprovalDetailsViewModel
    {
        public TradingLimitRequest Request { get; set; } = new();
        public bool CanApprove { get; set; }
        public string CurrentUserEmail { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
        public List<ApprovalNotification> ApprovalHistory { get; set; } = new();
    }

    public class ApprovalActionViewModel
    {
        public int RequestId { get; set; }
        public string? Comments { get; set; }
        public string? ReturnUrl { get; set; }
    }
}