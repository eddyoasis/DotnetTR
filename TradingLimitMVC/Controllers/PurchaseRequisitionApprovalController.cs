using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Models.AppSettings;
using TradingLimitMVC.Models.ViewModels;
using TradingLimitMVC.Services;
using System.Collections.Generic;

public class PurchaseRequisitionApprovalController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPowerAutomateService _powerAutomateService;
    private readonly ILogger<PurchaseRequisitionApprovalController> _logger;
    private readonly IApprovalWorkflowService _workflowService;
    private readonly IEmailService _emailService;
    private readonly IOptionsSnapshot<GeneralAppSetting> _generalAppSetting;



    public PurchaseRequisitionApprovalController(
        ApplicationDbContext context,
        IPowerAutomateService powerAutomateService, IApprovalWorkflowService workflowService,
        IEmailService emailService,
        IOptionsSnapshot<GeneralAppSetting> generalAppSetting,
        ILogger<PurchaseRequisitionApprovalController> logger)
    {
        _context = context;
        _powerAutomateService = powerAutomateService;
        _emailService = emailService;
        _generalAppSetting = generalAppSetting;
        _workflowService = workflowService;
        _logger = logger;
    }
    // GET: Approval/ApproveStep/{id}
    [HttpGet]
    public async Task<IActionResult> ApproveStep(int id, string approverRole = null)
    {
        try
        {
            _logger.LogInformation($" Loading PR {id} for approval");
            var pr = await _context.PurchaseRequisitions
                .Include(p => p.Items)
                .Include(p => p.CostCenters)
                .Include(p => p.WorkflowSteps)
                .Include(p => p.Approvals)
                .Include(p => p.Attachments)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pr == null)
            {
                _logger.LogWarning($" PR {id} not found");
                TempData["Error"] = "Purchase Requisition not found.";
                return RedirectToAction(nameof(Index));
            }
            //  Ensure collections are initialized
            pr.WorkflowSteps = pr.WorkflowSteps ?? new List<ApprovalWorkflowStep>();
            pr.Approvals = pr.Approvals ?? new List<PurchaseRequisitionApproval>();
            pr.Items = pr.Items ?? new List<PurchaseRequisitionItem>();
            pr.CostCenters = pr.CostCenters ?? new List<CostCenter>();
            pr.Attachments = pr.Attachments ?? new List<Attachment>();
            _logger.LogInformation($" PR {pr.PRReference}: {pr.WorkflowSteps.Count} steps, {pr.Approvals.Count} approvals");
            // Find the current pending step
            var currentStep = pr.WorkflowSteps
                .OrderBy(s => s.StepOrder)
                .FirstOrDefault(s => s.Status == ApprovalStatus.Pending && s.ApproverEmail.ToLower() == BaseService.Email.ToLower());
            if (currentStep == null)
            {
                _logger.LogWarning($" No pending step found for PR {id}");
                TempData["Error"] = "No pending approval found for this PR.";
                return RedirectToAction(nameof(Index));
            }
            _logger.LogInformation($" Current step: {currentStep.ApproverRole} - {currentStep.ApproverName}");
            var viewModel = new ApprovalViewModel
            {
                PurchaseRequisition = pr,
                Approval = new PurchaseRequisitionApproval
                {
                    PurchaseRequisitionId = pr.Id,
                    ApproverName = currentStep.ApproverName,
                    ApproverEmail = currentStep.ApproverEmail,
                    ApproverRole = currentStep.ApproverRole,
                    ApprovalLevel = currentStep.StepOrder,
                    Department = currentStep.Department ?? "Unknown"
                },
                ApprovalHistory = pr.Approvals.OrderBy(a => a.ApprovalDate).ToList(),
                Remarks = ""
            };
            ViewBag.CurrentApproverRole = approverRole ?? currentStep.ApproverRole;
            ViewBag.CurrentStep = currentStep;
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $" Error loading approval for PR {id}");
            TempData["Error"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: Approval/ProcessApprovalStep
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessApprovalStep(int id, string approverRole, string action, string comments)
    {
        try
        {
            //if (string.IsNullOrWhiteSpace(comments))
            //{
            //    TempData["Error"] = "Comments are required.";
            //    return RedirectToAction(nameof(ApproveStep), new { id, approverRole });
            //}
            var pr = await _context.PurchaseRequisitions
                .Include(p => p.WorkflowSteps)
                .Include(p => p.Approvals)
                .Include(p => p.CostCenters)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pr == null)
            {
                TempData["Error"] = "Purchase Requisition not found.";
                return RedirectToAction(nameof(Index));
            }
            // Find the current step for this approver
            //var currentStep = pr.WorkflowSteps
            //    .FirstOrDefault(s => s.ApproverRole == approverRole && s.Status == ApprovalStatus.Pending);
            var currentStep = pr.WorkflowSteps
                .FirstOrDefault(s => s.Status == ApprovalStatus.Pending && s.ApproverEmail.ToLower() == BaseService.Email.ToLower());
            if (currentStep == null)
            {
                TempData["Error"] = "No pending approval found for this role.";
                return RedirectToAction(nameof(Index));
            }
            // Determine approval status
            var status = action.ToLower() == "approve" ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            // Update workflow step
            currentStep.Status = status;
            currentStep.ActionDate = DateTime.Now;
            currentStep.Comments = comments;
            // Add approval record
            var approval = new PurchaseRequisitionApproval
            {
                PurchaseRequisitionId = id,
                ApproverName = currentStep.ApproverName,
                ApproverEmail = currentStep.ApproverEmail,
                ApproverRole = currentStep.ApproverRole,
                Status = status,
                Comments = comments ?? string.Empty,
                ApprovalDate = DateTime.Now,
                ApprovalLevel = currentStep.StepOrder,
                Department = currentStep.Department,
                ApprovalMethod = ApprovalMethod.Web
            };
            _context.PurchaseRequisitionApprovals.Add(approval);
            // Handle rejection
            if (status == ApprovalStatus.Rejected)
            {
                pr.CurrentStatus = WorkflowStatus.Rejected;
                pr.RejectedBy = currentStep.ApproverName;
                pr.RejectionReason = comments;
                pr.RejectedDate = DateTime.Now;
                // Mark all pending steps as skipped
                foreach (var step in pr.WorkflowSteps.Where(s => s.Status == ApprovalStatus.Pending))
                {
                    step.Status = ApprovalStatus.Skipped;
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = $"PR {pr.PRReference} has been rejected.";
                return RedirectToAction(nameof(Index));
            }
            // Handle approval - check if all steps at current level are approved
            var currentLevelSteps = pr.WorkflowSteps
                .Where(s => s.StepOrder == currentStep.StepOrder)
                .ToList();
            var allLevelApproved = currentLevelSteps.All(s => s.Status == ApprovalStatus.Approved);
            if (allLevelApproved)
            {
                // Move to next step
                pr.CurrentApprovalStep++;
                var nextSteps = pr.WorkflowSteps
                    .Where(s => s.StepOrder == pr.CurrentApprovalStep && s.Status == ApprovalStatus.Pending)
                    .ToList();
                if (nextSteps.Any())
                {
                    // More approvals needed
                    pr.CurrentStatus = GetStatusForStep(pr.CurrentApprovalStep);
                    TempData["Success"] = $"Approval successful! PR moved to next approval level.";
                    await SendEmail(pr, nextSteps.FirstOrDefault().ApproverEmail);
                }
                else
                {
                    // All approvals complete - redirect to PO creation
                    pr.CurrentStatus = WorkflowStatus.Approved;
                    pr.FinalApprovalDate = DateTime.Now;
                    pr.FinalApprover = currentStep.ApproverName;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"PR {pr.PRReference} fully approved! Redirecting to create Purchase Order.";
                    return RedirectToAction("CreateFromPR", "PurchaseOrder", new { prId = pr.Id });
                }
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Your approval has been recorded for PR {pr.PRReference}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approval step");
            TempData["Error"] = "Error processing approval.";
            return RedirectToAction(nameof(Index));
        }
    }

    private WorkflowStatus GetStatusForStep(int stepOrder)
    {
        return stepOrder switch
        {
            1 => WorkflowStatus.Submitted,
            2 => WorkflowStatus.ManagerApproval,
            3 => WorkflowStatus.HODApproval,
            4 => WorkflowStatus.FinanceOfficerApproval,
            _ => WorkflowStatus.Approved
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessApproval(int id, string approverEmail, string action, string comments)
    {
        try
        {
            var status = action.ToLower() == "approve" ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            var success = await _workflowService.ProcessApprovalStepAsync(id, approverEmail, status, comments);
            if (success)
            {
                TempData["Success"] = $"PR {action}d successfully!";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["Error"] = "Failed to process approval";
                return RedirectToAction(nameof(Approve), new { id });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approval");
            TempData["Error"] = "An error occurred";
            return RedirectToAction(nameof(Index));
        }
    }



    // GET: PurchaseRequisitionApproval/Index - List of PRs pending approval
    public async Task<IActionResult> Index()
    {
        try
        {
            _logger.LogInformation(" Loading approval list...");
            //  Database connection check
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                _logger.LogError(" Cannot connect to database");
                TempData["Error"] = "Cannot connect to database. Please check your connection.";
                return View(new List<PurchaseRequisition>());
            }
            _logger.LogInformation(" Database connection OK");
            //  First, count total PRs
            var totalCount = await _context.PurchaseRequisitions.CountAsync();
            _logger.LogInformation($" Total PRs in database: {totalCount}");
            //  Get PRs without navigation properties first
            var pendingPRIds = await _context.PurchaseRequisitions
                .Where(pr => pr.CurrentStatus != WorkflowStatus.Draft &&
                            pr.CurrentStatus != WorkflowStatus.Approved &&
                            pr.CurrentStatus != WorkflowStatus.Rejected)
                .OrderByDescending(pr => pr.SubmittedDate)
                .Select(pr => pr.Id)
                .ToListAsync();
            _logger.LogInformation($" Found {pendingPRIds.Count} pending PR IDs");
            if (!pendingPRIds.Any())
            {
                _logger.LogInformation(" No pending PRs found");
                return View(new List<PurchaseRequisition>());
            }
            //  Now load each PR individually with error handling
            var pendingPRs = new List<PurchaseRequisition>();
            foreach (var prId in pendingPRIds)
            {
                try
                {
                    var pr = await _context.PurchaseRequisitions
                        .Include(p => p.Items)
                        .Include(p => p.Approvals)
                        .Include(p => p.WorkflowSteps)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == prId);
                    if (pr != null)
                    {
                        //  Initialize collections
                        pr.Approvals ??= new List<PurchaseRequisitionApproval>();
                        pr.WorkflowSteps ??= new List<ApprovalWorkflowStep>();
                        pr.Items ??= new List<PurchaseRequisitionItem>();
                        pendingPRs.Add(pr);
                        _logger.LogInformation($"   Loaded PR {pr.PRReference}: " +
                            $"{pr.Items.Count} items, {pr.WorkflowSteps.Count} steps");
                    }
                }
                catch (Exception prEx)
                {
                    _logger.LogError(prEx, $" Error loading PR {prId} - skipping");
                    // Continue with other PRs
                }
            }
            _logger.LogInformation($" Successfully loaded {pendingPRs.Count} PRs");
            return View(pendingPRs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Error in Index()");
            _logger.LogError($"Exception Type: {ex.GetType().Name}");
            _logger.LogError($"Message: {ex.Message}");
            _logger.LogError($"InnerException: {ex.InnerException?.Message}");
            TempData["Error"] = $"Error loading approval list: {ex.Message}";
            return View(new List<PurchaseRequisition>());
        }
    }


    // GET: PurchaseRequisitionApproval/Approve/5
    public async Task<IActionResult> Approve(int? id)
    {
        if (id == null) return NotFound();

        try
        {
            var purchaseRequisition = await _context.PurchaseRequisitions
                .Include(pr => pr.Items)
                .Include(pr => pr.Attachments)
                .Include(pr => pr.Approvals.OrderBy(a => a.ApprovalLevel))
                .FirstOrDefaultAsync(pr => pr.Id == id);

            if (purchaseRequisition == null) return NotFound();

            // Check if PR is in a state that allows approval
            if (purchaseRequisition.CurrentStatus == WorkflowStatus.Draft ||
                purchaseRequisition.CurrentStatus == WorkflowStatus.Approved ||
                purchaseRequisition.CurrentStatus == WorkflowStatus.Rejected)
            {
                TempData["Error"] = "This Purchase Requisition is not available for approval.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new ApprovalViewModel
            {
                PurchaseRequisition = purchaseRequisition,
                Approval = new PurchaseRequisitionApproval
                {
                    PurchaseRequisitionId = purchaseRequisition.Id,
                    ApprovalLevel = GetNextApprovalLevel(purchaseRequisition.CurrentStatus),
                    ApproverName = "Current User", // Replace with actual user
                    ApproverEmail = "approver@company.com", // Replace with actual user email
                    Status = ApprovalStatus.Pending,
                    Department = "Management" // Replace with actual user department
                },
                ApprovalHistory = purchaseRequisition.Approvals.ToList(),
                Remarks = ""
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading approval details for PR {PRId}", id);
            TempData["Error"] = "Error loading approval details. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: PurchaseRequisitionApproval/Approve
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApprovalViewModel model, string action)
    {
        //if (string.IsNullOrEmpty(model.Remarks))
        //{
        //    ModelState.AddModelError("Remarks", "Remarks are required for approval actions.");
        //}

        if (ModelState.IsValid)
        {
            try
            {
                var purchaseRequisition = await _context.PurchaseRequisitions
                    .Include(pr => pr.Approvals)
                    .FirstOrDefaultAsync(pr => pr.Id == model.Approval.PurchaseRequisitionId);

                if (purchaseRequisition == null) return NotFound();

                // Create approval record
                var approval = new PurchaseRequisitionApproval
                {
                    PurchaseRequisitionId = purchaseRequisition.Id,
                    ApproverName = BaseService.Username,
                    ApproverEmail = model.Approval.ApproverEmail,
                    ApprovalLevel = model.Approval.ApprovalLevel,
                    Department = model.Approval.Department,
                    Comments = model.Remarks ?? string.Empty,
                    ApprovalDate = DateTime.Now,
                    Status = action?.ToLower() switch
                    {
                        "approve" => ApprovalStatus.Approved,
                        "reject" => ApprovalStatus.Rejected,
                        "return" => ApprovalStatus.RequiresModification,
                        _ => ApprovalStatus.Pending
                    }
                };

                _context.PurchaseRequisitionApprovals.Add(approval);

                string message;
                string nextApproverEmail = "";

                // Update PR status based on action
                switch (action?.ToLower())
                {
                    case "approve":
                        var nextStatus = GetNextWorkflowStatus(purchaseRequisition.CurrentStatus);
                        purchaseRequisition.CurrentStatus = nextStatus;

                        if (nextStatus == WorkflowStatus.Approved)
                        {
                            purchaseRequisition.FinalApprovalDate = DateTime.Now;
                            purchaseRequisition.FinalApprover = model.Approval.ApproverName;
                            message = "Purchase Requisition fully approved successfully!";
                        }
                        else
                        {
                            // Get next approver email
                            nextApproverEmail = GetNextApproverEmailForStatus(nextStatus, purchaseRequisition);
                            message = "Purchase Requisition approved and forwarded to next level!";
                        }
                        break;
                    case "reject":
                        purchaseRequisition.CurrentStatus = WorkflowStatus.Rejected;
                        message = "Purchase Requisition rejected successfully!";
                        break;
                    case "return":
                        purchaseRequisition.CurrentStatus = WorkflowStatus.RequiresModification;
                        message = "Purchase Requisition returned for modification successfully!";
                        break;
                    default:
                        message = "Purchase Requisition processed successfully!";
                        break;
                }

                purchaseRequisition.CurrentApprover = model.Approval.ApproverName;

                await _context.SaveChangesAsync();

                // If there's a next approver, trigger workflow
                if (!string.IsNullOrEmpty(nextApproverEmail))
                {
                    try
                    {
                        await _powerAutomateService.TriggerApprovalWorkflowAsync(
                            nextApproverEmail, purchaseRequisition.PRReference);

                        message += " Next approver notified.";
                    }
                    catch (Exception workflowEx)
                    {
                        _logger.LogError(workflowEx, "Error triggering workflow for next approver");
                        message += " But notification to next approver failed.";
                    }
                }

                TempData["Success"] = message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing approval for PR {PRId}", model.Approval.PurchaseRequisitionId);
                TempData["Error"] = "Error processing approval. Please try again.";
            }
        }

        // If validation failed, reload the view with the same data
        return await Approve(model.Approval?.PurchaseRequisitionId);
    }

    private async Task SendEmail(PurchaseRequisition pr, string approverEmail)
    {
        var generalAppSetting = _generalAppSetting.Value;
        var domainHost = generalAppSetting.Host;
        var costcenterNames = pr.CostCenters.Select(x => x.Name);

        var recipientsTo = new List<string> { approverEmail };
        var recipientsCC = new List<string>();
        var subject = $"TEST [PENDING SG IT] PR: {pr.PRReference} ({string.Join(",", costcenterNames)}) - {pr.ShortDescription}";
        var body = $@"
                <p>Please refer to the purchase requisition below for your approval.<br/>
                Awaiting your action.</p>
                <p><a href='{domainHost}/Login?ReturnUrl={domainHost}/Approval/ApproveStep/{pr.Id}'>Click here to approve</a></p>
                <p>
                    <strong>Reference No:</strong> {pr.PRInternalNo}<br/>
                    <strong>Requested by:</strong> {pr.SubmittedBy}<br/>
                    <strong>Cost Centre:</strong> {string.Join(", ", costcenterNames)}<br/>
                    <strong>GST:</strong> {pr.QuotationCurrency} {pr.TotalAmount:N2}<br/>
                    <strong>Amount:</strong> {pr.QuotationCurrency} {pr.TotalAmount:N2}<br/>
                    <strong>Reason:</strong><br/>
                    {pr.Reason}
                </p>";


        await _emailService.SendEmailAsync(recipientsTo, recipientsCC, subject, body);
    }

    //private async Task SendEmailRequestor(PurchaseRequisition pr)
    //{
    //    var generalAppSetting = _generalAppSetting.Value;
    //    var domainHost = generalAppSetting.Host;
    //    var costcenterNames = pr.CostCenters.Select(x => x.Name);

    //    var recipientsTo = new List<string> { requestorEmail };
    //    var recipientsCC = new List<string>();
    //    var subject = $"TEST [Approved SG IT] PR: {pr.PRReference} ({string.Join(",", costcenterNames)}) - {pr.ShortDescription}";
    //    var body = $@"
    //            <p>Please refer to the purchase requisition detail below.<br/>
    //            Awaiting your action.</p>
    //            <p><a href='{domainHost}/Login?ReturnUrl={domainHost}/Approval/ApproveStep/{pr.Id}'>Click here to approve</a></p>
    //            <p>
    //                <strong>Reference No:</strong> {pr.PRInternalNo}<br/>
    //                <strong>Requested by:</strong> {pr.SubmittedBy}<br/>
    //                <strong>Cost Centre:</strong> {string.Join(", ", costcenterNames)}<br/>
    //                <strong>GST:</strong> SGD {pr.TotalAmount:N2}<br/>
    //                <strong>Amount:</strong> SGD {pr.TotalAmount:N2}<br/>
    //                <strong>Reason:</strong><br/>
    //                {pr.Reason}
    //            </p>";


    //    await _emailService.SendEmailAsync(recipientsTo, recipientsCC, subject, body);
    //}

    // Helper method to determine next approval level
    private int GetNextApprovalLevel(WorkflowStatus currentStatus)
    {
        return currentStatus switch
        {
            WorkflowStatus.Submitted => 1, // Manager
            WorkflowStatus.ManagerApproval => 2, // Director
            WorkflowStatus.DirectorApproval => 3, // Finance
            _ => 1
        };
    }

    //  method to determine next workflow status after approval
    private WorkflowStatus GetNextWorkflowStatus(WorkflowStatus currentStatus)
    {
        return currentStatus switch
        {
            WorkflowStatus.Submitted => WorkflowStatus.ManagerApproval,
            WorkflowStatus.ManagerApproval => WorkflowStatus.DirectorApproval,
            WorkflowStatus.DirectorApproval => WorkflowStatus.FinanceApproval,
            WorkflowStatus.FinanceApproval => WorkflowStatus.Approved,
            _ => WorkflowStatus.Approved
        };
    }

    //  method to get next approver email based on status
    private string GetNextApproverEmailForStatus(WorkflowStatus status, PurchaseRequisition pr)
    {
        return status switch
        {
            WorkflowStatus.ManagerApproval => GetDepartmentHODEmail(pr.Department),
            WorkflowStatus.DirectorApproval => "director@company.com",
            WorkflowStatus.FinanceApproval => "finance.head@company.com",
            _ => "eddy.wang@kgi.com" // Default fallback
        };
    }

    //  method to get department HOD email
    private string GetDepartmentHODEmail(string department)
    {
        return department?.ToLower() switch
        {
            "information technology" => "it.hod@company.com",
            "human resources" => "hr.hod@company.com",
            "finance" => "finance.hod@company.com",
            "operations" => "ops.hod@company.com",
            "marketing" => "marketing.hod@company.com",
            "sales" => "elahvarasi.raju@kgi.com",
            _ => "eddy.wang@kgi.com" // Default as mentioned in transcript
        };
    }


}
