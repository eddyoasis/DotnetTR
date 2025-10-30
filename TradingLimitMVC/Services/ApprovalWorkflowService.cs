using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;

namespace TradingLimitMVC.Services
{
    public interface IApprovalWorkflowService
    {
        Task<ApprovalWorkflow> CreateWorkflowAsync(int requestId, List<ApprovalStepRequest> approvers, string workflowType = "Sequential");
        Task<IEnumerable<TradingLimitRequest>> GetPendingApprovalsForUserAsync(string userEmail);
        Task<bool> ProcessApprovalStepAsync(int stepId, string userEmail, string action, string? comments = null);
        Task<ApprovalWorkflow?> GetWorkflowByRequestIdAsync(int requestId);
        Task<ApprovalStep?> GetApprovalStepAsync(int stepId);
        Task<bool> CanUserApproveStepAsync(int stepId, string userEmail);
        Task<bool> AdvanceWorkflowAsync(int workflowId);
        Task<List<ApprovalStep>> GetActiveStepsForUserAsync(string userEmail);
    }

    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ApprovalWorkflowService> _logger;

        public ApprovalWorkflowService(ApplicationDbContext context, ILogger<ApprovalWorkflowService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ApprovalWorkflow> CreateWorkflowAsync(int requestId, List<ApprovalStepRequest> approvers, string workflowType = "Sequential")
        {
            try
            {
                var workflow = new ApprovalWorkflow
                {
                    TradingLimitRequestId = requestId,
                    WorkflowType = workflowType,
                    Status = "Pending",
                    CurrentStep = 1,
                    RequiredApprovals = workflowType == "Parallel" ? approvers.Count : 1,
                    CreatedDate = DateTime.UtcNow
                };

                _context.ApprovalWorkflows.Add(workflow);
                await _context.SaveChangesAsync();

                // Create approval steps
                for (int i = 0; i < approvers.Count; i++)
                {
                    var approver = approvers[i];

                    // Get group information from GroupSetting
                    var groupSetting = await _context.GroupSettings
                        .FirstOrDefaultAsync(gs => gs.Email.ToLower() == approver.Email.ToLower() &&
                            (gs.TypeID == 1 || gs.TypeID == 2)); // Approver or Endorser
                    
                    var step = new ApprovalStep
                    {
                        ApprovalWorkflowId = workflow.Id,
                        StepNumber = approver.StepNumber,
                        ApproverEmail = approver.Email,
                        ApproverName = approver.Name,
                        ApproverRole = approver.Role,
                        ApprovalGroupId = approver.ApprovalGroupId ?? groupSetting?.GroupID,
                        ApprovalGroupName = approver.ApprovalGroupName ?? groupSetting?.GroupName,
                        Status = workflowType == "Sequential" && i > 0 ? "Pending" : "InProgress",
                        IsRequired = approver.IsRequired,
                        DueDate = approver.DueDate,
                        MinimumAmountThreshold = approver.MinimumAmountThreshold,
                        MaximumAmountThreshold = approver.MaximumAmountThreshold,
                        RequiredDepartment = approver.RequiredDepartment,
                        ApprovalConditions = approver.ApprovalConditions,
                        AssignedDate = DateTime.UtcNow
                    };

                    _context.ApprovalSteps.Add(step);
                }

                await _context.SaveChangesAsync();

                // Reload workflow with steps
                return await _context.ApprovalWorkflows
                    .Include(w => w.ApprovalSteps)
                    .Include(w => w.TradingLimitRequest)
                    .FirstAsync(w => w.Id == workflow.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating approval workflow for request {RequestId}", requestId);
                throw;
            }
        }

        public async Task<IEnumerable<TradingLimitRequest>> GetPendingApprovalsForUserAsync(string userEmail)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    _logger.LogWarning("GetPendingApprovalsForUserAsync called with null or empty userEmail");
                    return new List<TradingLimitRequest>();
                }

                _logger.LogInformation("Getting pending approvals for user: {UserEmail} (normalized: {NormalizedEmail})", 
                    userEmail, userEmail.ToLower());
                
                // Get requests where user has active approval steps (multi-approval workflow)
                var requests = await _context.TradingLimitRequests
                    .Include(r => r.ApprovalWorkflow)
                        .ThenInclude(w => w!.ApprovalSteps)
                    .Include(r => r.Attachments)
                    .Where(r => r.ApprovalWorkflow != null &&
                               // Only show requests that are not already completed
                               (r.Status == "Submitted" || r.Status == "Pending" || r.Status == "InProgress") &&
                               r.ApprovalWorkflow.Status != "Approved" && 
                               r.ApprovalWorkflow.Status != "Rejected" &&
                               r.ApprovalWorkflow.ApprovalSteps.Any(s => 
                                   s.ApproverEmail != null && userEmail != null &&
                                   s.ApproverEmail.ToLower() == userEmail.ToLower() && 
                                   (s.Status == "Pending" || s.Status == "InProgress")))
                    .OrderByDescending(r => r.SubmittedDate)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} multi-approval requests before IsActive filter", requests.Count);

                // Filter by IsActive property after loading (since EF can't translate the computed property)
                var filteredRequests = requests.Where(r =>
                    r.ApprovalWorkflow!.ApprovalSteps.Any(s =>
                        s.ApproverEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase) && s.IsActive)).ToList();

                _logger.LogInformation("Found {Count} multi-approval requests after IsActive filter", filteredRequests.Count);

                // Also include legacy single-approver requests
                var legacyRequests = await _context.TradingLimitRequests
                    .Include(r => r.Attachments)
                    .Where(r => r.ApprovalWorkflow == null &&
                               r.ApprovalEmail != null && userEmail != null &&
                               r.ApprovalEmail.ToLower() == userEmail.ToLower() &&
                               (r.Status == "Submitted" || r.Status == "Pending Approval"))
                    .OrderByDescending(r => r.SubmittedDate)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} legacy single-approval requests", legacyRequests.Count);

                var totalRequests = filteredRequests.Concat(legacyRequests).ToList();
                _logger.LogInformation("Total pending requests for user {UserEmail}: {Count}", userEmail, totalRequests.Count);

                return totalRequests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approvals for user {UserEmail}", userEmail);
                throw;
            }
        }

        public async Task<bool> ProcessApprovalStepAsync(int stepId, string userEmail, string action, string? comments = null)
        {
            try
            {
                var step = await _context.ApprovalSteps
                    .Include(s => s.ApprovalWorkflow)
                        .ThenInclude(w => w.TradingLimitRequest)
                    .Include(s => s.ApprovalWorkflow.ApprovalSteps)
                    .FirstOrDefaultAsync(s => s.Id == stepId);

                if (step == null || !step.ApproverEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Update step status
                step.Status = action;
                step.Comments = comments;
                step.ActionDate = DateTime.UtcNow;

                // Update workflow based on action
                var workflow = step.ApprovalWorkflow;
                
                if (action == "Approved")
                {
                    workflow.ReceivedApprovals++;
                    
                    if (workflow.WorkflowType == "Sequential")
                    {
                        // Move to next step or complete workflow
                        await AdvanceSequentialWorkflowAsync(workflow);
                    }
                    else if (workflow.WorkflowType == "Parallel")
                    {
                        // Check if all required approvals are received
                        await CheckParallelWorkflowCompletionAsync(workflow);
                    }
                }
                else if (action == "Rejected")
                {
                    // Rejection stops the workflow
                    workflow.Status = "Rejected";
                    workflow.CompletedDate = DateTime.UtcNow;
                    workflow.TradingLimitRequest.Status = "Rejected";
                }
                else if (action == "Revision Required")
                {
                    // Revision required stops the workflow and returns to requester
                    workflow.Status = "Revision Required";
                    workflow.CompletedDate = DateTime.UtcNow;
                    workflow.TradingLimitRequest.Status = "Revision Required";
                }

                workflow.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Approval step {StepId} processed by {UserEmail} with action {Action}", 
                    stepId, userEmail, action);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing approval step {StepId}", stepId);
                throw;
            }
        }

        public async Task<ApprovalWorkflow?> GetWorkflowByRequestIdAsync(int requestId)
        {
            return await _context.ApprovalWorkflows
                .Include(w => w.ApprovalSteps)
                .Include(w => w.TradingLimitRequest)
                .FirstOrDefaultAsync(w => w.TradingLimitRequestId == requestId);
        }

        public async Task<ApprovalStep?> GetApprovalStepAsync(int stepId)
        {
            return await _context.ApprovalSteps
                .Include(s => s.ApprovalWorkflow)
                    .ThenInclude(w => w.TradingLimitRequest)
                .FirstOrDefaultAsync(s => s.Id == stepId);
        }

        public async Task<bool> CanUserApproveStepAsync(int stepId, string userEmail)
        {
            var step = await _context.ApprovalSteps
                .Include(s => s.ApprovalWorkflow)
                    .ThenInclude(w => w.TradingLimitRequest)
                .FirstOrDefaultAsync(s => s.Id == stepId);

            if (step == null) return false;

            // Check if user is assigned to this step
            if (!step.ApproverEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase)) return false;

            // Check if step is active
            if (step.Status != "Pending" && step.Status != "InProgress") return false;

            // Check if user didn't create the request (prevent self-approval)
            if (step.ApprovalWorkflow.TradingLimitRequest.CreatedBy?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true)
                return false;

            // For sequential workflows, check if it's the current step
            if (step.ApprovalWorkflow.WorkflowType == "Sequential")
            {
                return step.StepNumber == step.ApprovalWorkflow.CurrentStep;
            }

            // For parallel workflows, step is always available if active
            return true;
        }

        public async Task<bool> AdvanceWorkflowAsync(int workflowId)
        {
            var workflow = await _context.ApprovalWorkflows
                .Include(w => w.ApprovalSteps)
                .Include(w => w.TradingLimitRequest)
                .FirstOrDefaultAsync(w => w.Id == workflowId);

            if (workflow == null) return false;

            if (workflow.WorkflowType == "Sequential")
            {
                return await AdvanceSequentialWorkflowAsync(workflow);
            }
            
            return false;
        }

        public async Task<List<ApprovalStep>> GetActiveStepsForUserAsync(string userEmail)
        {
            return await _context.ApprovalSteps
                .Include(s => s.ApprovalWorkflow)
                    .ThenInclude(w => w.TradingLimitRequest)
                .Where(s => s.ApproverEmail != null && userEmail != null &&
                           s.ApproverEmail.ToLower() == userEmail.ToLower() && 
                           (s.Status == "Pending" || s.Status == "InProgress"))
                .OrderBy(s => s.AssignedDate)
                .ToListAsync();
        }

        private async Task<bool> AdvanceSequentialWorkflowAsync(ApprovalWorkflow workflow)
        {
            var nextStep = workflow.ApprovalSteps
                .Where(s => s.StepNumber > workflow.CurrentStep)
                .OrderBy(s => s.StepNumber)
                .FirstOrDefault();

            if (nextStep != null)
            {
                // Activate next step
                workflow.CurrentStep = nextStep.StepNumber;
                nextStep.Status = "InProgress";
                workflow.Status = "InProgress";
            }
            else
            {
                // Workflow completed
                workflow.Status = "Approved";
                workflow.CompletedDate = DateTime.UtcNow;
                workflow.TradingLimitRequest.Status = "Approved";
                workflow.TradingLimitRequest.ApprovedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> CheckParallelWorkflowCompletionAsync(ApprovalWorkflow workflow)
        {
            var requiredApprovals = workflow.ApprovalSteps.Count(s => s.IsRequired);
            var receivedApprovals = workflow.ApprovalSteps.Count(s => s.Status == "Approved" && s.IsRequired);

            if (receivedApprovals >= requiredApprovals)
            {
                // All required approvals received
                workflow.Status = "Approved";
                workflow.CompletedDate = DateTime.UtcNow;
                workflow.TradingLimitRequest.Status = "Approved";
                workflow.TradingLimitRequest.ApprovedDate = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
    }

    // Helper class for creating approval steps
    public class ApprovalStepRequest
    {
        public int StepNumber { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Role { get; set; }
        public int? ApprovalGroupId { get; set; }
        public string? ApprovalGroupName { get; set; }
        public bool IsRequired { get; set; } = true;
        public DateTime? DueDate { get; set; }
        public decimal? MinimumAmountThreshold { get; set; }
        public decimal? MaximumAmountThreshold { get; set; }
        public string? RequiredDepartment { get; set; }
        public string? ApprovalConditions { get; set; }
    }
}