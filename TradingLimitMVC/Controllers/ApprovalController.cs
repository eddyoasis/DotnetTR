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
                _logger.LogInformation("Current user email: {Email}", currentUser);
                
                var pendingRequests = await _tradingLimitRequestService.GetPendingApprovalsForUserAsync(currentUser);
                _logger.LogInformation("Found {Count} pending requests for user {Email}", pendingRequests.Count(), currentUser);
                
                // Debug: Log all requests in the system to understand what's available
                var allRequests = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .ToListAsync();
                
                _logger.LogInformation("Total requests in system: {Count}", allRequests.Count);
                foreach (var req in allRequests.Take(5)) // Log first 5 for debugging
                {
                    _logger.LogInformation("Request {Id}: Status={Status}, ApprovalEmail={ApprovalEmail}, HasWorkflow={HasWorkflow}",
                        req.Id, req.Status, req.ApprovalEmail, req.ApprovalWorkflow != null);
                    
                    if (req.ApprovalWorkflow != null)
                    {
                        foreach (var step in req.ApprovalWorkflow.ApprovalSteps ?? new List<ApprovalStep>())
                        {
                            _logger.LogInformation("  Step {StepNum}: Email={Email}, Status={Status}, IsActive={IsActive}",
                                step.StepNumber, step.ApproverEmail, step.Status, step.IsActive);
                        }
                    }
                }
                
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

                // Get approval history from ApprovalNotifications
                var approvalHistory = await _context.ApprovalNotifications
                    .Where(n => n.RequestId == id)
                    .OrderBy(n => n.SentDate)
                    .ToListAsync();

                var viewModel = new ApprovalDetailsViewModel
                {
                    Request = request,
                    CanApprove = canApprove,
                    CurrentUserEmail = currentUser,
                    ReturnUrl = returnUrl,
                    ApprovalHistory = approvalHistory
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

                // Handle multi-approval workflow vs single approval workflow
                if (request.ApprovalWorkflow != null)
                {
                    // Multi-approval workflow: Find the next approvable step for this user
                    var approvableSteps = request.ApprovalWorkflow.ApprovalSteps
                        .Where(s => s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                        .Where(s => {
                            if (request.ApprovalWorkflow.WorkflowType == "Sequential")
                                return s.IsActive && (s.Status == "Pending" || s.Status == "InProgress");
                            else // Parallel
                                return s.Status == "Pending" || s.Status == "InProgress";
                        })
                        .OrderBy(s => s.StepNumber)
                        .ToList();
                    
                    if (approvableSteps.Any())
                    {
                        // Approve the first available step (lowest step number)
                        var stepToApprove = approvableSteps.First();
                        var success = await _approvalWorkflowService.ProcessApprovalStepAsync(stepToApprove.Id, currentUser, "Approved", model.Comments);
                        if (!success)
                        {
                            TempData["ErrorMessage"] = "Failed to process approval step.";
                            return RedirectToAction("Details", new { id = model.RequestId });
                        }
                        
                        // Log which step was approved for debugging
                        _logger.LogInformation("User {User} approved step {StepNumber} of request {RequestId}", 
                            currentUser, stepToApprove.StepNumber, request.RequestId);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "No approvable steps found for your user.";
                        return RedirectToAction("Details", new { id = model.RequestId });
                    }
                }
                else
                {
                    // Legacy single-approver workflow: Update request status directly
                    request.Status = "Approved";
                    request.ModifiedDate = DateTime.UtcNow;
                    request.ModifiedBy = currentUserName;
                    request.ApprovedBy = currentUser;
                    request.ApprovedDate = DateTime.UtcNow;
                    request.ApprovalComments = model.Comments;

                    // Add approval record
                    await AddApprovalRecordAsync(request.Id, currentUser, currentUserName, "Approved", model.Comments);

                    await _context.SaveChangesAsync();
                }

                // Send notification
                await SendApprovalNotificationAsync(request, NotificationType.Approved, model.Comments);

                // Different success messages for single vs multi-approval workflows
                if (request.ApprovalWorkflow != null)
                {
                    // Reload to check final status after workflow processing
                    await _context.Entry(request).ReloadAsync();
                    await _context.Entry(request.ApprovalWorkflow).ReloadAsync();
                    
                    // Count remaining approvable steps for this user
                    var remainingUserSteps = request.ApprovalWorkflow.ApprovalSteps
                        .Where(s => s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                        .Where(s => s.Status == "Pending" || s.Status == "InProgress")
                        .Count();
                    
                    if (request.Status == "Approved")
                    {
                        TempData["SuccessMessage"] = $"Trading limit request {request.RequestId} has been fully approved and completed.";
                    }
                    else if (remainingUserSteps > 0)
                    {
                        TempData["SuccessMessage"] = $"Your approval has been recorded for trading limit request {request.RequestId}. You have {remainingUserSteps} more step(s) to approve.";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"Your approval for trading limit request {request.RequestId} has been recorded. The workflow will continue to the next approver.";
                    }
                }
                else
                {
                    TempData["SuccessMessage"] = $"Trading limit request {request.RequestId} has been approved successfully.";
                }
                
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

                // Handle multi-approval workflow vs single approval workflow
                if (request.ApprovalWorkflow != null)
                {
                    // Multi-approval workflow: Find the next approvable step for this user
                    var approvableSteps = request.ApprovalWorkflow.ApprovalSteps
                        .Where(s => s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                        .Where(s => {
                            if (request.ApprovalWorkflow.WorkflowType == "Sequential")
                                return s.IsActive && (s.Status == "Pending" || s.Status == "InProgress");
                            else // Parallel
                                return s.Status == "Pending" || s.Status == "InProgress";
                        })
                        .OrderBy(s => s.StepNumber)
                        .ToList();
                    
                    if (approvableSteps.Any())
                    {
                        // Reject the first available step (lowest step number)
                        var stepToReject = approvableSteps.First();
                        var success = await _approvalWorkflowService.ProcessApprovalStepAsync(stepToReject.Id, currentUser, "Rejected", model.Comments);
                        if (!success)
                        {
                            TempData["ErrorMessage"] = "Failed to process rejection step.";
                            return RedirectToAction("Details", new { id = model.RequestId });
                        }
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "No approvable steps found for your user.";
                        return RedirectToAction("Details", new { id = model.RequestId });
                    }
                }
                else
                {
                    // Legacy single-approver workflow: Update request status directly
                    request.Status = "Rejected";
                    request.ModifiedDate = DateTime.UtcNow;
                    request.ModifiedBy = currentUserName;
                    request.ApprovedBy = currentUser;
                    request.ApprovedDate = DateTime.UtcNow;
                    request.ApprovalComments = model.Comments;

                    // Add approval record
                    await AddApprovalRecordAsync(request.Id, currentUser, currentUserName, "Rejected", model.Comments);

                    await _context.SaveChangesAsync();
                }

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

                // Handle multi-approval workflow vs single approval workflow
                if (request.ApprovalWorkflow != null)
                {
                    // Multi-approval workflow: Find the next approvable step for this user
                    var approvableSteps = request.ApprovalWorkflow.ApprovalSteps
                        .Where(s => s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                        .Where(s => {
                            if (request.ApprovalWorkflow.WorkflowType == "Sequential")
                                return s.IsActive && (s.Status == "Pending" || s.Status == "InProgress");
                            else // Parallel
                                return s.Status == "Pending" || s.Status == "InProgress";
                        })
                        .OrderBy(s => s.StepNumber)
                        .ToList();
                    
                    if (approvableSteps.Any())
                    {
                        // Request revision on the first available step (lowest step number)
                        var stepToRevise = approvableSteps.First();
                        var success = await _approvalWorkflowService.ProcessApprovalStepAsync(stepToRevise.Id, currentUser, "Revision Required", model.Comments);
                        if (!success)
                        {
                            TempData["ErrorMessage"] = "Failed to process revision request.";
                            return RedirectToAction("Details", new { id = model.RequestId });
                        }
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "No approvable steps found for your user.";
                        return RedirectToAction("Details", new { id = model.RequestId });
                    }
                }
                else
                {
                    // Legacy single-approver workflow: Update request status directly
                    request.Status = "Revision Required";
                    request.ModifiedDate = DateTime.UtcNow;
                    request.ModifiedBy = currentUserName;
                    request.ApprovedBy = currentUser;
                    request.ApprovedDate = DateTime.UtcNow;
                    request.ApprovalComments = model.Comments;

                    // Add approval record
                    await AddApprovalRecordAsync(request.Id, currentUser, currentUserName, "Revision Required", model.Comments);

                    await _context.SaveChangesAsync();
                }

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
                // Multi-approval workflow: Check if user has any active approval steps
                // (User might have multiple steps assigned to them)
                var userSteps = workflow.ApprovalSteps
                    .Where(s => s.ApproverEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!userSteps.Any())
                {
                    return false; // User is not an approver in this workflow
                }

                // Check if any of the user's steps can be approved
                foreach (var userStep in userSteps)
                {
                    // For sequential workflows, only active steps can be approved
                    if (workflow.WorkflowType == "Sequential")
                    {
                        if (userStep.IsActive && (userStep.Status == "Pending" || userStep.Status == "InProgress"))
                        {
                            return true;
                        }
                    }
                    // For parallel workflows, any pending step can be approved
                    else if (workflow.WorkflowType == "Parallel")
                    {
                        if (userStep.Status == "Pending" || userStep.Status == "InProgress")
                        {
                            return true;
                        }
                    }
                }
                
                return false; // No approvable steps found for this user
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

        // GET: Approval/SelectStep/{id} - For users with multiple approvable steps
        [HttpGet("SelectStep/{id}")]
        public async Task<IActionResult> SelectStep(int id)
        {
            try
            {
                var request = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request?.ApprovalWorkflow == null)
                {
                    TempData["ErrorMessage"] = "Request or workflow not found.";
                    return RedirectToAction("Details", new { id });
                }

                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                
                // Find all approvable steps for this user
                var approvableSteps = request.ApprovalWorkflow.ApprovalSteps
                    .Where(s => s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                    .Where(s => {
                        if (request.ApprovalWorkflow.WorkflowType == "Sequential")
                            return s.IsActive && (s.Status == "Pending" || s.Status == "InProgress");
                        else // Parallel
                            return s.Status == "Pending" || s.Status == "InProgress";
                    })
                    .OrderBy(s => s.StepNumber)
                    .ToList();

                if (approvableSteps.Count <= 1)
                {
                    // If only one step, redirect to normal details page
                    return RedirectToAction("Details", new { id });
                }

                var viewModel = new SelectApprovalStepViewModel
                {
                    Request = request,
                    ApprovableSteps = approvableSteps,
                    CurrentUserEmail = currentUser
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading step selection for request {RequestId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading step selection.";
                return RedirectToAction("Details", new { id });
            }
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

        // GET: Get user's pending approval steps for a specific request (AJAX)
        [HttpGet("GetUserSteps/{id}")]
        public async Task<IActionResult> GetUserSteps(int id)
        {
            try
            {
                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                
                var request = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request?.ApprovalWorkflow == null)
                {
                    return Json(new { success = false, message = "Request or workflow not found." });
                }

                var userSteps = request.ApprovalWorkflow.ApprovalSteps
                    .Where(s => s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                    .Select(s => new {
                        s.Id,
                        s.StepNumber,
                        s.ApproverRole,
                        s.Status,
                        s.IsActive,
                        s.IsRequired,
                        s.Comments,
                        s.ActionDate,
                        CanApprove = (request.ApprovalWorkflow.WorkflowType == "Sequential" ? s.IsActive : true) && 
                                   (s.Status == "Pending" || s.Status == "InProgress")
                    })
                    .OrderBy(s => s.StepNumber)
                    .ToList();

                return Json(new { 
                    success = true, 
                    steps = userSteps,
                    workflowType = request.ApprovalWorkflow.WorkflowType,
                    canApproveAny = userSteps.Any(s => s.CanApprove)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user steps for request {RequestId}", id);
                return Json(new { success = false, message = "An error occurred." });
            }
        }

        // DEBUG: Test email comparison logic
        [HttpGet("TestEmail")]
        public async Task<IActionResult> TestEmail(string? testEmail = null)
        {
            var currentUser = await _generalService.GetCurrentUserEmailAsync();
            testEmail = testEmail ?? currentUser;
            
            var comparisonResults = new
            {
                CurrentUser = currentUser,
                TestEmail = testEmail,
                AreEqual_CaseSensitive = currentUser == testEmail,
                AreEqual_IgnoreCase = currentUser.Equals(testEmail, StringComparison.OrdinalIgnoreCase),
                CurrentUser_Lower = currentUser.ToLower(),
                TestEmail_Lower = testEmail?.ToLower(),
                AreEqual_ToLower = currentUser.ToLower() == testEmail?.ToLower()
            };
            
            return Json(comparisonResults);
        }

        // DEBUG: Temporary debug endpoint to check database state
        [HttpGet("Debug")]
        public async Task<IActionResult> Debug()
        {
            try
            {
                var currentUser = await _generalService.GetCurrentUserEmailAsync();
                
                var allRequests = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .ToListAsync();
                
                var debugInfo = new
                {
                    CurrentUser = currentUser,
                    TotalRequests = allRequests.Count,
                    Requests = allRequests.Select(r => new
                    {
                        r.Id,
                        r.RequestId,
                        r.Status,
                        r.ApprovalEmail,
                        r.CreatedBy,
                        r.SubmittedBy,
                        HasWorkflow = r.ApprovalWorkflow != null,
                        WorkflowStatus = r.ApprovalWorkflow?.Status,
                        WorkflowType = r.ApprovalWorkflow?.WorkflowType,
                        CurrentStep = r.ApprovalWorkflow?.CurrentStep,
                        Steps = r.ApprovalWorkflow?.ApprovalSteps?.Select(s => new
                        {
                            s.Id,
                            s.StepNumber,
                            s.ApproverEmail,
                            s.Status,
                            s.IsActive,
                            s.IsRequired,
                            s.ActionDate,
                            s.Comments,
                            IsCurrentUserStep = s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase)
                        }).ToList(),
                        CurrentUserSteps = r.ApprovalWorkflow?.ApprovalSteps?
                            .Where(s => s.ApproverEmail.Equals(currentUser, StringComparison.OrdinalIgnoreCase))
                            .Select(s => new {
                                s.Id,
                                s.StepNumber,
                                s.Status,
                                s.IsActive,
                                CanApprove = (r.ApprovalWorkflow.WorkflowType == "Sequential" ? s.IsActive : true) && 
                                           (s.Status == "Pending" || s.Status == "InProgress")
                            }).ToList()
                    }).ToList()
                };
                
                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
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

    public class SelectApprovalStepViewModel
    {
        public TradingLimitRequest Request { get; set; } = new();
        public List<ApprovalStep> ApprovableSteps { get; set; } = new();
        public string CurrentUserEmail { get; set; } = string.Empty;
    }
}