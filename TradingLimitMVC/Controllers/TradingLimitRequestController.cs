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

                // Get available approvers and endorsers from GroupSetting table
                var approvers = await GetApproversByGroupAndType();
                
                // Get suggested approval workflow based on request details
                var suggestedWorkflow = await GetSuggestedApprovalWorkflow(request);

                // Create enhanced workflow object
                var enhancedWorkflow = new EnhancedApprovalWorkflow
                {
                    RequestId = request.Id,
                    Steps = suggestedWorkflow,
                    Status = request.Status ?? "Draft"
                };

                ViewBag.AvailableApprovers = approvers;
                ViewBag.SuggestedWorkflow = suggestedWorkflow;
                ViewBag.EnhancedWorkflow = enhancedWorkflow;
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
                var approvers = new List<ApprovalStepRequest>();
                
                foreach (var step in model.ApprovalSteps.Where(s => !string.IsNullOrWhiteSpace(s.Email)))
                {
                    var approverRequest = new ApprovalStepRequest
                    {
                        StepNumber = step.StepNumber > 0 ? step.StepNumber : approvers.Count + 1,
                        Email = step.Email,
                        Name = step.Name,
                        Role = step.Role,
                        ApprovalGroupId = step.ApprovalGroupId, // Use from ApprovalSteps view model
                        ApprovalGroupName = step.ApprovalGroupName, // Use from ApprovalSteps view model
                        IsRequired = step.IsRequired,
                        DueDate = step.DueDate,
                        MinimumAmountThreshold = step.MinimumAmountThreshold,
                        MaximumAmountThreshold = step.MaximumAmountThreshold,
                        RequiredDepartment = step.RequiredDepartment,
                        ApprovalConditions = step.ApprovalConditions
                    };
                    
                    // Fallback: If group information is not provided from frontend, query database
                    if (!approverRequest.ApprovalGroupId.HasValue)
                    {
                        var groupSetting = await _context.GroupSettings
                            .Where(gs => gs.Email.ToLower() == step.Email.ToLower() && 
                                        (gs.TypeID == 1 || gs.TypeID == 2)) // Approver or Endorser
                            .FirstOrDefaultAsync();
                        
                        approverRequest.ApprovalGroupId = groupSetting?.GroupID;
                        approverRequest.ApprovalGroupName = groupSetting?.GroupName;
                    }
                    
                    approvers.Add(approverRequest);
                }

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

        // Helper method to create ApprovalStepRequest with group information
        private async Task<ApprovalStepRequest> CreateApprovalStepRequestAsync(string email, string? name, string? role, int stepNumber)
        {
            // Get group information from GroupSetting for this approver
            var groupSetting = await _context.GroupSettings
                .Where(gs => gs.Email.ToLower() == email.ToLower() && 
                            (gs.TypeID == 1 || gs.TypeID == 2)) // Approver or Endorser
                .FirstOrDefaultAsync();

            return new ApprovalStepRequest
            {
                StepNumber = stepNumber,
                Email = email,
                Name = name ?? groupSetting?.Username,
                Role = role ?? groupSetting?.TypeName,
                ApprovalGroupId = groupSetting?.GroupID,
                ApprovalGroupName = groupSetting?.GroupName,
                IsRequired = true,
                DueDate = DateTime.Now.AddDays(stepNumber * 2)
            };
        }

        // Helper method to create ApprovalStepRequest list from ApproverInfo list with group information
        private async Task<List<ApprovalStepRequest>> CreateApprovalStepRequestListAsync(List<ApproverInfo> approvers, int baseStepNumber = 1)
        {
            var stepRequests = new List<ApprovalStepRequest>();

            for (int i = 0; i < approvers.Count; i++)
            {
                var approver = approvers[i];
                var stepRequest = await CreateApprovalStepRequestAsync(
                    approver.Email, 
                    approver.Name, 
                    approver.Role, 
                    baseStepNumber + i
                );
                
                // Use the group information from ApproverInfo if available (which we just populated)
                if (approver.GroupId.HasValue)
                {
                    stepRequest.ApprovalGroupId = approver.GroupId;
                    stepRequest.ApprovalGroupName = approver.GroupName;
                }

                stepRequests.Add(stepRequest);
            }

            return stepRequests;
        }

        // Helper method to get approvers by group and type
        private async Task<List<ApproverInfo>> GetApproversByGroupAndType(int? groupId = null, int? typeId = null)
        {
            var query = _context.GroupSettings.AsQueryable();

            // Filter by group if specified
            if (groupId.HasValue)
            {
                query = query.Where(gs => gs.GroupID == groupId.Value);
            }

            // Filter by type if specified (default to approvers and endorsers)
            if (typeId.HasValue)
            {
                query = query.Where(gs => gs.TypeID == typeId.Value);
            }
            else
            {
                // Default: get approvers (1) and endorsers (2), exclude observers (3)
                query = query.Where(gs => gs.TypeID == 1 || gs.TypeID == 2);
            }

            var approvers = await query
                .OrderBy(gs => gs.GroupID)
                .ThenBy(gs => gs.TypeID)
                .ThenBy(gs => gs.Username)
                .Select(gs => new ApproverInfo
                {
                    Id = gs.Id,
                    Name = gs.Username,
                    Email = gs.Email,
                    Role = gs.TypeName,
                    Department = gs.GroupName,
                    PhoneNumber = string.Empty,
                    GroupId = gs.GroupID,
                    GroupName = gs.GroupName
                })
                .ToListAsync();

            return approvers;
        }

        // Helper method to get suggested approval workflow based on request details
        private async Task<List<ApprovalStepGroup>> GetSuggestedApprovalWorkflow(TradingLimitRequest request)
        {
            var approvalSteps = new List<ApprovalStepGroup>();

            try
            {
                // Determine the appropriate groups based on request type or amount
                var targetGroups = new List<int>();

                // Business logic to determine which groups should be involved
                if (request.GLProposedLimit > 10000000) // Large amounts need Risk approval
                {
                    targetGroups.AddRange(new[] { 1, 2, 3 }); // IWM, GSPS, Risk
                }
                else if (request.GLProposedLimit > 1000000) // Medium amounts need GSPS
                {
                    targetGroups.AddRange(new[] { 1, 2 }); // IWM, GSPS
                }
                else // Small amounts need only IWM
                {
                    targetGroups.Add(1); // IWM only
                }

                // Create sequential steps for each group, with parallel approvers within each group
                int stepNumber = 1;
                foreach (var groupId in targetGroups)
                {
                    var groupApprovers = await GetApproversByGroupAndType(groupId, 1); // Get approvers only
                    if (groupApprovers.Any())
                    {
                        var stepGroup = new ApprovalStepGroup
                        {
                            StepNumber = stepNumber,
                            GroupId = groupId,
                            GroupName = groupApprovers.First().GroupName ?? groupApprovers.First().Department,
                            ApprovalType = "ParallelAnyOne", // Any one approver in the group can approve
                            RequiredApprovals = 1, // Only one approval needed from this group
                            Approvers = groupApprovers,
                            DueDate = DateTime.Now.AddDays(stepNumber * 2), // 2 days per step
                            IsRequired = true
                        };
                        approvalSteps.Add(stepGroup);
                        stepNumber++;
                    }
                }

                // If no specific approvers found, create a default step with all approvers
                if (!approvalSteps.Any())
                {
                    var allApprovers = await GetApproversByGroupAndType(null, 1);
                    if (allApprovers.Any())
                    {
                        var defaultStep = new ApprovalStepGroup
                        {
                            StepNumber = 1,
                            GroupId = 0,
                            GroupName = "All Groups",
                            ApprovalType = "ParallelAnyOne",
                            RequiredApprovals = 1,
                            Approvers = allApprovers,
                            DueDate = DateTime.Now.AddDays(3),
                            IsRequired = true
                        };
                        approvalSteps.Add(defaultStep);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating suggested approval workflow for request {RequestId}", request.Id);
                // Fallback to default step
                var fallbackApprovers = await GetApproversByGroupAndType(null, 1);
                if (fallbackApprovers.Any())
                {
                    approvalSteps.Add(new ApprovalStepGroup
                    {
                        StepNumber = 1,
                        GroupId = 0,
                        GroupName = "Default",
                        ApprovalType = "ParallelAnyOne",
                        RequiredApprovals = 1,
                        Approvers = fallbackApprovers,
                        DueDate = DateTime.Now.AddDays(3),
                        IsRequired = true
                    });
                }
            }

            return approvalSteps;
        }

        // API endpoint to get approvers by group (for AJAX calls)
        [HttpGet("GetApprovers")]
        public async Task<IActionResult> GetApprovers(int? groupId = null, int? typeId = null)
        {
            try
            {
                var approvers = await GetApproversByGroupAndType(groupId, typeId);
                return Json(approvers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving approvers for group {GroupId} and type {TypeId}", groupId, typeId);
                return Json(new List<ApproverInfo>());
            }
        }

        // API endpoint to get suggested approval workflow for a request
        [HttpGet("GetSuggestedWorkflow/{id}")]
        public async Task<IActionResult> GetSuggestedWorkflow(int id)
        {
            try
            {
                var request = await _context.TradingLimitRequests.FindAsync(id);
                if (request == null)
                {
                    return NotFound();
                }

                var suggestedWorkflow = await GetSuggestedApprovalWorkflow(request);
                var enhancedWorkflow = new EnhancedApprovalWorkflow
                {
                    RequestId = request.Id,
                    Steps = suggestedWorkflow,
                    Status = request.Status ?? "Draft"
                };

                return Json(enhancedWorkflow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suggested workflow for request {Id}", id);
                return Json(new EnhancedApprovalWorkflow());
            }
        }

        // Helper method to get observers for notifications
        private async Task<List<ApproverInfo>> GetObserversByGroup(int? groupId = null)
        {
            var query = _context.GroupSettings
                .Where(gs => gs.TypeID == 3); // TypeID = 3 for Observers

            if (groupId.HasValue)
            {
                query = query.Where(gs => gs.GroupID == groupId.Value);
            }

            var observers = await query
                .OrderBy(gs => gs.GroupID)
                .ThenBy(gs => gs.Username)
                .Select(gs => new ApproverInfo
                {
                    Id = gs.Id,
                    Name = gs.Username,
                    Email = gs.Email,
                    Role = gs.TypeName,
                    Department = gs.GroupName,
                    PhoneNumber = string.Empty,
                    GroupId = gs.GroupID,
                    GroupName = gs.GroupName
                })
                .ToListAsync();

            return observers;
        }

        // API endpoint to get observers by group
        [HttpGet("GetObservers")]
        public async Task<IActionResult> GetObservers(int? groupId = null)
        {
            try
            {
                var observers = await GetObserversByGroup(groupId);
                return Json(observers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving observers for group {GroupId}", groupId);
                return Json(new List<ApproverInfo>());
            }
        }

        // Helper method to create different workflow templates
        private async Task<List<ApprovalStepGroup>> CreateWorkflowTemplate(string templateType, TradingLimitRequest request)
        {
            var steps = new List<ApprovalStepGroup>();

            switch (templateType.ToLower())
            {
                case "simple":
                    // Single step with any IWM approver
                    var iwmApprovers = await GetApproversByGroupAndType(1, 1); // IWM Approvers
                    if (iwmApprovers.Any())
                    {
                        steps.Add(new ApprovalStepGroup
                        {
                            StepNumber = 1,
                            GroupId = 1,
                            GroupName = "IWM",
                            ApprovalType = "ParallelAnyOne",
                            RequiredApprovals = 1,
                            Approvers = iwmApprovers,
                            DueDate = DateTime.Now.AddDays(2),
                            IsRequired = true
                        });
                    }
                    break;

                case "standard":
                    // Sequential: IWM -> GSPS
                    var iwmStep = await CreateGroupStep(1, 1, "ParallelAnyOne", 1, 2);
                    var gspsStep = await CreateGroupStep(2, 2, "ParallelAnyOne", 1, 4);
                    steps.AddRange(new[] { iwmStep, gspsStep }.Where(s => s != null).Cast<ApprovalStepGroup>());
                    break;

                case "complex":
                    // Sequential: IWM -> GSPS -> Risk, with multiple approvals required
                    var complexIwm = await CreateGroupStep(1, 1, "ParallelAnyOne", 1, 2);
                    var complexGsps = await CreateGroupStep(2, 2, "ParallelAnyOne", 1, 4);
                    var complexRisk = await CreateGroupStep(3, 3, "ParallelAnyOne", 1, 6);
                    steps.AddRange(new[] { complexIwm, complexGsps, complexRisk }.Where(s => s != null).Cast<ApprovalStepGroup>());
                    break;

                case "parallel":
                    // All groups in parallel (same step number)
                    var parallelGroups = new[] { 1, 2, 3 };
                    foreach (var groupId in parallelGroups)
                    {
                        var groupStep = await CreateGroupStep(groupId, 1, "ParallelAnyOne", 1, 3);
                        if (groupStep != null)
                        {
                            groupStep.StepNumber = 1; // All in step 1 for parallel execution
                            steps.Add(groupStep);
                        }
                    }
                    break;

                case "amount-based":
                default:
                    // Default amount-based logic
                    return await GetSuggestedApprovalWorkflow(request);
            }

            return steps;
        }

        // Helper method to create a step for a specific group
        private async Task<ApprovalStepGroup?> CreateGroupStep(int groupId, int stepNumber, string approvalType, int requiredApprovals, int dueDays)
        {
            var approvers = await GetApproversByGroupAndType(groupId, 1);
            if (!approvers.Any()) return null;

            return new ApprovalStepGroup
            {
                StepNumber = stepNumber,
                GroupId = groupId,
                GroupName = approvers.First().GroupName ?? approvers.First().Department,
                ApprovalType = approvalType,
                RequiredApprovals = requiredApprovals,
                Approvers = approvers,
                DueDate = DateTime.Now.AddDays(dueDays),
                IsRequired = true
            };
        }

        // API endpoint to get workflow templates
        [HttpGet("GetWorkflowTemplate")]
        public async Task<IActionResult> GetWorkflowTemplate(string templateType, int requestId)
        {
            try
            {
                var request = await _context.TradingLimitRequests.FindAsync(requestId);
                if (request == null)
                {
                    return NotFound();
                }

                var workflowSteps = await CreateWorkflowTemplate(templateType, request);
                var workflow = new EnhancedApprovalWorkflow
                {
                    RequestId = requestId,
                    Steps = workflowSteps,
                    Status = "Pending"
                };

                return Json(workflow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating workflow template {TemplateType} for request {RequestId}", templateType, requestId);
                return Json(new EnhancedApprovalWorkflow());
            }
        }

        // Helper method to convert ApprovalStepGroup to ApprovalStepRequest list with group information
        private async Task<List<ApprovalStepRequest>> ConvertStepGroupToRequestListAsync(ApprovalStepGroup stepGroup)
        {
            var stepRequests = new List<ApprovalStepRequest>();

            foreach (var approver in stepGroup.Approvers)
            {
                var stepRequest = new ApprovalStepRequest
                {
                    StepNumber = stepGroup.StepNumber,
                    Email = approver.Email,
                    Name = approver.Name,
                    Role = approver.Role,
                    ApprovalGroupId = approver.GroupId ?? stepGroup.GroupId,
                    ApprovalGroupName = approver.GroupName ?? stepGroup.GroupName,
                    IsRequired = stepGroup.IsRequired,
                    DueDate = stepGroup.DueDate
                };

                stepRequests.Add(stepRequest);
            }

            return stepRequests;
        }

        // Helper method to convert list of ApprovalStepGroup to flat list of ApprovalStepRequest
        private async Task<List<ApprovalStepRequest>> ConvertWorkflowToRequestListAsync(List<ApprovalStepGroup> workflow)
        {
            var allStepRequests = new List<ApprovalStepRequest>();

            foreach (var stepGroup in workflow)
            {
                var stepRequests = await ConvertStepGroupToRequestListAsync(stepGroup);
                allStepRequests.AddRange(stepRequests);
            }

            return allStepRequests;
        }

        // API endpoint to get all approvers grouped by group
        [HttpGet("GetApproversByGroups")]
        public async Task<IActionResult> GetApproversByGroups()
        {
            try
            {
                var groupedApprovers = new Dictionary<string, List<ApproverInfo>>();

                // Get approvers for each group
                var groups = new[] { 
                    new { Id = 1, Name = "IWM" }, 
                    new { Id = 2, Name = "GSPS" }, 
                    new { Id = 3, Name = "Risk" } 
                };

                foreach (var group in groups)
                {
                    var approvers = await GetApproversByGroupAndType(group.Id, 1);
                    groupedApprovers[group.Name] = approvers;
                }

                return Json(groupedApprovers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving grouped approvers");
                return Json(new Dictionary<string, List<ApproverInfo>>());
            }
        }
    }
}