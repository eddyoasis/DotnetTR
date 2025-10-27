using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TradingLimitMVC.Services
{
    public interface IApprovalWorkflowService
    {


        Task<List<ApprovalWorkflowStep>> GenerateWorkflowStepsAsync(PurchaseRequisition pr);
        Task<bool> ProcessApprovalStepAsync(int prId, string approverEmail, ApprovalStatus status, string comments);
        Task<bool> HandleRejectionAsync(int prId, string rejectedBy, string reason);
        Task<List<string>> GetNextApproversAsync(PurchaseRequisition pr);
        Task<bool> ValidateDistributionAsync(PurchaseRequisition pr);

        Task NotifyNextApproverAsync(PurchaseRequisition pr);
    }
    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ApprovalWorkflowService> _logger;
        private readonly IPowerAutomateService _powerAutomateService;
        private readonly IExchangeRateService _exchangeRateService;
        private readonly IWebHostEnvironment _environment;
        private readonly IPDFService _pdfService;
        private readonly IConfigurationHelperService _configHelper;
        public ApprovalWorkflowService(
            ApplicationDbContext context,
            ILogger<ApprovalWorkflowService> logger,
            IPowerAutomateService powerAutomateService,
            IExchangeRateService exchangeRateService,
            IWebHostEnvironment environment,
            IPDFService pdfService,
            IConfigurationHelperService configHelper)
        {
            _context = context;
            _logger = logger;
            _powerAutomateService = powerAutomateService;
            _exchangeRateService = exchangeRateService;
            _environment = environment;
            _pdfService = pdfService;
            _configHelper = configHelper;
        }


        public async Task<List<ApprovalWorkflowStep>> GenerateWorkflowStepsAsync(PurchaseRequisition pr)
        {
            var steps = new List<ApprovalWorkflowStep>();
            int stepOrder = 1;
            _logger.LogInformation($" Generating approval workflow for PR: {pr.PRReference}");
            // Validate Currency
            if (string.IsNullOrEmpty(pr.QuotationCurrency))
            {
                _logger.LogError($" ERROR: PR {pr.PRReference} has no currency set!");
                throw new InvalidOperationException("Cannot generate workflow - Currency is not set");
            }
            _logger.LogInformation($" Amount: {pr.QuotationCurrency} {pr.TotalAmount:N2}");
            _logger.LogInformation($" IT Related: {pr.IsITRelated}");
            _logger.LogInformation($" Signed PAF: {pr.SignedPDF}");
            _logger.LogInformation($" Fixed Asset: {pr.IsFixedAsset}");
            // ====================================================================
            // VALIDATE COST CENTERS
            // ====================================================================
            if (pr.CostCenters == null || !pr.CostCenters.Any())
            {
                throw new InvalidOperationException("Cannot generate workflow - No cost centers defined");
            }
            // ====================================================================
            // EXTRACT UNIQUE COST CENTER APPROVERS
            // ====================================================================
            var uniqueApprovers = new HashSet<string>();
            var costCenterApprovers = new List<(string ApproverName, string ApproverEmail, string CostCenterName, string ApproverRole)>();
            _logger.LogInformation($" Processing {pr.CostCenters.Count} cost centers:");
            foreach (var cc in pr.CostCenters)
            {
                _logger.LogInformation($"   - Cost Center: {cc.Name}, Approver: {cc.Approver}, Email: {cc.ApproverEmail}");
                if (!string.IsNullOrEmpty(cc.Approver) && !string.IsNullOrEmpty(cc.ApproverEmail))
                {
                    var approverInfo = _configHelper.GetCostCenterApproverInfo(cc.Name);
                    var uniqueKey = $"{cc.ApproverEmail}|{cc.Name}";
                    if (uniqueApprovers.Add(uniqueKey))
                    {
                        costCenterApprovers.Add((
                            approverInfo.Name,
                            //cc.Approver,
                            cc.ApproverEmail,
                            cc.Name,
                            cc.ApproverRole ?? "HOD"
                        ));
                        _logger.LogInformation($"    Added approver: {cc.Approver} ({cc.Name})");
                    }
                    else
                    {
                        _logger.LogInformation($"    Skipped duplicate approver: {cc.Approver} ({cc.Name})");
                    }
                }
            }
            _logger.LogInformation($" Found {costCenterApprovers.Count} unique cost center approvers");
            if (!costCenterApprovers.Any())
            {
                throw new InvalidOperationException("No valid approvers found in cost centers");
            }
            // ====================================================================
            // STEP 1: COST CENTER APPROVERS (PARALLEL APPROVAL)
            // ====================================================================
            _logger.LogInformation($" Step {stepOrder}: Adding {costCenterApprovers.Count} Cost Center Approvers");
            foreach (var approver in costCenterApprovers)
            {
                steps.Add(new ApprovalWorkflowStep
                {
                    StepOrder = stepOrder,
                    ApproverRole = approver.ApproverRole,
                    ApproverName = approver.ApproverName,
                    ApproverEmail = approver.ApproverEmail,
                    Department = approver.CostCenterName,
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    IsParallel = true
                });
                _logger.LogInformation($"     {approver.ApproverRole} - {approver.ApproverName} ({approver.CostCenterName})");
            }
            stepOrder++;
            // ====================================================================
            // STEP 2: IT HOD (IF IT RELATED)
            // ====================================================================
            if (pr.IsITRelated)
            {
                steps.Add(new ApprovalWorkflowStep
                {
                    StepOrder = stepOrder++,
                    ApproverRole = "IT HOD",
                    ApproverName = _configHelper.GetApproverName("ITHOD"),
                    ApproverEmail = _configHelper.GetApproverEmail("ITHOD"), //  From config
                    Department = "IT Department",
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    IsParallel = false
                });
            }
            // ====================================================================
            // STEP 3: CS HOD (ALWAYS REQUIRED)
            // ====================================================================
            steps.Add(new ApprovalWorkflowStep
            {
                StepOrder = stepOrder++,
                ApproverRole = "CS HOD",
                ApproverName = _configHelper.GetApproverName("CSHOD"), //  From config
                ApproverEmail = _configHelper.GetApproverEmail("CSHOD"), //  From config
                Department = "CS Department",
                Status = ApprovalStatus.Pending,
                IsRequired = true,
                IsParallel = false
            });
            // ====================================================================
            // CHECK SIGNED PAF
            // ====================================================================
            if (pr.SignedPDF)
            {
                _logger.LogInformation($" Signed PAF detected - Workflow ends after CS HOD");
                _logger.LogInformation($" Total Steps: {steps.Count}");
                return steps;
            }
            // ====================================================================
            //  GET THRESHOLDS FROM SERVICE (which reads from DB or appsettings.json)
            // ====================================================================
            var cfoThresholdSGD = await _exchangeRateService.GetFixedAssetCFOThresholdAsync(); // 10,000 SGD
            var ceoThresholdSGD = await _exchangeRateService.GetCEOThresholdAsync(); // 43,000 SGD (not used for Fixed Asset unchecked)
            var sgdValue = pr.ContractValueSGD;
            _logger.LogInformation($" CFO Threshold: SGD {cfoThresholdSGD:N2}");
            _logger.LogInformation($" CEO Threshold: SGD {ceoThresholdSGD:N2}");
            _logger.LogInformation($" PR Amount: {pr.QuotationCurrency} {pr.TotalAmount:N2}");
            _logger.LogInformation($" SGD Value: SGD {sgdValue:N2}");
            // ====================================================================
            // CHECK IF FIXED ASSET IS UNCHECKED
            // ====================================================================
            _logger.LogInformation($" Fixed Asset Status: {pr.IsFixedAsset}");
            if (!pr.IsFixedAsset)
            {
                _logger.LogInformation($" Fixed Asset is UNCHECKED - CFO approval REQUIRED (FINAL)");
                steps.Add(new ApprovalWorkflowStep
                {
                    StepOrder = stepOrder++,
                    ApproverRole = "CFO (Final Approval)",
                    ApproverName = _configHelper.GetApproverName("CFO"), //  From config
                    ApproverEmail = _configHelper.GetApproverEmail("CFO"), //  From config
                    Department = "Finance",
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    IsParallel = false
                });
                _logger.LogInformation($"     Step {stepOrder - 1}: CFO (FINAL - Fixed Asset unchecked)");
                _logger.LogInformation($" Workflow ends at CFO (Fixed Asset unchecked)");
                _logger.LogInformation($" Total Steps: {steps.Count}");
                return steps;
            }
            else
            {
                _logger.LogInformation($" Fixed Asset is CHECKED - threshold-based routing applies");
            }
            // ====================================================================
            // FIXED ASSET IS CHECKED - CHECK THRESHOLDS
            // ====================================================================
            if (sgdValue < cfoThresholdSGD)
            {
                _logger.LogInformation($"?? Fixed Asset < SGD {cfoThresholdSGD:N0}");
                _logger.LogInformation($" Adding {costCenterApprovers.Count} Cost Center approver(s) as FINAL approver(s)");
                var finalStepOrder = stepOrder;
                foreach (var approver in costCenterApprovers)
                {
                    steps.Add(new ApprovalWorkflowStep
                    {
                        StepOrder = finalStepOrder,
                        ApproverRole = $"{approver.ApproverRole} (Final Approval)",
                        ApproverName = approver.ApproverName,
                        ApproverEmail = approver.ApproverEmail,
                        Department = approver.CostCenterName,
                        Status = ApprovalStatus.Pending,
                        IsRequired = true,
                        IsParallel = true
                    });
                    _logger.LogInformation($"     Final Approver: {approver.ApproverName} from {approver.CostCenterName}");
                }
                stepOrder++;
            }
            else
            {
                _logger.LogInformation($"?? Fixed Asset >= SGD {cfoThresholdSGD:N0} - CFO approval REQUIRED");
                steps.Add(new ApprovalWorkflowStep
                {
                    StepOrder = stepOrder++,
                    ApproverRole = "CFO",
                    ApproverName = _configHelper.GetApproverName("CFO"), //  From config
                    ApproverEmail = _configHelper.GetApproverEmail("CFO"), //  From config
                    Department = "Finance",
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    IsParallel = false
                });
                _logger.LogInformation($"     Step {stepOrder - 1}: CFO");
                //  Check CEO threshold (only for Fixed Asset = checked)
                if (sgdValue > ceoThresholdSGD)
                {
                    _logger.LogInformation($"?? Amount > SGD {ceoThresholdSGD:N0} - CEO approval REQUIRED");
                    steps.Add(new ApprovalWorkflowStep
                    {
                        StepOrder = stepOrder++,
                        ApproverRole = "CEO",
                        ApproverName = _configHelper.GetApproverName("CEO"), //  From config
                        ApproverEmail = _configHelper.GetApproverEmail("CEO"), //  From config
                        Department = "Executive",
                        Status = ApprovalStatus.Pending,
                        IsRequired = true,
                        IsParallel = false
                    });
                    _logger.LogInformation($"       Step {stepOrder - 1}: CEO");
                }
                else
                {
                    _logger.LogInformation($"?? Amount <= SGD {ceoThresholdSGD:N0} - CEO approval NOT required");
                }
            }
            _logger.LogInformation($" Total approval steps generated: {steps.Count}");
            _logger.LogInformation($" Complete workflow summary:");
            foreach (var step in steps.OrderBy(s => s.StepOrder))
            {
                _logger.LogInformation($"   Step {step.StepOrder}: {step.ApproverRole} - {step.ApproverName} ({step.Department})");
            }
            return steps;
        }

        // Process approval with proper rejection handling
        public async Task<bool> ProcessApprovalStepAsync(int prId, string approverEmail, ApprovalStatus status, string comments)
        {
            try
            {
                _logger.LogInformation($" Processing approval for PR ID: {prId}");
                _logger.LogInformation($" Approver: {approverEmail}");
                _logger.LogInformation($" Status: {status}");

                var pr = await _context.PurchaseRequisitions
                    .Include(p => p.WorkflowSteps)
                    .Include(p => p.Items)
                    .Include(p => p.CostCenters)
                    .FirstOrDefaultAsync(p => p.Id == prId);

                if (pr == null)
                {
                    _logger.LogError($" PR {prId} not found");
                    return false;
                }

                //  Validate currency before processing
                if (string.IsNullOrEmpty(pr.QuotationCurrency))
                {
                    _logger.LogError($" PR {pr.PRReference} has no currency set!");
                    return false;
                }

                _logger.LogInformation($" PR: {pr.PRReference}");
                _logger.LogInformation($" Amount: {pr.QuotationCurrency} {pr.TotalAmount:N2}");

                var currentStep = pr.WorkflowSteps
                    .FirstOrDefault(s => s.ApproverEmail == approverEmail && s.Status == ApprovalStatus.Pending);

                if (currentStep == null)
                {
                    _logger.LogWarning($"? No pending step for {approverEmail}");
                    return false;
                }

                _logger.LogInformation($" Current Step: {currentStep.StepOrder} - {currentStep.ApproverRole}");

                currentStep.Status = status;
                currentStep.ActionDate = DateTime.Now;
                currentStep.Comments = comments;

                var approval = new PurchaseRequisitionApproval
                {
                    PurchaseRequisitionId = prId,
                    ApproverName = currentStep.ApproverName,
                    //ApproverEmail = currentStep.ApproverEmail,
                    ApproverEmail = BaseService.Username,
                    ApproverRole = currentStep.ApproverRole,
                    Status = status,
                    Comments = comments,
                    ApprovalDate = DateTime.Now,
                    ApprovalLevel = currentStep.StepOrder,
                    Department = currentStep.Department,
                    ApprovalMethod = ApprovalMethod.Web
                };
                _context.PurchaseRequisitionApprovals.Add(approval);

                if (status == ApprovalStatus.Rejected)
                {
                    _logger.LogWarning($" PR {pr.PRReference} REJECTED by {currentStep.ApproverName}");

                    pr.CurrentStatus = WorkflowStatus.Rejected;
                    pr.RejectedBy = currentStep.ApproverName;
                    pr.RejectionReason = comments;
                    pr.RejectedDate = DateTime.Now;

                    foreach (var step in pr.WorkflowSteps.Where(s => s.Status == ApprovalStatus.Pending))
                    {
                        step.Status = ApprovalStatus.Skipped;
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($" Rejection processed successfully");
                    return true;
                }

                //  Check if all parallel approvals at current level are complete
                var currentLevelSteps = pr.WorkflowSteps
                    .Where(s => s.StepOrder == currentStep.StepOrder)
                    .ToList();

                var allCurrentLevelApproved = currentLevelSteps
                    .All(s => s.Status == ApprovalStatus.Approved);

                if (!allCurrentLevelApproved)
                {
                    _logger.LogInformation($" Waiting for other approvers at level {currentStep.StepOrder}");
                    await _context.SaveChangesAsync();
                    return true;  // Don't advance yet
                }

                // All parallel approvals done, advance to next step
                pr.CurrentApprovalStep++;
                _logger.LogInformation($" Advanced to step {pr.CurrentApprovalStep} of {pr.TotalApprovalSteps}");
                var nextStep = pr.WorkflowSteps
                    .Where(s => s.Status == ApprovalStatus.Pending)
                    .OrderBy(s => s.StepOrder)
                    .FirstOrDefault();

                if (nextStep != null)
                {
                    _logger.LogInformation($" Next approver: {nextStep.ApproverRole} - {nextStep.ApproverName}");
                    await _context.SaveChangesAsync();
                    await NotifyNextApproverAsync(pr);
                    return true;
                }

                // ====================================================================
                // ALL APPROVALS COMPLETE - CHECK PO GENERATION
                // ====================================================================
                _logger.LogInformation($" All approvals complete for PR {pr.PRReference}");
                pr.CurrentStatus = WorkflowStatus.Approved;
                pr.FinalApprovalDate = DateTime.Now;
                //  FIX: Set FinalApprover correctly - Find ALL approvers from the HIGHEST approved step
                var approvedSteps = pr.WorkflowSteps
                   .Where(s => s.Status == ApprovalStatus.Approved)
                   .ToList();
                if (approvedSteps.Any())
                {
                    // Get the highest step order that has approvals
                    var maxStepOrder = approvedSteps.Max(s => s.StepOrder);
                    // Get ALL approvers from that highest step
                    var finalApprovers = approvedSteps
                        .Where(s => s.StepOrder == maxStepOrder)
                        .OrderBy(s => s.ApproverName)
                        .Select(s => s.ApproverName)
                        .ToList();
                    if (finalApprovers.Count > 1)
                    {
                        // Multiple final approvers (parallel approval)
                        pr.FinalApprover = string.Join(", ", finalApprovers);
                        _logger.LogInformation($" Final Approvers (Parallel - Step {maxStepOrder}): {pr.FinalApprover}");
                    }
                    else
                    {
                        // Single final approver
                        pr.FinalApprover = finalApprovers.First();
                        _logger.LogInformation($" Final Approver (Step {maxStepOrder}): {pr.FinalApprover}");
                    }
                }
                else
                {
                    _logger.LogWarning($" No approved steps found for final approver assignment");
                    pr.FinalApprover = currentStep.ApproverName;
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation($" PR {pr.PRReference} marked as FULLY APPROVED");

                // ====================================================================
                // AUTO-GENERATE PO (UNLESS "No PO Required" IS CHECKED)
                // ====================================================================
                if (pr.NoPORequired)
                {
                    _logger.LogInformation($" 'No PO Required' is CHECKED - Skipping PO generation");
                    pr.POReference = "N/A - No PO Required";
                    pr.POGenerated = true;
                    pr.POGeneratedDate = DateTime.Now;
                }
                else
                {
                    _logger.LogInformation($" Auto-generating Purchase Order...");
                    await AutoGeneratePurchaseOrderAsync(pr);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($" Approval processing complete");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error processing approval step");
                return false;
            }
        }
        public async Task NotifyNextApproverAsync(PurchaseRequisition pr)
        {
            var nextStep = pr.WorkflowSteps
                .Where(s => s.Status == ApprovalStatus.Pending)
                .OrderBy(s => s.StepOrder)
                .FirstOrDefault();

            if (nextStep != null)
            {
                try
                {
                    await _powerAutomateService.TriggerApprovalWorkflowAsync(
                        nextStep.ApproverEmail,
                        pr.PRReference,
                        pr);

                    _logger.LogInformation($" Notification sent to {nextStep.ApproverName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to notify {nextStep.ApproverEmail}");
                }
            }
        }
        private async Task AutoGeneratePurchaseOrderAsync(PurchaseRequisition pr)
        {
            try
            {
                _logger.LogInformation($" Starting PO auto-generation for PR {pr.PRReference}");

                //  Validate PR has items
                if (pr.Items == null || !pr.Items.Any())
                {
                    _logger.LogError($" Cannot generate PO - No items in PR {pr.PRReference}");
                    pr.POReference = "ERROR: No items to generate PO";
                    pr.POGenerated = false;
                    return;
                }

                _logger.LogInformation($" PR has {pr.Items.Count} items");

                //  Validate currency
                if (string.IsNullOrEmpty(pr.QuotationCurrency))
                {
                    _logger.LogError($" Cannot generate PO - No currency in PR {pr.PRReference}");
                    pr.POReference = "ERROR: No currency set";
                    pr.POGenerated = false;
                    return;
                }

                _logger.LogInformation($" Currency: {pr.QuotationCurrency}");
                _logger.LogInformation($" Amount: {pr.QuotationCurrency} {pr.TotalAmount:N2}");

                var poCount = await _context.PurchaseOrders.CountAsync();
                var poReference = $"PO-{DateTime.Now.Year}-{(poCount + 1):D3}";
                var defaultVendor = pr.Items.FirstOrDefault()?.SuggestedSupplier ?? "TBD";

                _logger.LogInformation($" Creating PO: {poReference}");
                _logger.LogInformation($" Vendor: {defaultVendor}");

                var po = new PurchaseOrder
                {
                    POReference = poReference,
                    PONo = $"{(poCount + 1):D4}",
                    PurchaseRequisitionId = pr.Id,
                    PRReference = pr.PRReference,
                    Company = pr.Company ?? "Default Company",
                    DeliveryAddress = pr.DeliveryAddress ?? "",
                    Attention = pr.ContactPerson,
                    PhoneNo = pr.ContactPhoneNo,
                    IssueDate = DateTime.Now,
                    DeliveryDate = pr.ExpectedDeliveryDate ?? DateTime.Now.AddDays(30),
                    //POOriginator = pr.SubmittedBy ?? "System",
                    POOriginator = BaseService.Username,
                    Vendor = defaultVendor,
                    VendorAddress = pr.VendorFullAddress ?? "",
                    PaymentTerms = "Net 30",
                    POStatus = $"Auto-Generated from Approved PR ({pr.QuotationCurrency})",
                    CurrentStatus = POWorkflowStatus.Issued,
                    CreatedDate = DateTime.Now,
                    SubmittedBy = BaseService.Username,
                    SubmittedDate = DateTime.Now,
                    Items = pr.Items.Select(item => new PurchaseOrderItem
                    {
                        Description = item.Description ?? "Item",
                        Details = $"{item.Action} - {item.Description}",
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountAmount = item.DiscountAmount,
                        Amount = item.Amount,
                        GST = item.GST ?? "7%",
                        PRNo = pr.PRReference
                    }).ToList()
                };

                _context.PurchaseOrders.Add(po);

                pr.POGenerated = true;
                pr.POGeneratedDate = DateTime.Now;
                pr.POReference = poReference;

                _logger.LogInformation($" PO {poReference} created successfully!");
                _logger.LogInformation($" PO contains {po.Items.Count} items");
                _logger.LogInformation($" PO Total: {pr.QuotationCurrency} {po.TotalAmount:N2}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $" PO auto-generation FAILED for PR {pr.PRReference}");
                _logger.LogError($"Error Details: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                pr.POGenerated = false;
                pr.POReference = $"ERROR: {ex.Message}";
            }
        }


        // Notify submitter when PR is rejected
        private async Task NotifySubmitterOfRejection(PurchaseRequisition pr, string rejectedBy, string reason)
        {
            try
            {
                _logger.LogInformation(
                    $"PR {pr.PRReference} rejected by {rejectedBy}. Reason: {reason}. Notifying submitter: {pr.SubmittedBy}");

                var submitterEmail = GetSubmitterEmail(pr.SubmittedBy);

                var notificationPayload = new
                {
                    recipientEmail = submitterEmail,
                    recipientName = pr.SubmittedBy,
                    prReference = pr.PRReference,
                    rejectedBy = rejectedBy,
                    rejectionReason = reason,
                    rejectionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    prTotal = pr.TotalAmount,
                    editLink = $"https://localhost:7218/PurchaseRequisition/Edit/{pr.Id}",
                    notificationType = "PRRejection"
                };

                // For now, just log it (you'll add Power Automate flow later)
                _logger.LogWarning($"[REJECTION NOTIFICATION] To: {submitterEmail}, PR: {pr.PRReference}, Rejected by: {rejectedBy}");

                // TODO: Implement Power Automate flow for rejection notifications
                // await _powerAutomateService.TriggerRejectionNotificationAsync(notificationPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify submitter of rejection");
            }
        }
       
       
        private string GetSubmitterEmail(string submitterName)
        {
            // Map submitter names to emails
            return submitterName switch
            {
                "UserA" => "elahvarasi.raju@kgi.com",
                "Current User" => "elahvarasi.raju@kgi.com",
                _ => "elahvarasi.raju@kgi.com"
            };
        }

        public async Task<List<string>> GetNextApproversAsync(PurchaseRequisition pr)
        {
            var nextStep = pr.CurrentApprovalStep + 1;
            return pr.WorkflowSteps
                .Where(s => s.StepOrder == nextStep && s.Status == ApprovalStatus.Pending)
                .Select(s => s.ApproverEmail)
                .ToList();
        }

        public async Task<bool> ValidateDistributionAsync(PurchaseRequisition pr)
        {
            if (pr.CostCenters?.Any() != true) return false;

            if (pr.DistributionType == DistributionType.Percentage)
            {
                var totalPercentage = pr.CostCenters.Sum(cc => cc.Percentage ?? 0);
                return Math.Abs(totalPercentage - 100) < 0.01m;
            }
            else
            {
                var totalAmount = pr.CostCenters.Sum(cc => cc.Amount ?? 0);
                return totalAmount > 0 && Math.Abs(totalAmount - pr.TotalAmount) < 0.01m;
            }
        }

        public async Task<bool> HandleRejectionAsync(int prId, string rejectedBy, string reason)
        {
            try
            {
                var pr = await _context.PurchaseRequisitions
                    .Include(p => p.WorkflowSteps)
                    .FirstOrDefaultAsync(p => p.Id == prId);

                if (pr == null) return false;

                pr.CurrentStatus = WorkflowStatus.Rejected;
                pr.RejectedBy = rejectedBy;
                pr.RejectionReason = reason;
                pr.RejectedDate = DateTime.Now;

                // Mark all pending steps as skipped
                foreach (var step in pr.WorkflowSteps.Where(s => s.Status == ApprovalStatus.Pending))
                {
                    step.Status = ApprovalStatus.Skipped;
                }

                await NotifySubmitterOfRejection(pr, rejectedBy, reason);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling rejection for PR {prId}");
                return false;
            }
        }

        

        
    }
}
