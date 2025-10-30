using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Services;
using System.Security.Claims;

namespace TradingLimitMVC.Controllers
{
    [Authorize]
    [Route("Approval")]
    public class ApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITradingLimitRequestService _tradingLimitRequestService;
        private readonly IApprovalWorkflowService _approvalWorkflowService;
        private readonly ILogger<ApprovalController> _logger;
        private readonly IGeneralService _generalService;

        public ApprovalController(
            ApplicationDbContext context,
            ITradingLimitRequestService tradingLimitRequestService,
            IApprovalWorkflowService approvalWorkflowService,
            ILogger<ApprovalController> logger,
            IGeneralService generalService)
        {
            _context = context;
            _tradingLimitRequestService = tradingLimitRequestService;
            _approvalWorkflowService = approvalWorkflowService;
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
                var pendingRequests = await _tradingLimitRequestService.GetPendingApprovalsForUserAsync(currentUser);
                
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
                    .Include(t => t.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
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
        [HttpPost("Approve")]
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

                var request = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == model.RequestId);
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
        [HttpPost("Reject")]
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

                var request = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == model.RequestId);
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
        [HttpPost("RequestRevision")]
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

                var request = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == model.RequestId);
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



        private async Task<bool> CanUserApproveRequestAsync(string userEmail, TradingLimitRequest request)
        {
            if (string.IsNullOrEmpty(userEmail))
            {
                return false;
            }

            // Additional check: user cannot approve their own requests
            if (request.CreatedBy?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true)
            {
                return false;
            }

            // Check if request has a multi-approval workflow
            var workflow = await _context.ApprovalWorkflows
                .Include(w => w.ApprovalSteps)
                .FirstOrDefaultAsync(w => w.TradingLimitRequestId == request.Id);

            if (workflow != null)
            {
                // Multi-approval workflow: Check if user has an active approval step
                var userStep = workflow.ApprovalSteps
                    .FirstOrDefault(s => s.ApproverEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase));

                if (userStep == null)
                {
                    return false; // User is not an approver in this workflow
                }

                // For sequential workflows, only active steps can be approved
                if (workflow.WorkflowType == "Sequential")
                {
                    return userStep.IsActive && (userStep.Status == "Pending" || userStep.Status == "InProgress");
                }
                
                // For parallel workflows, any pending step can be approved
                if (workflow.WorkflowType == "Parallel")
                {
                    return userStep.Status == "Pending" || userStep.Status == "InProgress";
                }
                
                return false;
            }
            else
            {
                // Legacy single-approver workflow
                if (string.IsNullOrEmpty(request.ApprovalEmail))
                {
                    return false;
                }

                // Check if the current user's email matches the assigned approval email
                return userEmail.Equals(request.ApprovalEmail, StringComparison.OrdinalIgnoreCase);
            }
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

        // POST: Approval/ApprovalStep/{stepId}/Approve
        [HttpPost("ApprovalStep/{stepId}/Approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStep(int stepId, string? comments = null)
        {
            try
            {
                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                
                var canApprove = await _approvalWorkflowService.CanUserApproveStepAsync(stepId, currentUser);
                if (!canApprove)
                {
                    return Json(new { success = false, message = "You are not authorized to approve this step." });
                }

                var success = await _approvalWorkflowService.ProcessApprovalStepAsync(stepId, currentUser, "Approved", comments);
                
                if (success)
                {
                    return Json(new { success = true, message = "Step approved successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Error processing approval step." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving step {StepId}", stepId);
                return Json(new { success = false, message = "An error occurred while processing the approval." });
            }
        }

        // POST: Approval/ApprovalStep/{stepId}/Reject
        [HttpPost("ApprovalStep/{stepId}/Reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectStep(int stepId, string comments)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(comments))
                {
                    return Json(new { success = false, message = "Comments are required when rejecting." });
                }

                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                
                var canApprove = await _approvalWorkflowService.CanUserApproveStepAsync(stepId, currentUser);
                if (!canApprove)
                {
                    return Json(new { success = false, message = "You are not authorized to reject this step." });
                }

                var success = await _approvalWorkflowService.ProcessApprovalStepAsync(stepId, currentUser, "Rejected", comments);
                
                if (success)
                {
                    return Json(new { success = true, message = "Step rejected successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Error processing rejection." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting step {StepId}", stepId);
                return Json(new { success = false, message = "An error occurred while processing the rejection." });
            }
        }

        // GET: Approval/ApprovalStep/{stepId}
        [HttpGet("ApprovalStep/{stepId}")]
        public async Task<IActionResult> ApprovalStepDetails(int stepId)
        {
            try
            {
                var step = await _approvalWorkflowService.GetApprovalStepAsync(stepId);
                if (step == null)
                {
                    TempData["ErrorMessage"] = "Approval step not found.";
                    return RedirectToAction("Index");
                }

                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                var canApprove = await _approvalWorkflowService.CanUserApproveStepAsync(stepId, currentUser);

                var viewModel = new ApprovalStepViewModel
                {
                    Step = step,
                    CanApprove = canApprove,
                    CurrentUserEmail = currentUser
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading approval step {StepId}", stepId);
                TempData["ErrorMessage"] = "An error occurred while loading the approval step.";
                return RedirectToAction("Index");
            }
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

    public class ApprovalStepViewModel
    {
        public ApprovalStep Step { get; set; } = new();
        public bool CanApprove { get; set; }
        public string CurrentUserEmail { get; set; } = string.Empty;
    }
}