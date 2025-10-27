using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Models.AppSettings;
using TradingLimitMVC.Models.ViewModels;
using TradingLimitMVC.Services;
using System.Collections.Generic;
using System.Security.Claims;

namespace TradingLimitMVC.Controllers
{
    [Route("PurchaseRequisition")]
    public class PurchaseRequisitionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPowerAutomateService _powerAutomateService;
        private readonly IPDFService _pdfService;
        private readonly IPRPDFService _prPdfService;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly ILogger<PurchaseRequisitionController> _logger;
        private readonly IExchangeRateService _exchangeRateService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfigurationHelperService _configHelper;
        private readonly IOptionsSnapshot<ApprovalThresholdsAppSetting> _approvalThresholdsAppSetting;
        private readonly IOptionsSnapshot<DepartmentRolesAppSetting> _departmentRolesAppSetting;
        private readonly IOptionsSnapshot<GeneralAppSetting> _generalAppSetting;
        private readonly IEmailService _emailService;

        public PurchaseRequisitionController(
            ApplicationDbContext context,
            IOptionsSnapshot<ApprovalThresholdsAppSetting> approvalThresholdsAppSetting,
            IOptionsSnapshot<DepartmentRolesAppSetting> departmentRolesAppSetting,
            IOptionsSnapshot<GeneralAppSetting> generalAppSetting,
            IConfiguration configuration,
            IPowerAutomateService powerAutomateService,
            IEmailService emailService,
            IPDFService pdfService,
            IPRPDFService prPdfService,
            IApprovalWorkflowService workflowService,
            IExchangeRateService exchangeRateService,
            ILogger<PurchaseRequisitionController> logger,
            IWebHostEnvironment environment, IConfigurationHelperService configHelper)
        {
            _context = context;
            _approvalThresholdsAppSetting = approvalThresholdsAppSetting;
            _departmentRolesAppSetting = departmentRolesAppSetting;
            _generalAppSetting = generalAppSetting;
            _configuration = configuration;
            _emailService = emailService;
            _powerAutomateService = powerAutomateService;
            _pdfService = pdfService;
            _prPdfService = prPdfService;
            _workflowService = workflowService;
            _exchangeRateService = exchangeRateService;
            _logger = logger;
            _environment = environment;
            _configHelper = configHelper;
        }

        //// GET: PurchaseRequisition
        //public async Task<IActionResult> Index()
        //{
        //    var purchaseRequisitions = await _context.PurchaseRequisitions
        //        .Include(pr => pr.Items)
        //        .Include(pr => pr.Approvals)
        //        .Include(pr => pr.WorkflowSteps)
        //        .Include(pr => pr.CostCenters)
        //        .OrderByDescending(pr => pr.CreatedDate)
        //        .ToListAsync();
        //    return View(purchaseRequisitions);
        //}

        // GET: PurchaseRequisition/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var purchaseRequisition = await _context.PurchaseRequisitions
                .Include(pr => pr.Items)
                .Include(pr => pr.Attachments)
                .Include(pr => pr.Approvals.OrderBy(a => a.ApprovalLevel))
                .Include(pr => pr.CostCenters)
                .Include(pr => pr.WorkflowSteps.OrderBy(ws => ws.StepOrder))
                .FirstOrDefaultAsync(m => m.Id == id);
            if (purchaseRequisition == null) return NotFound();
            //  FIX: Find the associated PO ID if PR is approved and PO was generated
            if (purchaseRequisition.CurrentStatus == WorkflowStatus.Approved &&
                purchaseRequisition.POGenerated &&
                !string.IsNullOrEmpty(purchaseRequisition.PRReference))
            {
                var associatedPO = await _context.PurchaseOrders
                    .Where(po => po.PRReference == purchaseRequisition.PRReference ||
                                po.PurchaseRequisitionId == purchaseRequisition.Id)
                    .Select(po => new { po.Id, po.POReference, po.POStatus })
                    .FirstOrDefaultAsync();
                if (associatedPO != null)
                {
                    ViewBag.POId = associatedPO.Id;
                    ViewBag.POReference = associatedPO.POReference;
                    ViewBag.POStatus = associatedPO.POStatus;
                    _logger.LogInformation($"Found PO {associatedPO.POReference} (ID: {associatedPO.Id}) for PR {purchaseRequisition.PRReference}");
                }
                else
                {
                    _logger.LogWarning($"PR {purchaseRequisition.PRReference} marked as POGenerated but no PO found in database");
                }
            }
            return View(purchaseRequisition);
        }

        // ===================================================================
        // STEP 2: ADD API ENDPOINT TO GET VENDOR DETAILS
        // ===================================================================

        [HttpGet]
        [Route("api/vendor/{vendorId}")]
        public async Task<IActionResult> GetVendorDetails(int vendorId)
        {
            try
            {
                var vendor = await _context.SAPDropdownItems
                .FirstOrDefaultAsync(s => s.ID == vendorId && s.TypeName == "Vendor");

                if (vendor == null)
                {
                    return NotFound(new { success = false, message = "Vendor not found" });
                }
                // Parse vendor details from DDName or additional fields
                // Assuming format: "VendorName|Address|Phone|Email"
                var details = ParseVendorDetails(vendor);
                return Ok(new
                {
                    success = true,
                    vendorId = vendor.ID,
                    vendorCode = vendor.DDID,
                    vendorName = vendor.DDName,
                    vendorAddress = details.VendorAddress,
                    contactPerson = details.ContactPerson,
                    phoneNumber = details.PhoneNumber,
                    email = details.Email,
                    faxNumber = details.FaxNumber,
                    //paymentTerms = details.PaymentTerms
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vendor details");
                return StatusCode(500, new { success = false, message = ex.Message });
            }

        }
        private SAPDropdownItem ParseVendorDetails(SAPDropdownItem vendor)
        {

            return new SAPDropdownItem
            {
                VendorAddress = vendor.VendorAddress ?? $"{vendor.DDName} Address",
                ContactPerson = vendor.ContactPerson ?? "Contact Person",
                PhoneNumber = vendor.PhoneNumber ?? "Phone Number",
                Email = vendor.Email ?? "email@vendor.com",
                FaxNumber = vendor.FaxNumber ?? "Fax Number",
                //PaymentTerms = vendor.PaymentTerms ?? "Net 3
            };
        }

        // GET: PurchaseRequisition/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            SetupViewBag();
            var model = new PurchaseRequisition
            {
                ExpectedDeliveryDate = DateTime.Now.AddDays(30),
                CreatedDate = DateTime.Now,
                DistributionCurrency = "USD"
            };
            return View(model);
        }


        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseRequisition purchaseRequisition, IFormCollection formData)
        {
            try
            {
                _logger.LogInformation("=================== CREATE PR DEBUG ===================");
                _logger.LogInformation($" GetCurrentUser(): {GetCurrentUser()}");
                _logger.LogInformation($" BaseService.Username: {BaseService.Username}");
                _logger.LogInformation($" BaseService.Email: {BaseService.Email}");
                _logger.LogInformation($" BaseService.Department: {BaseService.Department}");
                _logger.LogInformation($" BaseService.JobTitle: {BaseService.JobTitle}");
                _logger.LogInformation($" User.Identity.Name: {User.Identity?.Name}");
                _logger.LogInformation($" User.Identity.IsAuthenticated: {User.Identity?.IsAuthenticated}");
                _logger.LogInformation(" All User Claims:");
                foreach (var claim in User.Claims)
                {
                    _logger.LogInformation($"     {claim.Type} = {claim.Value}");
                }
                _logger.LogInformation("====================================================");
                ModelState.Remove("Items");
                ModelState.Remove("CostCenters");
                ModelState.Remove("WorkflowSteps");
                //  VALIDATION 1: Currency Selection
                if (string.IsNullOrEmpty(purchaseRequisition.QuotationCurrency))
                {
                    TempData["Error"] = " Please select a Currency before submitting the PR";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }

                // VALIDATION: Phone Number Format
                if (!string.IsNullOrEmpty(purchaseRequisition.ContactPhoneNo))
                {
                    var phoneRegex = new System.Text.RegularExpressions.Regex(@"^[\d\s\+\-\(\)]+$");
                    if (!phoneRegex.IsMatch(purchaseRequisition.ContactPhoneNo))
                    {
                        TempData["Error"] = " Invalid phone number format. Only numbers and symbols (+, -, (, )) are allowed.";
                        SetupViewBag();
                        return View(purchaseRequisition);
                    }
                    // Additional validation: Check length
                    if (purchaseRequisition.ContactPhoneNo.Length > 20)
                    {
                        TempData["Error"] = " Phone number cannot exceed 20 characters.";
                        SetupViewBag();
                        return View(purchaseRequisition);
                    }
                }
                //  VALIDATION 2: Short Description
                if (string.IsNullOrWhiteSpace(purchaseRequisition.ShortDescription))
                {
                    TempData["Error"] = " Short Description is required";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }
                //  VALIDATION 3: Reason
                if (string.IsNullOrWhiteSpace(purchaseRequisition.Reason))
                {
                    TempData["Error"] = " Reason is required";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }
                
                // Generate references
                var count = await _context.PurchaseRequisitions.CountAsync();
                purchaseRequisition.PRReference = $"PR-{DateTime.Now.Year}-{(count + 1):D3}";
                purchaseRequisition.PRInternalNo = $"{(count + 1):D4}";
                purchaseRequisition.CreatedDate = DateTime.Now;
                purchaseRequisition.CurrentStatus = WorkflowStatus.Draft;
                purchaseRequisition.SubmittedBy = BaseService.Username;
                purchaseRequisition.SubmittedByEmail = BaseService.Email;
                purchaseRequisition.DistributionType = DistributionType.Amount;

                // Get exchange rate
                purchaseRequisition.ExchangeRateToSGD = await _exchangeRateService
                    .GetExchangeRateAsync(purchaseRequisition.QuotationCurrency);

                _logger.LogInformation($" PR Currency: {purchaseRequisition.QuotationCurrency}");
                _logger.LogInformation($" Exchange Rate: {purchaseRequisition.ExchangeRateToSGD:F4}");

                // Process Items FIRST
                ProcessItemsFromForm(purchaseRequisition, formData);
                _logger.LogInformation($" PR IsFixedAsset = {purchaseRequisition.IsFixedAsset}");
                _logger.LogInformation($" Items with IsFixedAsset checked:");
                foreach (var item in purchaseRequisition.Items.Where(i => i.IsFixedAsset))
                {
                    _logger.LogInformation($"   - {item.Description}");
                }

                //   NEW CALCULATION: Total WITH GST for threshold comparison
                decimal subtotal = purchaseRequisition.Items.Sum(i => i.Amount);
                decimal gstTotal = CalculateGSTTotal(purchaseRequisition);
                decimal totalWithGST = subtotal + gstTotal;

                //  : TotalAmount MUST be tax-inclusive
                purchaseRequisition.TotalAmount = totalWithGST;
                _logger.LogInformation($" Item Total (incl GST): {purchaseRequisition.QuotationCurrency} {totalWithGST:F2}");

                _logger.LogInformation($" After ProcessItemsFromForm: Items count = {purchaseRequisition.Items?.Count ?? 0}");
                _logger.LogInformation($" Subtotal (excl. GST) = {purchaseRequisition.QuotationCurrency} {subtotal:F2}");
                _logger.LogInformation($" GST Total = {purchaseRequisition.QuotationCurrency} {gstTotal:F2}");
                _logger.LogInformation($" Total Amount (incl. GST) = {purchaseRequisition.QuotationCurrency} {totalWithGST:F2}");
                _logger.LogInformation($" Contract Value (SGD) = SGD {purchaseRequisition.ContractValueSGD:F2}");

                if (purchaseRequisition.Items?.Any() != true)
                {
                    TempData["Error"] = " Cannot save PR without valid purchase items";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }

                //  VALIDATION 4: Check for Supplier and Payment Terms
                var itemsWithoutSupplier = purchaseRequisition.Items
                    .Where(i => string.IsNullOrWhiteSpace(i.SuggestedSupplier))
                    .ToList();
                if (itemsWithoutSupplier.Any())
                {
                    TempData["Error"] = $" {itemsWithoutSupplier.Count} item(s) missing supplier. Please select a supplier for all items.";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }
                var itemsWithoutPaymentTerms = purchaseRequisition.Items
                    .Where(i => string.IsNullOrWhiteSpace(i.PaymentTerms))
                    .ToList();
                if (itemsWithoutPaymentTerms.Any())
                {
                    TempData["Error"] = $"{itemsWithoutPaymentTerms.Count} item(s) missing payment terms. Please enter payment terms for all items.";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }

                var invalidItems = purchaseRequisition.Items
                    .Select((item, index) => new { item, index })
                    .Where(x =>
                        string.IsNullOrWhiteSpace(x.item.Description) ||
                        x.item.Quantity <= 0 ||
                        x.item.UnitPrice < 0)
                    .ToList();

                if (invalidItems.Any())
                {
                    var errorDetails = string.Join(", ", invalidItems.Select(x =>
                        $"Item {x.index + 1}: Desc={x.item.Description ?? "NULL"}, Qty={x.item.Quantity}, Price={x.item.UnitPrice}"));
                    _logger.LogError($" Invalid items found: {errorDetails}");
                    TempData["Error"] = $"Invalid items found: {errorDetails}";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }

                // Process Cost Centers
                await ProcessCostCentersFromForm(purchaseRequisition, formData);
                _logger.LogInformation($" DEBUG: Cost Centers after processing:");
                foreach (var cc in purchaseRequisition.CostCenters)
                {
                    _logger.LogInformation($"   - Name: {cc.Name}, Approver: {cc.Approver}, Email: {cc.ApproverEmail}, Amount: ${cc.Amount ?? 0:N2}");
                }

                _logger.LogInformation($" PR Total (incl. GST): {purchaseRequisition.QuotationCurrency} {purchaseRequisition.TotalAmount:F2} " +
                                      $"@ rate {purchaseRequisition.ExchangeRateToSGD:F4} = SGD {purchaseRequisition.ContractValueSGD:F2}");

                //  : Validate distribution against TAX-INCLUSIVE TOTAL
                var validationResult = ValidateDistribution(purchaseRequisition);
                if (!validationResult.IsValid)
                {
                    TempData["Error"] = $" Distribution Error: {validationResult.ErrorMessage}";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }

                purchaseRequisition.IsDistributionValid = true;
                purchaseRequisition.DistributionTotal = purchaseRequisition.TotalAmount;
                purchaseRequisition.IsFixedAsset = purchaseRequisition.Items.Any(i => i.IsFixedAsset);
                _context.Add(purchaseRequisition);
                await _context.SaveChangesAsync();
                TempData["Success"] = $" Purchase Requisition {purchaseRequisition.PRReference} created successfully! " +
                                     $"Total: {purchaseRequisition.QuotationCurrency} {purchaseRequisition.TotalAmount:N2} (incl. GST)";
                return RedirectToAction(nameof(Details), new { id = purchaseRequisition.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error creating PR");
                TempData["Error"] = "Error creating Purchase Requisition: " + ex.Message;
                SetupViewBag();
                return View(purchaseRequisition);
            }
        }
        //  METHOD: Calculate GST Total
        private decimal CalculateGSTTotal(PurchaseRequisition pr)
        {
            if (pr.Items == null || !pr.Items.Any())
                return 0;

            decimal gstTotal = 0;
            foreach (var item in pr.Items)
            {
                if (!string.IsNullOrEmpty(item.GST) && item.GST != "0%")
                {
                    var gstRate = decimal.TryParse(item.GST.Replace("%", ""), out var rate) ? rate : 0;
                    gstTotal += (item.Amount * gstRate / 100);
                }
            }

            return gstTotal;
        }
        private bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return true; // Allow empty if not required
                             // Check length
            if (phoneNumber.Length > 20)
                return false;
            // Check format: only digits, spaces, +, -, (, )
            var phoneRegex = new System.Text.RegularExpressions.Regex(@"^[\d\s\+\-\(\)]+$");
            return phoneRegex.IsMatch(phoneNumber);
        }


        [HttpGet("SubmitConfirmation/{id}")]
        public async Task<IActionResult> SubmitConfirmation(int id)
        {
            var pr = await _context.PurchaseRequisitions
                .Include(p => p.WorkflowSteps.OrderBy(ws => ws.StepOrder))
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pr == null)
            {
                TempData["Error"] = "PR not found";
                return RedirectToAction(nameof(Index));
            }

            return View(pr);
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var purchaseRequisition = await _context.PurchaseRequisitions
                    .Include(pr => pr.Items)
                    .Include(pr => pr.CostCenters)
                    .Include(pr => pr.Attachments)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (purchaseRequisition == null) return NotFound();

                // Allow edit only for Draft or Rejected status
                if (purchaseRequisition.CurrentStatus != WorkflowStatus.Draft &&
                    purchaseRequisition.CurrentStatus != WorkflowStatus.Rejected)
                {
                    TempData["Error"] = "Cannot edit PR - it's already in approval workflow or completed.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                SetupViewBag();
                return View(purchaseRequisition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading PR for edit: {Id}", id);
                TempData["Error"] = "Error loading Purchase Requisition.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: PurchaseRequisition/Edit/5
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PurchaseRequisition purchaseRequisition, IFormCollection formData)
        {
            if (id != purchaseRequisition.Id) return NotFound();

            try
            {
                var existingPR = await _context.PurchaseRequisitions
                    .Include(pr => pr.Items)
                    .Include(pr => pr.CostCenters)
                    .Include(pr => pr.WorkflowSteps)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (existingPR == null) return NotFound();

                // Check if allowed to edit
                if (existingPR.CurrentStatus != WorkflowStatus.Draft &&
                    existingPR.CurrentStatus != WorkflowStatus.Rejected)
                {
                    TempData["Error"] = "Cannot edit PR in current status.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Update basic fields
                existingPR.Company = purchaseRequisition.Company;
                existingPR.Department = purchaseRequisition.Department;
                existingPR.IsITRelated = purchaseRequisition.IsITRelated;
                existingPR.NoPORequired = purchaseRequisition.NoPORequired;
                existingPR.ExpectedDeliveryDate = purchaseRequisition.ExpectedDeliveryDate;
                existingPR.DeliveryAddress = purchaseRequisition.DeliveryAddress;
                existingPR.ContactPerson = purchaseRequisition.ContactPerson;
                existingPR.ContactPhoneNo = purchaseRequisition.ContactPhoneNo;
                existingPR.QuotationCurrency = purchaseRequisition.QuotationCurrency;
                existingPR.ShortDescription = purchaseRequisition.ShortDescription;
                existingPR.TypeOfPurchase = purchaseRequisition.TypeOfPurchase;
                existingPR.Reason = purchaseRequisition.Reason;
                existingPR.SignedPDF = purchaseRequisition.SignedPDF;
                existingPR.Remarks = purchaseRequisition.Remarks;

                // Clear old items and cost centers
                _context.PurchaseRequisitionItems.RemoveRange(existingPR.Items);
                _context.CostCenters.RemoveRange(existingPR.CostCenters);

                // Process new items
                ProcessItemsFromForm(existingPR, formData);

                // Process new cost centers
                await ProcessCostCentersFromForm(existingPR, formData);

                // Validate distribution
                var validationResult = ValidateDistribution(existingPR);
                if (!validationResult.IsValid)
                {
                    TempData["Error"] = $"Distribution Error: {validationResult.ErrorMessage}";
                    SetupViewBag();
                    return View(purchaseRequisition);
                }

                // If PR was rejected, reset it to Draft status
                if (existingPR.CurrentStatus == WorkflowStatus.Rejected)
                {
                    existingPR.CurrentStatus = WorkflowStatus.Draft;
                    existingPR.RejectionReason = null;
                    existingPR.RejectedBy = null;
                    existingPR.RejectedDate = null;
                    existingPR.CurrentApprovalStep = 0;

                    // Clear old workflow steps
                    _context.ApprovalWorkflowSteps.RemoveRange(existingPR.WorkflowSteps);

                    TempData["Success"] = "PR updated successfully. You can now resubmit for approval.";
                }
                else
                {
                    TempData["Success"] = "PR updated successfully.";
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PR {Id}", id);
                TempData["Error"] = "Error updating Purchase Requisition: " + ex.Message;
                SetupViewBag();
                return View(purchaseRequisition);
            }
        }
        private string FormatCurrency(decimal amount, string currency)
        {
            return $"{currency} {amount:N2}";
        }

        private void LogPRSummary(PurchaseRequisition pr)
        {
            _logger.LogInformation("=".PadRight(60, '='));
            _logger.LogInformation($" PR Summary: {pr.PRReference}");
            _logger.LogInformation($" Amount: {FormatCurrency(pr.TotalAmount, pr.QuotationCurrency ?? "USD")}");
            _logger.LogInformation($" Rate: {pr.ExchangeRateToSGD:F4} (to SGD)");
            _logger.LogInformation($" SGD Value: {FormatCurrency(pr.ContractValueSGD, "SGD")}");
            //_logger.LogInformation($" Department: {pr.Department}");
            _logger.LogInformation($" Items: {pr.Items?.Count ?? 0}");
            _logger.LogInformation($" Cost Centers: {pr.CostCenters?.Count ?? 0}");
            _logger.LogInformation("=".PadRight(60, '='));
        }
        // POST: Submit for Approval 
        [HttpPost]
        [Route("SubmitForApproval/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(int id)
        {
            try
            {
                var pr = await _context.PurchaseRequisitions
                .Include(p => p.Items)
                .Include(p => p.CostCenters)
                .Include(p => p.WorkflowSteps)
                .FirstOrDefaultAsync(p => p.Id == id);


                if (pr == null)
                {
                    TempData["Error"] = " Purchase Requisition not found.";
                    return RedirectToAction(nameof(Index));
                }

                //  : Validate Currency
                if (string.IsNullOrEmpty(pr.QuotationCurrency))
                {
                    TempData["Error"] = " Cannot submit - Currency not set. Please edit the PR and select a currency.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Validate items
                if (pr.Items?.Any() != true)
                {
                    TempData["Error"] = " Cannot submit - no purchase items found.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Validate cost centers
                if (pr.CostCenters?.Any() != true)
                {
                    TempData["Error"] = " Cannot submit - no cost centers defined.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Validate distribution
                var validation = ValidateDistribution(pr);
                if (!validation.IsValid)
                {
                    TempData["Error"] = $" Cannot submit - {validation.ErrorMessage}";
                    return RedirectToAction(nameof(Details), new { id });
                }

                _logger.LogInformation($" Submitting PR {pr.PRReference}");
                _logger.LogInformation($" Amount: {pr.QuotationCurrency} {pr.TotalAmount:N2}");
                _logger.LogInformation($" Exchange Rate: {pr.ExchangeRateToSGD:F4}");
                _logger.LogInformation($" Contract Value: SGD {pr.ContractValueSGD:N2}");

                // Generate workflow (will use currency-based thresholds)
                pr.WorkflowSteps = await _workflowService.GenerateWorkflowStepsAsync(pr);
                pr.TotalApprovalSteps = pr.WorkflowSteps.Count;
                pr.CurrentApprovalStep = 1;
                pr.CurrentStatus = WorkflowStatus.Submitted;
                pr.SubmittedDate = DateTime.Now;
                pr.SubmittedBy = BaseService.Username;
                pr.SubmittedByEmail = BaseService.Email;
                pr.IsDistributionValid = true;

                await _context.SaveChangesAsync();

                _logger.LogInformation($" PR {pr.PRReference} submitted with {pr.TotalApprovalSteps} approval steps");

                // Send to first approver
                var firstStep = pr.WorkflowSteps.FirstOrDefault(s => s.StepOrder == 1);
                if (firstStep != null)
                {
                    try
                    {
                        // Send Email
                        await SendEmail(pr, firstStep.ApproverEmail);

                        //await _powerAutomateService.TriggerApprovalWorkflowAsync(
                        //    firstStep.ApproverEmail,
                        //    pr.PRReference,
                        //    pr);

                        _logger.LogInformation($" Sent PR {pr.PRReference} to {firstStep.ApproverEmail} ({firstStep.ApproverRole})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $" Failed to send notification to {firstStep.ApproverEmail}");
                    }
                }

                return RedirectToAction(nameof(SubmitConfirmation), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error submitting PR {Id}", id);
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }


        }

        // Download PDF
        [HttpGet("DownloadPDF/{id}")]
        public async Task<IActionResult> DownloadPDF(int id)
        {
            try
            {
                var pr = await _context.PurchaseRequisitions
                    .Include(pr => pr.Items)
                    .Include(pr => pr.CostCenters)
                    .Include(pr => pr.WorkflowSteps.OrderBy(ws => ws.StepOrder))
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (pr == null)
                {
                    TempData["Error"] = "Purchase Requisition not found";
                    return RedirectToAction(nameof(Index));
                }

                var pdfBytes = await _prPdfService.GeneratePRPdfAsync(pr);
                var fileName = $"PR_{pr.PRReference}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for PR {Id}", id);
                TempData["Error"] = "Failed to generate PDF. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimulateAllApprovals(int id)
        {
            try
            {
                _logger.LogInformation($" AUTO-APPROVE START for PR ID: {id}");
                var pr = await _context.PurchaseRequisitions
                    .Include(p => p.WorkflowSteps)
                    .Include(p => p.Approvals)
                    .Include(p => p.Items)
                    .Include(p => p.CostCenters)
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (pr == null)
                {
                    TempData["Error"] = " Purchase Requisition not found";
                    return RedirectToAction(nameof(Details), new { id });
                }
                _logger.LogInformation($" PR {pr.PRReference} has {pr.Items?.Count ?? 0} items");
                var pendingSteps = pr.WorkflowSteps
                    .Where(s => s.Status == ApprovalStatus.Pending)
                    .OrderBy(s => s.StepOrder)
                    .ToList();
                if (!pendingSteps.Any())
                {
                    TempData["Warning"] = " No pending approvals found";
                    return RedirectToAction(nameof(Details), new { id });
                }
                _logger.LogInformation($"?? Auto-approving {pendingSteps.Count} pending steps");
                //  FIX: Approve EACH workflow step AND create individual approval records
                foreach (var step in pendingSteps)
                {
                    _logger.LogInformation($"    Step {step.StepOrder}: {step.ApproverRole} - {step.ApproverName}");
                    // Update workflow step
                    step.Status = ApprovalStatus.Approved;
                    step.ActionDate = DateTime.Now;
                    step.Comments = " Auto-approved via testing tool";
                    //  : Create INDIVIDUAL approval record for EACH approver
                    var approval = new PurchaseRequisitionApproval
                    {
                        PurchaseRequisitionId = pr.Id,
                        ApproverName = step.ApproverName,
                        ApproverEmail = step.ApproverEmail,
                        ApproverRole = step.ApproverRole,
                        Status = ApprovalStatus.Approved,
                        Comments = " Auto-approved via testing tool",
                        ApprovalDate = DateTime.Now,
                        ApprovalLevel = step.StepOrder,
                        Department = step.Department,
                        ApprovalMethod = ApprovalMethod.Web
                    };
                    _context.PurchaseRequisitionApprovals.Add(approval);
                    _logger.LogInformation($"      ? Created approval record for {step.ApproverName}");
                }
                // Mark PR as fully approved
                pr.CurrentStatus = WorkflowStatus.Approved;
                pr.CurrentApprovalStep = pr.TotalApprovalSteps;
                pr.FinalApprovalDate = DateTime.Now;
                //  FIX: Set FinalApprover correctly for parallel approvals
                var highestStepOrder = pr.WorkflowSteps.Max(s => s.StepOrder);
                var finalApprovers = pr.WorkflowSteps
                    .Where(s => s.StepOrder == highestStepOrder && s.Status == ApprovalStatus.Approved)
                    .OrderBy(s => s.ApproverName)
                    .Select(s => s.ApproverName)
                    .ToList();
                if (finalApprovers.Count > 1)
                {
                    pr.FinalApprover = string.Join(", ", finalApprovers);
                    _logger.LogInformation($" Final Approvers (Parallel): {pr.FinalApprover}");
                }
                else
                {
                    pr.FinalApprover = finalApprovers.FirstOrDefault() ?? pendingSteps.Last().ApproverName;
                    _logger.LogInformation($" Final Approver: {pr.FinalApprover}");
                }
                // Save approval status FIRST
                await _context.SaveChangesAsync();
                _logger.LogInformation($" PR {pr.PRReference} marked as FULLY APPROVED");
                // Check "No PO Required" flag
                if (pr.NoPORequired)
                {
                    _logger.LogInformation($" 'No PO Required' is checked - skipping PO generation");
                    TempData["Success"] = $" PR {pr.PRReference} fully approved (No PO required)";
                    return RedirectToAction(nameof(Details), new { id });
                }
                // Check if PR has items before attempting PO generation
                if (pr.Items == null || !pr.Items.Any())
                {
                    _logger.LogError($" PR {pr.PRReference} has NO items - cannot generate PO");
                    TempData["Error"] = $" PR approved but has no items. Cannot auto-generate PO.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                // AUTO-GENERATE PO
                try
                {
                    _logger.LogInformation($"?? Starting PO auto-generation...");
                    var poCount = await _context.PurchaseOrders.CountAsync();
                    var poReference = $"PO-{DateTime.Now.Year}-{(poCount + 1):D3}";
                    var defaultVendor = pr.Items.FirstOrDefault()?.SuggestedSupplier ?? "TBD";
                    var paymentTerms = pr.Items.FirstOrDefault()?.PaymentTerms;
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
                        POOriginator = pr.SubmittedBy ?? "System",
                        Vendor = defaultVendor,
                        VendorAddress = pr.VendorFullAddress ?? "",
                        PaymentTerms = paymentTerms,
                        POStatus = "Auto-Generated from Approved PR",
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
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($" PO {poReference} (ID: {po.Id}) auto-generated successfully!");
                    TempData["Success"] = $" SUCCESS! PR {pr.PRReference} fully approved and PO {poReference} auto-generated!";
                }
                catch (Exception poEx)
                {
                    _logger.LogError(poEx, $" PO auto-generation FAILED");
                    pr.POGenerated = false;
                    pr.POReference = $" ERROR: {poEx.Message}";
                    await _context.SaveChangesAsync();
                    TempData["Error"] = $" PR approved but PO generation failed: {poEx.Message}. Please create PO manually.";
                }
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"  ERROR during auto-approval");
                TempData["Error"] = $" Error: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // API: Get Approval Statuses (for polling)
        [HttpGet("api/approvalstatuses")]
        public async Task<IActionResult> GetApprovalStatuses()
        {
            try
            {
                var pendingPRs = await _context.PurchaseRequisitions
                    .Include(pr => pr.WorkflowSteps.OrderBy(ws => ws.StepOrder))
                    .Where(pr => pr.CurrentStatus != WorkflowStatus.Draft &&
                                pr.CurrentStatus != WorkflowStatus.Approved &&
                                pr.CurrentStatus != WorkflowStatus.Rejected)
                    .Select(pr => new
                    {
                        id = pr.Id,
                        prReference = pr.PRReference,
                        currentStatus = pr.CurrentStatus.ToString(),
                        statusDisplay = pr.StatusDisplayName,
                        currentStep = pr.CurrentApprovalStep,
                        totalSteps = pr.TotalApprovalSteps,
                        workflowSteps = pr.WorkflowSteps.Select(ws => new
                        {
                            stepOrder = ws.StepOrder,
                            approverName = ws.ApproverName,
                            approverRole = ws.ApproverRole,
                            status = ws.Status.ToString(),
                            actionDate = ws.ActionDate
                        }).ToList()
                    })
                    .ToListAsync();
                return Json(new { success = true, data = pendingPRs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval statuses");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // API: Teams Approval Callback
        [HttpPost("api/prteams-approval")]
        [Route("api/pr/teams-approval")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessTeamsApproval([FromBody] TeamsApprovalRequest request)
        {
            try
            {
                _logger.LogInformation($"Received Teams approval: PR={request.PRReference}, Action={request.Action}, Email={request.ApproverEmail}");

                var pr = await _context.PurchaseRequisitions
                    .Include(p => p.WorkflowSteps)
                    .Include(p => p.CostCenters)
                    .FirstOrDefaultAsync(p => p.PRReference == request.PRReference);

                if (pr == null)
                {
                    _logger.LogWarning($"PR not found: {request.PRReference}");
                    return NotFound(new { success = false, message = "PR not found" });
                }

                var currentStep = pr.WorkflowSteps
                    .FirstOrDefault(s => s.ApproverEmail == request.ApproverEmail && s.Status == ApprovalStatus.Pending);

                if (currentStep == null)
                {
                    _logger.LogWarning($"No pending step found for {request.ApproverEmail}");
                    return BadRequest(new { success = false, message = "No pending approval found for this user" });
                }

                var status = request.Action.ToLower() == "approve" ? ApprovalStatus.Approved : ApprovalStatus.Rejected;

                var success = await _workflowService.ProcessApprovalStepAsync(
                    pr.Id,
                    request.ApproverEmail,
                    status,
                    request.Comments ?? "Approved via Teams");

                if (success)
                {
                    var nextStep = pr.WorkflowSteps
                        .Where(s => s.Status == ApprovalStatus.Pending)
                        .OrderBy(s => s.StepOrder)
                        .FirstOrDefault();

                    _logger.LogInformation($"Successfully processed {request.Action} from {request.ApproverEmail}");

                    return Ok(new
                    {
                        success = true,
                        message = $" Approved by {currentStep.ApproverName}",
                        currentStatus = pr.CurrentStatus.ToString(),
                        nextApprover = nextStep != null ? $"{nextStep.ApproverName} ({nextStep.ApproverRole})" : "Fully Approved",
                        approvedBy = currentStep.ApproverName,
                        approvedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                return BadRequest(new { success = false, message = "Failed to process approval" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Teams approval");
                return StatusCode(500, new { success = false, message = "Internal server error", error = ex.Message });
            }
        }

        // GET: Completed Requests
        [HttpGet("CompletedRequests")]
        public async Task<IActionResult> CompletedRequests()
        {
            var completedPRs = await _context.PurchaseRequisitions
                .Include(pr => pr.Items)
                .Where(pr => pr.CurrentStatus == WorkflowStatus.Approved)
                .OrderByDescending(pr => pr.FinalApprovalDate)
                .ToListAsync();

            return View(completedPRs);
        }

        #region Private Helper Methods

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
        private void ProcessItemsFromForm(PurchaseRequisition pr, IFormCollection formData)
        {
            _logger.LogInformation("=== PROCESSING ITEMS FROM FORM ===");
            var items = new List<PurchaseRequisitionItem>();

            var itemIndices = formData.Keys
                .Where(k => k.StartsWith("Items[") && k.Contains("].Description"))
                .Select(k =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(k, @"Items\[(\d+)\]");
                    return match.Success ? int.Parse(match.Groups[1].Value) : -1;
                })
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            _logger.LogInformation($"Found {itemIndices.Count} item indices in form data");

            foreach (var index in itemIndices)
            {
                var description = formData[$"Items[{index}].Description"].ToString();
                var quantityStr = formData[$"Items[{index}].Quantity"].ToString();
                var unitPriceStr = formData[$"Items[{index}].UnitPrice"].ToString();
                var discountPercentStr = formData[$"Items[{index}].DiscountPercent"].ToString();
                var discountAmountStr = formData[$"Items[{index}].DiscountAmount"].ToString();
                var gst = formData[$"Items[{index}].GST"].ToString();
                var supplier = formData[$"Items[{index}].SuggestedSupplier"].ToString();
                var paymentTerms = formData[$"Items[{index}].PaymentTerms"].ToString();
                var assetsClass = formData[$"Items[{index}].AssetsClass"].ToString();

                //   FIX: Properly detect Fixed Asset checkbox
                var isFixedAssetValue = formData[$"Items[{index}].IsFixedAsset"].ToString();
                bool isFixedAsset = !string.IsNullOrEmpty(isFixedAssetValue) &&
                                   (isFixedAssetValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                    isFixedAssetValue.Equals("on", StringComparison.OrdinalIgnoreCase));

                _logger.LogInformation($" Item {index}: IsFixedAsset raw='{isFixedAssetValue}' ? parsed={isFixedAsset}");

                // SKIP EMPTY ROWS
                if (string.IsNullOrWhiteSpace(description) &&
                    string.IsNullOrWhiteSpace(quantityStr) &&
                    string.IsNullOrWhiteSpace(unitPriceStr))
                {
                    _logger.LogInformation($"  Skipping empty item at index {index}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(description))
                {
                    _logger.LogWarning($"  Item {index} missing description");
                    continue;
                }

                if (!int.TryParse(quantityStr, out int quantity) || quantity <= 0)
                {
                    _logger.LogWarning($"  Item {index} invalid quantity: {quantityStr}");
                    continue;
                }

                if (!decimal.TryParse(unitPriceStr, out decimal unitPrice) || unitPrice < 0)
                {
                    _logger.LogWarning($"  Item {index} invalid unit price: {unitPriceStr}");
                    continue;
                }

                decimal discountPercent = 0;
                decimal discountAmount = 0;

                if (!string.IsNullOrWhiteSpace(discountPercentStr))
                {
                    decimal.TryParse(discountPercentStr, out discountPercent);
                }

                if (!string.IsNullOrWhiteSpace(discountAmountStr))
                {
                    decimal.TryParse(discountAmountStr, out discountAmount);
                }

                // GST validation
                if (!string.IsNullOrEmpty(gst))
                {
                    // Remove % if present and validate
                    gst = gst.Replace("%", "").Trim();
                    if (decimal.TryParse(gst, out decimal gstValue))
                    {
                        // Ensure it's a valid value
                        if (gstValue < 0) gstValue = 0;
                        if (gstValue > 10) gstValue = 10;
                        gst = $"{gstValue}%";
                    }
                    else
                    {
                        gst = "0%";
                    }
                }
                else
                {
                    gst = "0%";
                }

                var item = new PurchaseRequisitionItem
                {
                    Action = "Purchase",
                    Description = description?.Trim(),
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    DiscountPercent = discountPercent,
                    DiscountAmount = discountAmount,
                    GST = gst,
                    SuggestedSupplier = supplier?.Trim(),
                    PaymentTerms = paymentTerms?.Trim(),
                    IsFixedAsset = isFixedAsset,
                    AssetsClass = assetsClass?.Trim()
                };

                CalculateItemAmount(item);
                items.Add(item);

                _logger.LogInformation($" Item {index}: {description}, Qty={quantity}, Price=${unitPrice}, Amount=${item.Amount}, IsFixedAsset={isFixedAsset}");
            }

            pr.Items = items;

            if (items.Any())
            {
                //  : Set PR-level IsFixedAsset based on items
                pr.IsFixedAsset = items.Any(i => i.IsFixedAsset);
                pr.TotalAmount = items.Sum(i => i.Amount);

                _logger.LogInformation($"=== PROCESSING COMPLETE ===");
                _logger.LogInformation($"?? Total Items: {items.Count}");
                _logger.LogInformation($" Subtotal: ${pr.TotalAmount:F2}");
                _logger.LogInformation($"  PR IsFixedAsset: {pr.IsFixedAsset}");

                if (pr.IsFixedAsset)
                {
                    var fixedAssetCount = items.Count(i => i.IsFixedAsset);
                    _logger.LogInformation($" {fixedAssetCount} item(s) marked as Fixed Asset");
                }
            }
            else
            {
                _logger.LogError(" ERROR: No valid items found!");
                pr.TotalAmount = 0;
                pr.IsFixedAsset = false;
            }
        }
        private void CalculateItemAmount(PurchaseRequisitionItem item)
        {
            // Calculate subtotal BEFORE discount
            var subtotal = item.Quantity * item.UnitPrice;
            _logger.LogInformation($"=== Calculating Item: {item.Description} ===");
            _logger.LogInformation($"Quantity: {item.Quantity}, UnitPrice: ${item.UnitPrice}, Subtotal: ${subtotal}");
            decimal discount = 0;
            //  FIX: Properly handle discount calculation
            if (item.DiscountPercent > 0 && item.DiscountPercent <= 100)
            {
                // If discount percentage is entered, calculate discount amount
                discount = (subtotal * item.DiscountPercent) / 100;
                item.DiscountAmount = discount;
                _logger.LogInformation($"Applied {item.DiscountPercent}% discount = ${discount}");
            }
            else if (item.DiscountAmount > 0)
            {
                // If discount amount is entered directly, use it
                discount = item.DiscountAmount;
                //  : Validate that discount doesn't exceed subtotal
                if (discount >= subtotal)
                {
                    _logger.LogError($" ERROR: Discount ${discount} >= Subtotal ${subtotal}! Setting discount to 0.");
                    discount = 0;
                    item.DiscountAmount = 0;
                    item.DiscountPercent = 0;
                }
                else
                {
                    // Calculate the percentage
                    item.DiscountPercent = (discount / subtotal * 100);
                    _logger.LogInformation($"Applied fixed discount ${discount} ({item.DiscountPercent:F2}%)");
                }
            }
            else
            {
                // No discount
                item.DiscountPercent = 0;
                item.DiscountAmount = 0;
                _logger.LogInformation("No discount applied");
            }
            // Calculate final amount
            item.Amount = subtotal - discount;
            _logger.LogInformation($"Final Amount: ${item.Amount:F2}");
            //  VALIDATION: Final check
            if (item.Amount < 0)
            {
                _logger.LogError($" : Amount is NEGATIVE! Resetting to subtotal with no discount.");
                item.Amount = subtotal;
                item.DiscountPercent = 0;
                item.DiscountAmount = 0;
            }
            else if (item.Amount == 0 && subtotal > 0)
            {
                _logger.LogError($" : Amount is ZERO but subtotal is ${subtotal}! Resetting discount.");
                item.Amount = subtotal;
                item.DiscountPercent = 0;
                item.DiscountAmount = 0;
            }
        }
        private async Task ProcessCostCentersFromForm(PurchaseRequisition pr, IFormCollection formData)
        {
            var costCenters = new List<CostCenter>();
            _logger.LogInformation($"=== PROCESSING COST CENTERS ===");

            var costCenterIndices = formData.Keys
                .Where(k => k.StartsWith("CostCenters[") && k.Contains("].Name"))
                .Select(k =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(k, @"CostCenters\[(\d+)\]");
                    return match.Success ? int.Parse(match.Groups[1].Value) : -1;
                })
                .Where(i => i >= 0)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            decimal totalAmount = 0;

            foreach (var index in costCenterIndices)
            {
                var name = formData[$"CostCenters[{index}].Name"].ToString();
                var approver = formData[$"CostCenters[{index}].Approver"].ToString();
                var amountStr = formData[$"CostCenters[{index}].Amount"].ToString();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(approver))
                {
                    continue;
                }

                if (!decimal.TryParse(amountStr, out decimal amount) || amount <= 0)
                {
                    continue;
                }

                //  Get approver info from configuration
                var approverInfo = _configHelper.GetCostCenterApproverInfo(name);

                var costCenter = new CostCenter
                {
                    Name = name,
                    Approver = approver,
                    ApproverEmail = approverInfo.Email, //  From config
                    Amount = amount,
                    Distribution = "Amount",
                    ApprovalStatus = ApprovalStatusHOD.Pending,
                    ApproverRole = approverInfo.Role, //  From config
                    ApprovalOrder = costCenters.Count + 1,
                    IsRequired = true
                };

                costCenters.Add(costCenter);
                totalAmount += amount;

                _logger.LogInformation($"Added Cost Center: {name} = ${amount:F2} (Approver: {approver}, Email: {approverInfo.Email})");
            }

            pr.CostCenters = costCenters;
        }
        //Validate distribution against TAX-INCLUSIVE total
        private (bool IsValid, string ErrorMessage) ValidateDistribution(PurchaseRequisition pr)
        {
            if (pr.CostCenters?.Any() != true)
                return (false, "At least one cost center is required");
            var costCenterTotal = pr.CostCenters.Sum(cc => cc.Amount ?? 0);
            //  FIX: Log both values for debugging
            _logger.LogInformation($" VALIDATION: Cost Center Total = ${costCenterTotal:F2}, PR Total = ${pr.TotalAmount:F2}");
            if (Math.Abs(costCenterTotal - pr.TotalAmount) > 0.01m)
                return (false, $"Cost centers must equal total amount (incl. GST). Current: ${costCenterTotal:N2}, Required: ${pr.TotalAmount:N2}");
            foreach (var cc in pr.CostCenters)
            {
                if (string.IsNullOrWhiteSpace(cc.Approver) || string.IsNullOrWhiteSpace(cc.ApproverEmail))
                    return (false, $"Cost center '{cc.Name}' is missing approver information");
            }
            return (true, " Valid");
        }
        private decimal CalculateTotalWithGST(PurchaseRequisition pr)
        {
            if (pr.Items == null || !pr.Items.Any())
                return 0;

            decimal subtotal = pr.Items.Sum(i => i.Amount);
            decimal gstTotal = pr.Items.Sum(i =>
            {
                var gstRate = decimal.TryParse(i.GST?.Replace("%", ""), out var rate) ? rate : 0;
                return i.Amount * gstRate / 100;
            });

            return subtotal + gstTotal;
        }

        private string GetCurrentUser()
        {
            //  Try multiple sources in priority order
            // 1. Try BaseService (set by CookieAuthMiddleware)
            if (!string.IsNullOrEmpty(BaseService.Username))
            {
                _logger.LogInformation($" GetCurrentUser() from BaseService.Username: {BaseService.Username}");
                return BaseService.Username;
            }
            // 2. Try User Claims
            var nameClaim = User.FindFirst("name")?.Value
                            ?? User.FindFirst(ClaimTypes.Name)?.Value
                            ?? User.FindFirst("preferred_username")?.Value
                            ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
            if (!string.IsNullOrEmpty(nameClaim))
            {
                _logger.LogInformation($" GetCurrentUser() from Claims: {nameClaim}");
                return nameClaim;
            }
            // 3. Try User.Identity.Name
            if (!string.IsNullOrEmpty(User.Identity?.Name))
            {
                _logger.LogInformation($" GetCurrentUser() from User.Identity.Name: {User.Identity.Name}");
                return User.Identity.Name;
            }
            // 4. Fallback - log all claims for debugging
            _logger.LogWarning(" GetCurrentUser() - No username found! Logging all available claims:");
            foreach (var claim in User.Claims)
            {
                _logger.LogWarning($"  Claim: {claim.Type} = {claim.Value}");
            }
            _logger.LogError(" GetCurrentUser() returning fallback: Unknown User");
            return "Unknown User";
        }

        private void SetupViewBag()
        {
            var approvalThresholds = _approvalThresholdsAppSetting.Value;
            var departmentRoles = _departmentRolesAppSetting.Value;

            //ViewBag.Companies = _configuration.GetSection("AppSettings:CompanyOptions").Get<string[]>();
            ViewBag.Departments = _configuration.GetSection("AppSettings:DepartmentOptions").Get<string[]>();
            ViewBag.Currencies = _configuration.GetSection("AppSettings:CurrencyOptions").Get<string[]>();
            ViewBag.CurrencyDisplayNames = _configuration.GetSection("AppSettings:CurrencyDisplayNames").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            ViewBag.PurchaseTypes = _configuration.GetSection("AppSettings:PurchaseTypes").Get<string[]>();
            ViewBag.GSTOptions = _configuration.GetSection("AppSettings:GSTOptions").Get<string[]>() ?? new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

            // Load companies with full details
            ViewBag.Companies = _context.Companies
            .Where(c => c.IsActive)
                .Select(c => new
                {
                    c.CompanyName,
                    c.FullAddress  
                })
            .ToList();

            // Load vendors 
            var vendors = _context.SAPDropdownItems
                .Where(s => s.TypeName == "Vendor")
                .OrderBy(s => s.DDName)
                .ToList();
            ViewBag.Vendors = vendors;
            ViewBag.CFOThreshold = approvalThresholds.CFO_Threshold_SGD;
            ViewBag.CEOThreshold = approvalThresholds.CEO_Threshold_SGD;
            ViewBag.DepartmentRoles = departmentRoles.DepartmentRoles;
        }

        [HttpGet]
        [Route("api/company/{companyName}")]
        public async Task<IActionResult> GetCompanyDetails(string companyName)
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyName == companyName && c.IsActive);
                if (company == null)
                {
                    return NotFound(new { success = false, message = "Company not found" });
                }
                return Ok(new
                {
                    success = true,
                    companyName = company.CompanyName,
                    fullAddress = company.FullAddress,
                    address = company.Address,
                    city = company.City,
                    state = company.State,
                    postalCode = company.PostalCode,
                    country = company.Country,
                    phoneNumber = company.PhoneNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching company details");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Route("Index")]
        public async Task<IActionResult> Index(PRFilterViewModel filters)
        {
            try
            {
                _logger.LogInformation(" Loading PR Index...");
                // Initialize filters if null
                filters = filters ?? new PRFilterViewModel();
                // Start with base query
                IQueryable<PurchaseRequisition> query = _context.PurchaseRequisitions
                    .Include(pr => pr.Items);
                // Apply filters with null-safe checks
                if (!string.IsNullOrEmpty(filters.PRReference))
                {
                    query = query.Where(pr => pr.PRReference != null && pr.PRReference.Contains(filters.PRReference));
                }
                if (!string.IsNullOrEmpty(filters.Company))
                {
                    query = query.Where(pr => pr.Company != null && pr.Company == filters.Company);
                }
                if (!string.IsNullOrEmpty(filters.Department))
                {
                    query = query.Where(pr => pr.Department != null && pr.Department == filters.Department);
                }
                if (!string.IsNullOrEmpty(filters.SubmittedBy))
                {
                    query = query.Where(pr => pr.SubmittedBy != null && pr.SubmittedBy.Contains(filters.SubmittedBy));
                }
                if (filters.Status.HasValue)
                {
                    query = query.Where(pr => pr.CurrentStatus == filters.Status.Value);
                }
                if (filters.DateFrom.HasValue)
                {
                    query = query.Where(pr => pr.CreatedDate >= filters.DateFrom.Value);
                }
                if (filters.DateTo.HasValue)
                {
                    query = query.Where(pr => pr.CreatedDate <= filters.DateTo.Value.AddDays(1));
                }
                if (filters.AmountFrom.HasValue)
                {
                    query = query.Where(pr => pr.TotalAmount >= filters.AmountFrom.Value);
                }
                if (filters.AmountTo.HasValue)
                {
                    query = query.Where(pr => pr.TotalAmount <= filters.AmountTo.Value);
                }
                // Get total count BEFORE sorting and pagination
                var totalCount = await query.CountAsync();
                _logger.LogInformation($" Total PRs matching filters: {totalCount}");
                // Apply sorting
                string sortBy = filters.SortBy ?? "CreatedDate";
                string sortOrder = filters.SortOrder ?? "desc";
                IQueryable<PurchaseRequisition> sortedQuery;
                if (sortBy == "PRReference")
                {
                    sortedQuery = sortOrder == "asc"
                        ? query.OrderBy(pr => pr.PRReference ?? "")
                        : query.OrderByDescending(pr => pr.PRReference ?? "");
                }
                else if (sortBy == "Company")
                {
                    sortedQuery = sortOrder == "asc"
                        ? query.OrderBy(pr => pr.Company ?? "")
                        : query.OrderByDescending(pr => pr.Company ?? "");
                }
                else if (sortBy == "SubmittedBy")
                {
                    sortedQuery = sortOrder == "asc"
                        ? query.OrderBy(pr => pr.SubmittedBy ?? "")
                        : query.OrderByDescending(pr => pr.SubmittedBy ?? "");
                }
                else if (sortBy == "TotalAmount")
                {
                    sortedQuery = sortOrder == "asc"
                        ? query.OrderBy(pr => pr.TotalAmount)
                        : query.OrderByDescending(pr => pr.TotalAmount);
                }
                else if (sortBy == "Status")
                {
                    sortedQuery = sortOrder == "asc"
                        ? query.OrderBy(pr => pr.CurrentStatus)
                        : query.OrderByDescending(pr => pr.CurrentStatus);
                }
                else // Default: CreatedDate
                {
                    sortedQuery = sortOrder == "asc"
                        ? query.OrderBy(pr => pr.CreatedDate)
                        : query.OrderByDescending(pr => pr.CreatedDate);
                }
                _logger.LogInformation($" Query type: {query.GetType().Name}");
                _logger.LogInformation($" Query expression: {query.Expression}");
                _logger.LogInformation($" Total count before pagination: {totalCount}");
                // Get paginated results
                var prs = await query
                    .Skip((filters.PageNumber - 1) * filters.PageSize)
                    .Take(filters.PageSize)
                    .ToListAsync();
                _logger.LogInformation($" PRS is null: {prs == null}");
                _logger.LogInformation($" PRS count: {prs?.Count ?? -1}");
                // Get dropdown data
                var companies = await _context.PurchaseRequisitions
                    .Where(pr => !string.IsNullOrEmpty(pr.Company))
                    .Select(pr => pr.Company)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();
                var departments = await _context.PurchaseRequisitions
                    .Where(pr => !string.IsNullOrEmpty(pr.Department))
                    .Select(pr => pr.Department)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();
                var viewModel = new PRIndexViewModel
                {
                    PurchaseRequisitions = prs ?? new List<PurchaseRequisition>(),
                    Filters = filters,
                    TotalCount = totalCount,
                    TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)filters.PageSize) : 0,
                    Companies = companies ?? new List<string>(),
                    Departments = departments ?? new List<string>()
                };
                _logger.LogInformation($" Index loaded: {viewModel.TotalCount} total, {viewModel.PurchaseRequisitions.Count} displayed");
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error loading PR index");
                _logger.LogError($"Exception: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = $"Error loading purchase requisitions: {ex.Message}";
                return View(new PRIndexViewModel
                {
                    PurchaseRequisitions = new List<PurchaseRequisition>(),
                    Filters = filters ?? new PRFilterViewModel(),
                    TotalCount = 0,
                    TotalPages = 0,
                    Companies = new List<string>(),
                    Departments = new List<string>()
                });
            }
            #endregion
        }
    }
    public class TeamsApprovalRequest
    {
        public string PRReference { get; set; } = string.Empty;
        public string ApproverEmail { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Comments { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
