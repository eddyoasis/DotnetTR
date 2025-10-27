using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;

namespace TradingLimitMVC.Services
{
    public interface IPOApprovalWorkflowService
    {
        Task<List<POApprovalWorkflowStep>> GeneratePOWorkflowStepsAsync(PurchaseOrder po);
        Task<bool> ProcessPOApprovalStepAsync(int poId, string approverEmail, ApprovalStatus status, string comments);
    }
    public class POApprovalWorkflowService : IPOApprovalWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<POApprovalWorkflowService> _logger;
        private readonly IPowerAutomateService _powerAutomateService;
        public POApprovalWorkflowService(
            ApplicationDbContext context,
            ILogger<POApprovalWorkflowService> logger,
            IPowerAutomateService powerAutomateService)
        {
            _context = context;
            _logger = logger;
            _powerAutomateService = powerAutomateService;
        }
        public async Task<List<POApprovalWorkflowStep>> GeneratePOWorkflowStepsAsync(PurchaseOrder po)
        {
            var steps = new List<POApprovalWorkflowStep>();
            var stepOrder = 1;
            var totalAmount = po.TotalAmount;
            _logger.LogInformation($"Generating PO workflow for {po.POReference}, Amount: ${totalAmount:F2}");
            //Step 1: Finance Officer(for all POs > $1, 000)
            if (totalAmount > 1000)
            {
                steps.Add(new POApprovalWorkflowStep
                {
                    StepOrder = stepOrder++,
                    ApproverRole = "Finance Officer",
                    ApproverName = "Sock Leng",
                    ApproverEmail = "elahvarasi.raju@kgi.com",
                    Department = "Finance",
                    IsRequired = true,
                    Status = ApprovalStatus.Pending,
                    PurchaseOrderId = po.Id
                });
            }
            //Step 2: Finance HOD(for POs > $5, 000)
            if (totalAmount > 5000)
            {
                steps.Add(new POApprovalWorkflowStep
                {
                    StepOrder = stepOrder++,
                    ApproverRole = "Finance HOD",
                    ApproverName = "Julie",
                    ApproverEmail = "elahvarasi.raju@kgi.com",
                    Department = "Finance",
                    IsRequired = true,
                    Status = ApprovalStatus.Pending,
                    PurchaseOrderId = po.Id
                });
            }
            //Step 3: Director(for POs > $10, 000)
            if (totalAmount > 10000)
            {
                steps.Add(new POApprovalWorkflowStep
                {
                    StepOrder = stepOrder++,
                    ApproverRole = "Director",
                    ApproverName = "Scott",
                    ApproverEmail = "elahvarasi.raju@kgi.com",
                    Department = "Executive",
                    IsRequired = true,
                    Status = ApprovalStatus.Pending,
                    PurchaseOrderId = po.Id
                });
            }
            return steps;
        }
        public async Task<bool> ProcessPOApprovalStepAsync(int poId, string approverEmail, ApprovalStatus status, string comments)
        {
            try
            {
                var po = await _context.PurchaseOrders
                    .Include(p => p.WorkflowSteps)
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.Id == poId);
                if (po == null) return false;
                var currentStep = po.WorkflowSteps
                    .FirstOrDefault(s => s.ApproverEmail == approverEmail && s.Status == ApprovalStatus.Pending);
                if (currentStep == null) return false;
                // Update step
                currentStep.Status = status;
                currentStep.ActionDate = DateTime.Now;
                currentStep.Comments = comments;
                // Add approval record
                var approval = new PurchaseOrderApproval
                {
                    PurchaseOrderId = poId,
                    ApproverName = currentStep.ApproverName,
                    ApproverEmail = currentStep.ApproverEmail,
                    Status = status,
                    Comments = comments,
                    ApprovalDate = DateTime.Now,
                    ApprovalLevel = currentStep.StepOrder,
                    ApprovalMethod = ApprovalMethod.Teams
                };
                _context.PurchaseOrderApprovals.Add(approval);
                // Handle rejection
                if (status == ApprovalStatus.Rejected)
                {
                    po.CurrentStatus = POWorkflowStatus.Rejected;
                    po.WorkflowSteps.Where(s => s.Status == ApprovalStatus.Pending)
                        .ToList().ForEach(s => s.Status = ApprovalStatus.Skipped);
                    await _context.SaveChangesAsync();
                    return true;
                }
                // Move to next step
                po.CurrentApprovalStep++;
                var nextSteps = po.WorkflowSteps
                    .Where(s => s.StepOrder == po.CurrentApprovalStep && s.Status == ApprovalStatus.Pending)
                    .ToList();
                if (nextSteps.Any())
                {
                    foreach (var nextStep in nextSteps)
                    {
                        await _powerAutomateService.TriggerPOApprovalWorkflowAsync(
                            nextStep.ApproverEmail, po.POReference, po);
                    }
                }
                else
                {
                    // Fully approved
                    po.CurrentStatus = POWorkflowStatus.Approved;
                    po.FinalApprovalDate = DateTime.Now;
                    po.FinalApprover = currentStep.ApproverName;
                }
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing PO approval for {poId}");
                return false;
            }
        }
    }
}
