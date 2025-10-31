using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Services;
using System.Security.Claims;
using TradingLimitMVC.Models.AppSettings;
using Azure.Core;
using Microsoft.Extensions.Options;
using TradingLimitMVC.Helpers;

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
        private readonly IEmailService _emailService;
        private readonly IOptionsSnapshot<GeneralAppSetting> _generalAppSetting;

        public ApprovalController(
            ApplicationDbContext context,
            IEmailService emailService,
            IOptionsSnapshot<GeneralAppSetting> generalAppSetting,
            ITradingLimitRequestService tradingLimitRequestService,
            IApprovalWorkflowService approvalWorkflowService,
            ILogger<ApprovalController> logger,
            IGeneralService generalService)
        {
            _context = context;
            _emailService = emailService;
            _generalAppSetting = generalAppSetting;
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
                    // Check if user's group has already approved this step
                    var currentUserGroupId = await GetUserGroupIdAsync(currentUser);
                    var currentStepNumber = request.ApprovalWorkflow.CurrentStep;
                    
                    // Check if someone from the same group has already approved the current step
                    var groupAlreadyApproved = await CheckGroupApprovalStatusAsync(request.Id, currentStepNumber, currentUserGroupId);
                    
                    if (groupAlreadyApproved)
                    {
                        TempData["InfoMessage"] = $"Your group has already provided approval for step {currentStepNumber}. The workflow will continue to the next step.";
                        _logger.LogInformation("User {User} from group {GroupId} attempted to approve step {StepNumber} but group already approved", 
                            currentUser, currentUserGroupId, currentStepNumber);
                        return RedirectToAction("Details", new { id = model.RequestId });
                    }

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
                        
                        // Update approval group information if not already set
                        if (!stepToApprove.ApprovalGroupId.HasValue)
                        {
                            stepToApprove.ApprovalGroupId = currentUserGroupId;
                            
                            // Get group name from GroupSetting
                            if (currentUserGroupId.HasValue)
                            {
                                var groupSetting = await _context.GroupSettings
                                    .FirstOrDefaultAsync(gs => gs.GroupID == currentUserGroupId.Value);
                                stepToApprove.ApprovalGroupName = groupSetting?.GroupName ?? "Unknown Group";
                            }
                        }
                        
                        var success = await _approvalWorkflowService.ProcessApprovalStepAsync(stepToApprove.Id, currentUser, "Approved", model.Comments);
                        if (!success)
                        {
                            TempData["ErrorMessage"] = "Failed to process approval step.";
                            return RedirectToAction("Details", new { id = model.RequestId });
                        }
                        
                        // After processing approval, check if we can advance to next step
                        await CheckAndAdvanceWorkflowAsync(request.Id, stepToApprove.StepNumber);
                        
                        // Log which step was approved for debugging
                        _logger.LogInformation("User {User} (Group: {GroupId}) approved step {StepNumber} of request {RequestId}", 
                            currentUser, currentUserGroupId, stepToApprove.StepNumber, request.RequestId);
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
                    request.ModifiedDate = DateTimeHelper.GetCurrentLocalTime();
                    request.ModifiedBy = currentUserName;
                    request.ApprovedBy = currentUser;
                    request.ApprovedDate = DateTimeHelper.GetCurrentLocalTime();
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
                    request.ModifiedDate = DateTimeHelper.GetCurrentLocalTime();
                    request.ModifiedBy = currentUserName;
                    request.ApprovedBy = currentUser;
                    request.ApprovedDate = DateTimeHelper.GetCurrentLocalTime();
                    request.ApprovalComments = model.Comments;

                    // Add approval record
                    await AddApprovalRecordAsync(request.Id, currentUser, currentUserName, "Rejected", model.Comments);

                    await _context.SaveChangesAsync();
                }

                // Send notification
                //await SendApprovalNotificationAsync(request, NotificationType.Rejected, model.Comments);

                //Send email
                var submittedByEmail = request.SubmittedByEmail;
                await _emailService.SendApprovalCompletedEmail(request, submittedByEmail);

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
                    request.ModifiedDate = DateTimeHelper.GetCurrentLocalTime();
                    request.ModifiedBy = currentUserName;
                    request.ApprovedBy = currentUser;
                    request.ApprovedDate = DateTimeHelper.GetCurrentLocalTime();
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

        // Get user's group ID from GroupSetting table
        private async Task<int?> GetUserGroupIdAsync(string userEmail)
        {
            try
            {
                var groupSetting = await _context.GroupSettings
                    .Where(gs => gs.Email.Equals(userEmail, StringComparison.OrdinalIgnoreCase) && 
                                (gs.TypeID == 1 || gs.TypeID == 2)) // Approver or Endorser
                    .FirstOrDefaultAsync();
                
                return groupSetting?.GroupID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group ID for user {UserEmail}", userEmail);
                return null;
            }
        }

        // Check if someone from the same group has already approved the current step
        private async Task<bool> CheckGroupApprovalStatusAsync(int requestId, int stepNumber, int? groupId)
        {
            if (!groupId.HasValue)
            {
                return false;
            }

            try
            {
                // Enhanced: Use ApprovalGroupId field for more efficient checking
                var groupAlreadyApproved = await _context.ApprovalSteps
                    .AnyAsync(s => s.ApprovalWorkflow.TradingLimitRequestId == requestId && 
                                  s.StepNumber == stepNumber &&
                                  s.Status == "Approved" &&
                                  s.ApprovalGroupId == groupId);

                if (groupAlreadyApproved)
                {
                    _logger.LogInformation("Group {GroupId} already approved step {StepNumber} for request {RequestId}", 
                        groupId, stepNumber, requestId);
                    return true;
                }

                // Fallback: Check by email lookup if ApprovalGroupId is not set (for legacy data)
                var currentStepApprovals = await _context.ApprovalSteps
                    .Where(s => s.ApprovalWorkflow.TradingLimitRequestId == requestId && 
                               s.StepNumber == stepNumber &&
                               s.Status == "Approved" &&
                               s.ApprovalGroupId == null) // Only check legacy data without group ID
                    .ToListAsync();

                foreach (var approval in currentStepApprovals)
                {
                    var approverGroupId = await GetUserGroupIdAsync(approval.ApproverEmail);
                    if (approverGroupId == groupId)
                    {
                        _logger.LogInformation("Group {GroupId} already approved step {StepNumber} for request {RequestId} (legacy check)", 
                            groupId, stepNumber, requestId);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking group approval status for request {RequestId}, step {StepNumber}, group {GroupId}", 
                    requestId, stepNumber, groupId);
                return false;
            }
        }

        // Check if workflow can advance to next step after an approval
        private async Task CheckAndAdvanceWorkflowAsync(int requestId, int currentStepNumber)
        {
            try
            {
                var workflow = await _context.ApprovalWorkflows
                    .Include(w => w.ApprovalSteps)
                    .FirstOrDefaultAsync(w => w.TradingLimitRequestId == requestId);

                if (workflow == null)
                {
                    return;
                }

                // Get all steps for the current step number
                var currentStepApprovals = workflow.ApprovalSteps
                    .Where(s => s.StepNumber == currentStepNumber)
                    .ToList();

                // Check if step requirements are met based on approval type
                bool stepComplete = await IsStepCompleteAsync(currentStepApprovals, currentStepNumber);

                if (stepComplete)
                {
                    // Mark all remaining pending steps in current step as skipped (since group requirement is met)
                    var remainingSteps = currentStepApprovals.Where(s => s.Status == "Pending" || s.Status == "InProgress").ToList();
                    foreach (var step in remainingSteps)
                    {
                        step.Status = "Skipped";
                        step.Comments = $"Skipped - Group {step.ApprovalGroupName ?? "requirement"} already met in this step";
                        step.ActionDate = DateTimeHelper.GetCurrentLocalTime();
                        
                        _logger.LogInformation("Skipped step {StepId} for {ApproverEmail} in step {StepNumber} - group requirement met", 
                            step.Id, step.ApproverEmail, step.StepNumber);
                    }

                    // Check and skip future steps from same groups that already approved
                    CheckAndSkipSameGroupNextSteps(workflow, currentStepNumber);
                    
                    // Activate next appropriate step (this will now handle skipped steps properly)
                    await ActivateNextStepAsync(workflow, currentStepNumber);
                    
                    await _context.SaveChangesAsync();

                    //Send Email declaration
                    var tradingLimitRequest = await _context.TradingLimitRequests.FirstOrDefaultAsync(x => x.Id == requestId);

                    // Verify workflow advancement
                    var nextActiveStep = FindNextActiveStepNumber(workflow, currentStepNumber);
                    if (nextActiveStep.HasValue)
                    {
                        //Send Email
                        var approvalStep = workflow.ApprovalSteps.FirstOrDefault(x => x.StepNumber == nextActiveStep);

                        if (tradingLimitRequest != null && approvalStep != null)
                        {
                            var approverEmail = approvalStep.ApproverEmail;
                            var observerEmails = await _context.GroupSettings.Where(x => x.GroupID == approvalStep.ApprovalGroupId && x.TypeID == 3).Select(x => x.Email).ToListAsync();
                            observerEmails.Add(tradingLimitRequest.SubmittedByEmail);

                            await _emailService.SendApprovalEmail(tradingLimitRequest, approverEmail, observerEmails);
                        }

                        _logger.LogInformation("Successfully advanced workflow for request {RequestId} from step {CurrentStep} to step {NextStep}", 
                            requestId, currentStepNumber, nextActiveStep.Value);
                    }
                    else
                    {
                        //Send Email
                        if (tradingLimitRequest != null)
                        {
                            var submittedByEmail = tradingLimitRequest.SubmittedByEmail;

                            await _emailService.SendApprovalCompletedEmail(tradingLimitRequest, submittedByEmail);
                        }

                        _logger.LogInformation("Workflow for request {RequestId} completed after step {CurrentStep} - no more active steps", 
                            requestId, currentStepNumber);
                    }
                }
                else
                {
                    _logger.LogInformation("Step {StepNumber} for request {RequestId} not yet complete - waiting for more approvals", 
                        currentStepNumber, requestId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error advancing workflow for request {RequestId} from step {StepNumber}", 
                    requestId, currentStepNumber);
            }
        }

        // Check and skip next steps if they belong to the same group and role that already approved
        private void CheckAndSkipSameGroupNextSteps(ApprovalWorkflow workflow, int currentStepNumber)
        {
            try
            {
                // Get groups and roles that approved the current step (both must be present)
                var approvedGroupRoles = workflow.ApprovalSteps
                    .Where(s => s.StepNumber == currentStepNumber && s.Status == "Approved")
                    .Select(s => new { 
                        GroupId = s.ApprovalGroupId, 
                        Role = s.ApproverRole 
                    })
                    .Where(gr => gr.GroupId.HasValue && !string.IsNullOrEmpty(gr.Role))
                    .ToList();

                if (!approvedGroupRoles.Any())
                {
                    _logger.LogInformation("No complete group+role information available for current step {StepNumber} in workflow {WorkflowId} - skipping group+role-based step skipping", 
                        currentStepNumber, workflow.Id);
                    return;
                }

                var nextStepNumber = currentStepNumber + 1;
                var maxSteps = workflow.ApprovalSteps.Max(s => s.StepNumber);
                int totalStepsSkipped = 0;

                var approvedGroups = approvedGroupRoles.Where(gr => gr.GroupId.HasValue).Select(gr => gr.GroupId!.Value).ToHashSet();
                var approvedRoles = approvedGroupRoles.Where(gr => !string.IsNullOrEmpty(gr.Role)).Select(gr => gr.Role!).ToHashSet();

                _logger.LogInformation("Starting group+role-based step skipping for workflow {WorkflowId}. Group+role combinations that approved step {StepNumber}: [{GroupRoleCombos}]", 
                    workflow.Id, currentStepNumber, 
                    string.Join(", ", approvedGroupRoles.Select(gr => $"Group:{gr.GroupId}+Role:{gr.Role}")));

                // Keep checking subsequent steps for same group+role combinations
                while (nextStepNumber <= maxSteps)
                {
                    var nextSteps = workflow.ApprovalSteps
                        .Where(s => s.StepNumber == nextStepNumber)
                        .ToList();

                    if (!nextSteps.Any())
                    {
                        nextStepNumber++;
                        continue;
                    }

                    // Check if steps belong to groups AND roles that already approved
                    var stepsToSkip = nextSteps.Where(step => 
                        (step.ApprovalGroupId.HasValue && approvedGroups.Contains(step.ApprovalGroupId.Value)) &&
                        (!string.IsNullOrEmpty(step.ApproverRole) && approvedRoles.Contains(step.ApproverRole)) &&
                        (step.Status == "Pending" || step.Status == "InProgress")).ToList();

                    if (stepsToSkip.Any())
                    {
                        // Skip steps from groups AND roles that already approved (both criteria must match)
                        foreach (var step in stepsToSkip)
                        {
                            string skipReason = $"Group {step.ApprovalGroupName ?? step.ApprovalGroupId?.ToString() ?? "Unknown"} with role '{step.ApproverRole}' already approved in step {currentStepNumber}";
                            
                            step.Status = "Skipped";
                            step.Comments = $"Skipped - {skipReason}";
                            step.ActionDate = DateTimeHelper.GetCurrentLocalTime();
                            totalStepsSkipped++;
                            
                            _logger.LogInformation("Skipped step {StepId} (Step {StepNumber}) for {ApproverEmail} from group {GroupName} with role {Role} - {SkipReason}", 
                                step.Id, step.StepNumber, step.ApproverEmail, step.ApprovalGroupName, step.ApproverRole, skipReason);
                        }

                        // Check if ALL steps in this step number were skipped
                        bool allStepsInNumberSkipped = nextSteps.All(s => 
                            s.Status == "Skipped" || s.Status == "Approved" || s.Status == "Rejected");

                        if (allStepsInNumberSkipped)
                        {
                            _logger.LogInformation("All steps in step number {StepNumber} were skipped or completed for workflow {WorkflowId}", 
                                nextStepNumber, workflow.Id);
                        }
                    }

                    // Check if there are still active steps in this step number (from different group+role combinations)
                    bool hasRemainingActiveSteps = nextSteps.Any(step => 
                        step.Status == "Pending" || step.Status == "InProgress");

                    if (!hasRemainingActiveSteps)
                    {
                        // All steps in this number are processed, continue to next number
                        nextStepNumber++;
                    }
                    else
                    {
                        // Found steps from different group+role combinations that need processing, stop skipping
                        _logger.LogInformation("Found remaining active steps in step {StepNumber} from different group+role combinations - stopping skip process", 
                            nextStepNumber);
                        break;
                    }
                }

                if (totalStepsSkipped > 0)
                {
                    _logger.LogInformation("Group+role-based skipping completed for workflow {WorkflowId}: {SkippedCount} steps skipped due to same-group AND same-role approval", 
                        workflow.Id, totalStepsSkipped);
                }
                else
                {
                    _logger.LogInformation("No steps were skipped for workflow {WorkflowId} - no matching group+role combinations found in future steps", 
                        workflow.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and skipping same group/role steps for workflow {WorkflowId}", workflow.Id);
            }
        }

        // Check if a step is complete based on group and role-based approval logic
        private async Task<bool> IsStepCompleteAsync(List<ApprovalStep> currentStepApprovals, int stepNumber)
        {
            try
            {
                // Enhanced: Use ApprovalGroupId and ApproverRole fields for more efficient checking
                var approvedGroups = currentStepApprovals
                    .Where(a => a.Status == "Approved" && a.ApprovalGroupId.HasValue)
                    .Select(a => a.ApprovalGroupId!.Value)
                    .ToHashSet();

                var requiredGroups = currentStepApprovals
                    .Where(s => s.ApprovalGroupId.HasValue)
                    .Select(s => s.ApprovalGroupId!.Value)
                    .ToHashSet();

                // Also check for role-based approvals
                var approvedRoles = currentStepApprovals
                    .Where(a => a.Status == "Approved" && !string.IsNullOrEmpty(a.ApproverRole))
                    .Select(a => a.ApproverRole!)
                    .ToHashSet();

                var requiredRoles = currentStepApprovals
                    .Where(s => !string.IsNullOrEmpty(s.ApproverRole))
                    .Select(s => s.ApproverRole!)
                    .ToHashSet();

                // Fallback for legacy data without ApprovalGroupId
                if (!approvedGroups.Any() || !requiredGroups.Any())
                {
                    var legacyApprovedGroups = new HashSet<int?>();
                    var legacyRequiredGroups = new HashSet<int?>();
                    
                    foreach (var approval in currentStepApprovals.Where(a => a.Status == "Approved"))
                    {
                        var approverGroupId = await GetUserGroupIdAsync(approval.ApproverEmail);
                        if (approverGroupId.HasValue)
                        {
                            legacyApprovedGroups.Add(approverGroupId);
                        }
                    }

                    foreach (var step in currentStepApprovals)
                    {
                        var groupId = await GetUserGroupIdAsync(step.ApproverEmail);
                        if (groupId.HasValue)
                        {
                            legacyRequiredGroups.Add(groupId);
                        }
                    }

                    // Convert to int HashSet for consistency
                    approvedGroups = legacyApprovedGroups.Where(g => g.HasValue).Select(g => g!.Value).ToHashSet();
                    requiredGroups = legacyRequiredGroups.Where(g => g.HasValue).Select(g => g!.Value).ToHashSet();
                }

                // Step is complete if we have at least one approval from any group OR any role
                // Option 1: Any one group or role approval completes the step (ParallelAnyOne)
                bool anyGroupApproved = approvedGroups.Count > 0;
                bool anyRoleApproved = approvedRoles.Count > 0;
                bool stepCompleteByGroupOrRole = anyGroupApproved || anyRoleApproved;
                
                // Option 2: All groups AND roles must approve (ParallelAll) - uncomment if needed
                // bool allGroupsApproved = requiredGroups.Count > 0 && approvedGroups.Count == requiredGroups.Count;
                // bool allRolesApproved = requiredRoles.Count > 0 && approvedRoles.Count == requiredRoles.Count;
                // bool stepCompleteByAll = allGroupsApproved && allRolesApproved;

                _logger.LogInformation("Step {StepNumber} completion status: Required groups: {RequiredGroups}, Approved groups: {ApprovedGroups}, Required roles: {RequiredRoles}, Approved roles: {ApprovedRoles}, Complete: {IsComplete}", 
                    stepNumber, requiredGroups.Count, approvedGroups.Count, requiredRoles.Count, approvedRoles.Count, stepCompleteByGroupOrRole);

                return stepCompleteByGroupOrRole; // Change to stepCompleteByAll if you need all groups AND roles to approve
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if step {StepNumber} is complete", stepNumber);
                return false;
            }
        }

        // Activate the next step in the workflow (handles skipped steps properly)
        private async Task ActivateNextStepAsync(ApprovalWorkflow workflow, int currentStepNumber)
        {
            try
            {
                var nextActiveStepNumber = FindNextActiveStepNumber(workflow, currentStepNumber);
                
                if (nextActiveStepNumber.HasValue)
                {
                    var nextSteps = workflow.ApprovalSteps
                        .Where(s => s.StepNumber == nextActiveStepNumber.Value)
                        .ToList();

                    // Activate all steps in the next active step number
                    foreach (var step in nextSteps)
                    {
                        if (step.Status == "Pending")
                        {
                            step.Status = "InProgress";
                            _logger.LogInformation("Activated step {StepId} (Step {StepNumber}) for approver {ApproverEmail} in workflow {WorkflowId}", 
                                step.Id, step.StepNumber, step.ApproverEmail, workflow.Id);
                        }
                    }

                    // Update workflow current step to the next active step
                    workflow.CurrentStep = nextActiveStepNumber.Value;
                    
                    _logger.LogInformation("Advanced workflow {WorkflowId} from step {CurrentStep} to step {NextStep} (skipped intermediate steps)", 
                        workflow.Id, currentStepNumber, nextActiveStepNumber.Value);
                }
                else
                {
                    // No more active steps - workflow is complete
                    workflow.Status = "Approved";
                    workflow.CompletedDate = DateTimeHelper.GetCurrentLocalTime();
                    
                    // Update the main request status
                    var request = await _context.TradingLimitRequests.FindAsync(workflow.TradingLimitRequestId);
                    if (request != null)
                    {
                        request.Status = "Approved";
                        request.ModifiedDate = DateTimeHelper.GetCurrentLocalTime();
                    }
                    
                    _logger.LogInformation("Workflow {WorkflowId} completed - all required steps processed (some may have been skipped)", workflow.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating next step after step {CurrentStepNumber} in workflow {WorkflowId}", 
                    currentStepNumber, workflow.Id);
            }
        }

        // Find the next step number that has non-skipped steps
        private int? FindNextActiveStepNumber(ApprovalWorkflow workflow, int currentStepNumber)
        {
            try
            {
                var maxStepNumber = workflow.ApprovalSteps.Max(s => s.StepNumber);
                
                // Look for the next step that has at least one non-skipped step
                for (int stepNumber = currentStepNumber + 1; stepNumber <= maxStepNumber; stepNumber++)
                {
                    var stepsInThisNumber = workflow.ApprovalSteps
                        .Where(s => s.StepNumber == stepNumber)
                        .ToList();
                    
                    if (stepsInThisNumber.Any())
                    {
                        // Check if this step has any non-skipped steps
                        bool hasActiveSteps = stepsInThisNumber.Any(s => 
                            s.Status == "Pending" || 
                            s.Status == "InProgress" || 
                            (!IsStepPermanentlySkipped(s) && s.Status != "Approved" && s.Status != "Rejected"));
                        
                        if (hasActiveSteps)
                        {
                            _logger.LogInformation("Found next active step: {StepNumber} for workflow {WorkflowId}", 
                                stepNumber, workflow.Id);
                            return stepNumber;
                        }
                        else
                        {
                            _logger.LogInformation("Step {StepNumber} in workflow {WorkflowId} has no active steps (all skipped/completed)", 
                                stepNumber, workflow.Id);
                        }
                    }
                }
                
                _logger.LogInformation("No more active steps found after step {CurrentStepNumber} in workflow {WorkflowId}", 
                    currentStepNumber, workflow.Id);
                return null; // No more active steps
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding next active step after step {CurrentStepNumber} in workflow {WorkflowId}", 
                    currentStepNumber, workflow.Id);
                return null;
            }
        }

        // Helper method to determine if a step is permanently skipped (vs temporarily inactive)
        private bool IsStepPermanentlySkipped(ApprovalStep step)
        {
            return step.Status == "Skipped" || 
                   (step.Comments != null && step.Comments.Contains("Skipped - Group")) ||
                   (step.Comments != null && step.Comments.Contains("already approved"));
        }

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
                SentDate = DateTimeHelper.GetCurrentLocalTime(),
                IsRead = true,
                ReadDate = DateTimeHelper.GetCurrentLocalTime()
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
                        SentDate = DateTimeHelper.GetCurrentLocalTime(),
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

        // Helper method to populate ApprovalGroupId for existing approval steps
        public async Task<IActionResult> PopulateApprovalGroups()
        {
            try
            {
                var stepsWithoutGroups = await _context.ApprovalSteps
                    .Where(s => s.ApprovalGroupId == null)
                    .ToListAsync();

                int updatedCount = 0;

                foreach (var step in stepsWithoutGroups)
                {
                    var groupId = await GetUserGroupIdAsync(step.ApproverEmail);
                    if (groupId.HasValue)
                    {
                        step.ApprovalGroupId = groupId;
                        
                        var groupSetting = await _context.GroupSettings
                            .FirstOrDefaultAsync(gs => gs.GroupID == groupId.Value);
                        step.ApprovalGroupName = groupSetting?.GroupName ?? $"Group {groupId}";
                        
                        updatedCount++;
                    }
                }

                if (updatedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated {Count} approval steps with group information", updatedCount);
                }

                return Json(new { 
                    success = true, 
                    message = $"Updated {updatedCount} approval steps with group information",
                    totalStepsProcessed = stepsWithoutGroups.Count,
                    updatedSteps = updatedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating approval groups");
                return Json(new { success = false, message = "Error updating approval groups: " + ex.Message });
            }
        }

        // Helper method to validate and fix workflow state if needed
        [HttpGet("ValidateWorkflowState/{requestId}")]
        public async Task<IActionResult> ValidateWorkflowState(int requestId)
        {
            try
            {
                var workflow = await _context.ApprovalWorkflows
                    .Include(w => w.ApprovalSteps)
                    .FirstOrDefaultAsync(w => w.TradingLimitRequestId == requestId);

                if (workflow == null)
                {
                    return Json(new { success = false, message = "Workflow not found" });
                }

                var validationResults = new List<string>();

                // Check if current step is valid
                var currentStepSteps = workflow.ApprovalSteps
                    .Where(s => s.StepNumber == workflow.CurrentStep)
                    .ToList();

                if (!currentStepSteps.Any())
                {
                    validationResults.Add($"Warning: Current step {workflow.CurrentStep} has no steps defined");
                }

                // Check if there are active steps in current step
                var activeStepsInCurrentStep = currentStepSteps
                    .Where(s => s.Status == "InProgress")
                    .ToList();

                if (!activeStepsInCurrentStep.Any() && workflow.Status != "Approved")
                {
                    validationResults.Add($"Issue: No active steps found in current step {workflow.CurrentStep}");
                    
                    // Try to find next valid step
                    var nextActiveStep = FindNextActiveStepNumber(workflow, workflow.CurrentStep - 1);
                    if (nextActiveStep.HasValue)
                    {
                        validationResults.Add($"Suggested fix: Advance to step {nextActiveStep.Value}");
                        
                        // Auto-fix if requested
                        workflow.CurrentStep = nextActiveStep.Value;
                        var stepsToActivate = workflow.ApprovalSteps
                            .Where(s => s.StepNumber == nextActiveStep.Value && s.Status == "Pending")
                            .ToList();
                        
                        foreach (var step in stepsToActivate)
                        {
                            step.Status = "InProgress";
                        }
                        
                        await _context.SaveChangesAsync();
                        validationResults.Add($"Fixed: Advanced workflow to step {nextActiveStep.Value}");
                    }
                    else if (workflow.ApprovalSteps.All(s => s.Status == "Approved" || s.Status == "Skipped" || s.Status == "Rejected"))
                    {
                        // All steps are complete
                        workflow.Status = "Approved";
                        workflow.CompletedDate = DateTimeHelper.GetCurrentLocalTime();
                        
                        var request = await _context.TradingLimitRequests.FindAsync(requestId);
                        if (request != null)
                        {
                            request.Status = "Approved";
                            request.ModifiedDate = DateTimeHelper.GetCurrentLocalTime();
                        }
                        
                        await _context.SaveChangesAsync();
                        validationResults.Add("Fixed: Marked workflow as completed");
                    }
                }

                // Provide workflow statistics
                var stepStats = workflow.ApprovalSteps
                    .GroupBy(s => s.Status)
                    .ToDictionary(g => g.Key, g => g.Count());

                return Json(new { 
                    success = true, 
                    workflowId = workflow.Id,
                    currentStep = workflow.CurrentStep,
                    workflowStatus = workflow.Status,
                    validationResults = validationResults,
                    stepStatistics = stepStats,
                    message = validationResults.Any() ? string.Join("; ", validationResults) : "Workflow state is valid"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating workflow state for request {RequestId}", requestId);
                return Json(new { success = false, message = "Error validating workflow: " + ex.Message });
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