using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Models.ViewModels;
using TradingLimitMVC.Services;
using System.Security.Claims;

namespace TradingLimitMVC.Controllers
{
    [Route("PurchaseOrder")]
    public class PurchaseOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPowerAutomateService _powerAutomateService;
        private readonly IPDFService _pdfService;
        private readonly IPRPDFService _prPdfService;
        private readonly IPOApprovalWorkflowService _poApprovalService;
        private readonly ILogger<PurchaseOrderController> _logger;
        public PurchaseOrderController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IPowerAutomateService powerAutomateService,
            IPDFService pdfService,
            IPRPDFService prPdfService,
            IPOApprovalWorkflowService poApprovalService,
            ILogger<PurchaseOrderController> logger)
        {
            _context = context;
            _configuration = configuration;
            _powerAutomateService = powerAutomateService;
            _pdfService = pdfService;
            _prPdfService = prPdfService;
            _poApprovalService = poApprovalService;
            _logger = logger;
        }
        //// GET: PurchaseOrder
        //public async Task<IActionResult> Index()
        //{
        //    try
        //    {
        //        var purchaseOrders = await _context.PurchaseOrders
        //            .Include(po => po.Items)
        //            .Include(po => po.PurchaseRequisition)
        //            .Include(po => po.WorkflowSteps)
        //            .OrderByDescending(po => po.CreatedDate)
        //            .ToListAsync();
        //        return View(purchaseOrders);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading purchase orders");
        //        TempData["Error"] = "Error loading purchase orders. Please check the database.";
        //        return View(new List<PurchaseOrder>());
        //    }
        //}
        // GET: PurchaseOrder/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.PurchaseRequisition)
                        .ThenInclude(pr => pr.WorkflowSteps)
                    .Include(po => po.WorkflowSteps.OrderBy(ws => ws.StepOrder))
                    .Include(po => po.Approvals)
                    .FirstOrDefaultAsync(m => m.Id == id);
                if (purchaseOrder == null)
                {
                    TempData["Error"] = "Purchase Order not found.";
                    return RedirectToAction(nameof(Index));
                }
                // ============================================================
                // GET VENDOR DETAILS FROM DATABASE
                // ============================================================
                if (!string.IsNullOrEmpty(purchaseOrder.Vendor))
                {
                    var vendor = await _context.SAPDropdownItems
                        .Where(s => s.TypeName == "Vendor")
                        .Where(s => s.DDName == purchaseOrder.Vendor || s.VendorName == purchaseOrder.Vendor)
                        .FirstOrDefaultAsync();
                    if (vendor != null)
                    {
                        ViewBag.VendorName = vendor.DDName ?? vendor.VendorName;
                        ViewBag.VendorAddress = vendor.VendorAddress ?? "";
                        ViewBag.VendorAttention = vendor.ContactPerson ?? "";
                        ViewBag.VendorPhone = vendor.PhoneNumber ?? "";
                        ViewBag.VendorFax = vendor.FaxNumber ?? "";
                        ViewBag.VendorEmail = vendor.Email ?? "";
                    }
                    else
                    {
                        ViewBag.VendorName = purchaseOrder.Vendor;
                        ViewBag.VendorAddress = purchaseOrder.VendorAddress ?? "";
                        ViewBag.VendorAttention = purchaseOrder.Attention ?? "";
                        ViewBag.VendorPhone = purchaseOrder.PhoneNo ?? "";
                        ViewBag.VendorFax = purchaseOrder.FaxNo ?? "";
                        ViewBag.VendorEmail = purchaseOrder.Email ?? "";
                    }
                }


                // ============================================================
                //  USE PR'S SAVED DELIVERY ADDRESS
                //  PO.DeliveryAddress (if exists) ? PR.DeliveryAddress
                // ============================================================
                string deliveryAddress = "";

                if (!string.IsNullOrEmpty(purchaseOrder.DeliveryAddress))
                {
                    // PO has its own delivery address (may have been edited during PO creation)
                    deliveryAddress = purchaseOrder.DeliveryAddress;
                    _logger.LogInformation($"? Using PO's delivery address: {deliveryAddress}");
                }
                else if (purchaseOrder.PurchaseRequisition != null &&
                         !string.IsNullOrEmpty(purchaseOrder.PurchaseRequisition.DeliveryAddress))
                {
                    // Use PR's delivery address (which user may have edited in PR screen)
                    deliveryAddress = purchaseOrder.PurchaseRequisition.DeliveryAddress;
                    _logger.LogInformation($"? Using PR's saved delivery address: {deliveryAddress}");
                }

                ViewBag.CompanyFullAddress = deliveryAddress;
                // ============================================================
                // GET PR CONTACT DETAILS
                // ============================================================
                if (purchaseOrder.PurchaseRequisition != null)
                {
                    ViewBag.PRContactPerson = purchaseOrder.PurchaseRequisition.ContactPerson ?? purchaseOrder.POOriginator;
                    ViewBag.PRContactPhone = purchaseOrder.PurchaseRequisition.ContactPhoneNo ?? "";
                }
                else
                {
                    ViewBag.PRContactPerson = purchaseOrder.POOriginator ?? "N/A";
                    ViewBag.PRContactPhone = "";
                }
                // Pass currency to view
                ViewBag.Currency = purchaseOrder.Currency ?? "SGD";
                return View(purchaseOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading PO details for ID {Id}", id);
                TempData["Error"] = "Error loading Purchase Order details.";
                return RedirectToAction(nameof(Index));
            }
        }
        // GET: PurchaseOrder/Create (Manual creation with optional PR selection)
        public async Task<IActionResult> Create(int? prId = null)
        {
            await SetupViewBag();

            var model = new PurchaseOrder
            {
                IssueDate = DateTime.Now,
                DeliveryDate = DateTime.Now.AddDays(30),
                CreatedDate = DateTime.Now,
                Items = new List<PurchaseOrderItem>()
            };

            // If prId provided, pre-populate from PR
            if (prId.HasValue)
            {
                var pr = await _context.PurchaseRequisitions
                    .Include(p => p.Items)
                    .Include(p => p.CostCenters)
                    .FirstOrDefaultAsync(p => p.Id == prId.Value);

                if (pr != null)
                {
                    model.SelectedPRId = pr.Id;
                    model.PurchaseRequisitionId = pr.Id;
                    model.PRReference = pr.PRReference;
                    model.Company = pr.Company;
                    model.DeliveryAddress = pr.DeliveryAddress;
                    model.POOriginator = pr.SubmittedBy;
                    model.Attention = pr.ContactPerson;
                    model.PhoneNo = pr.ContactPhoneNo;

                    // Copy items from PR
                    model.Items = pr.Items.Select(item => new PurchaseOrderItem
                    {
                        Description = item.Description,
                        Details = item.Action,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountAmount = item.DiscountAmount,
                        Amount = item.Amount,
                        GST = item.GST ?? "7%",
                        PRNo = pr.PRReference
                    }).ToList();

                    ViewBag.SourcePR = pr;
                    ViewBag.PRCostCenters = pr.CostCenters;
                    ViewBag.PRApprovedBy = pr.FinalApprover;
                    ViewBag.PRApprovalDate = pr.FinalApprovalDate;
                    ViewBag.PROriginator = pr.SubmittedBy;
                   
                }
            }

            // Add one empty item if no items exist
            if (!model.Items.Any())
            {
                model.Items.Add(new PurchaseOrderItem
                {
                    Quantity = 1,
                    UnitPrice = 0,
                    DiscountPercent = 0,
                    DiscountAmount = 0,
                    Amount = 0
                });
            }

            return View(model);
        }
        // ============================================================
        // FIXED: GetCurrentUser to properly retrieve username
        // ============================================================
        private string GetCurrentUser()
        {
            // Priority 1: Try BaseService (set by CookieAuthMiddleware)
            if (!string.IsNullOrEmpty(BaseService.Username))
            {
                _logger.LogInformation($"?? GetCurrentUser from BaseService: {BaseService.Username}");
                return BaseService.Username;
            }

            // Priority 2: Try User Claims
            var nameClaim = User.FindFirst("name")?.Value
                            ?? User.FindFirst(ClaimTypes.Name)?.Value
                            ?? User.FindFirst("preferred_username")?.Value
                            ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value
                            ?? User.FindFirst(ClaimTypes.GivenName)?.Value;

            if (!string.IsNullOrEmpty(nameClaim))
            {
                _logger.LogInformation($" GetCurrentUser from Claims: {nameClaim}");
                return nameClaim;
            }

            // Priority 3: Try User.Identity.Name
            if (!string.IsNullOrEmpty(User.Identity?.Name))
            {
                _logger.LogInformation($" GetCurrentUser from Identity: {User.Identity.Name}");
                return User.Identity.Name;
            }

            // Priority 4: Try email as fallback
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value
                            ?? User.FindFirst("email")?.Value;

            if (!string.IsNullOrEmpty(emailClaim))
            {
                _logger.LogInformation($" GetCurrentUser from Email: {emailClaim}");
                return emailClaim;
            }

            // Fallback - log all claims for debugging
            _logger.LogWarning(" No username found! Available claims:");
            foreach (var claim in User.Claims)
            {
                _logger.LogWarning($"  {claim.Type} = {claim.Value}");
            }

            _logger.LogError(" Returning fallback: Unknown User");
            return "Unknown User";
        }

        // POST: PurchaseOrder/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrder purchaseOrder)
        {
            try
            {
                // Remove validation for Items to handle dynamic rows
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Items")).ToList())
                {
                    ModelState.Remove(key);
                }

                if (!ModelState.IsValid)
                {
                    await SetupViewBag();
                    return View(purchaseOrder);
                }

                // Generate PO references
                var count = await _context.PurchaseOrders.CountAsync();
                purchaseOrder.POReference = $"PO-{DateTime.Now.Year}-{(count + 1):D3}";
                purchaseOrder.PONo = $"{(count + 1):D4}";
                purchaseOrder.CreatedDate = DateTime.Now;
                purchaseOrder.POStatus = "Draft";

                // Process items
                var validItems = new List<PurchaseOrderItem>();
                if (purchaseOrder.Items != null)
                {
                    foreach (var item in purchaseOrder.Items)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Description) &&
                            item.Quantity > 0 && item.UnitPrice > 0)
                        {
                            var subtotal = item.Quantity * item.UnitPrice;
                            var discount = item.DiscountPercent > 0
                                ? (subtotal * item.DiscountPercent / 100)
                                : item.DiscountAmount;

                            item.Amount = subtotal - discount;
                            validItems.Add(item);
                        }
                    }
                }

                if (!validItems.Any())
                {
                    TempData["Error"] = "At least one valid item is required.";
                    await SetupViewBag();
                    return View(purchaseOrder);
                }

                purchaseOrder.Items = validItems;

                // Link to PR if selected
                if (purchaseOrder.SelectedPRId.HasValue)
                {
                    var pr = await _context.PurchaseRequisitions.FindAsync(purchaseOrder.SelectedPRId.Value);
                    if (pr != null)
                    {
                        pr.POGenerated = true;
                        pr.POGeneratedDate = DateTime.Now;
                        pr.POReference = purchaseOrder.POReference;
                        purchaseOrder.PurchaseRequisitionId = pr.Id;
                        purchaseOrder.PRReference = pr.PRReference;
                    }
                }

                _context.Add(purchaseOrder);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Purchase Order {purchaseOrder.POReference} created successfully!";
                return RedirectToAction(nameof(Details), new { id = purchaseOrder.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PO");
                TempData["Error"] = "Error creating Purchase Order: " + ex.Message;
                await SetupViewBag();
                return View(purchaseOrder);
            }
        }
        public async Task<IActionResult> CompletedRequests()
        {
            var completedRequests = await _context.PurchaseRequisitions
                .Include(pr => pr.Items)
                .Include(pr => pr.CostCenters)
                .Include(pr => pr.WorkflowSteps)
                .Where(pr => pr.CurrentStatus == WorkflowStatus.Approved) // ONLY approved
                .OrderByDescending(pr => pr.FinalApprovalDate)
                .ToListAsync();
            return View(completedRequests);
        }

        // GET: Create PO from Approved PR
        [HttpGet("CreateFromPR/{prId}")]
        public async Task<IActionResult> CreateFromPR(int prId)
        {
            int poId = 0;

            try
            {
                var pr = await _context.PurchaseRequisitions
                    .Include(p => p.WorkflowSteps)
                    .Include(p => p.Approvals)
                    .Include(p => p.Items)
                    .Include(p => p.CostCenters)
                    .FirstOrDefaultAsync(p => p.Id == prId);


                // Check "No PO Required" flag
                if (pr.NoPORequired)
                {
                    _logger.LogInformation($" 'No PO Required' is checked - skipping PO generation");
                    TempData["Success"] = $" PR {pr.PRReference} fully approved (No PO required)";
                    return RedirectToAction(nameof(Details), new { prId });
                }
                // Check if PR has items before attempting PO generation
                if (pr.Items == null || !pr.Items.Any())
                {
                    _logger.LogError($" PR {pr.PRReference} has NO items - cannot generate PO");
                    TempData["Error"] = $" PR approved but has no items. Cannot auto-generate PO.";
                    return RedirectToAction(nameof(Details), new { prId });
                }
                // AUTO-GENERATE PO
                try
                {
                    _logger.LogInformation($"?? Starting PO auto-generation...");
                    var poCount = await _context.PurchaseOrders.CountAsync();
                    var poReference = $"PO-{DateTime.Now.Year}-{(poCount + 1):D3}";
                    var defaultVendor = pr.Items.FirstOrDefault()?.SuggestedSupplier ?? "TBD";
                    var paymentTerms = pr.Items.FirstOrDefault()?.PaymentTerms;
                    var currentUser = GetCurrentUser();
                    var currency = pr.QuotationCurrency ?? "SGD";

                    // ============================================================
                    // FIX 1: Get company delivery address from Company table
                    // ============================================================
                    string deliveryAddress = pr.DeliveryAddress ?? "";
                    

                    var po = new PurchaseOrder
                    {
                        POReference = poReference,
                        PONo = $"{(poCount + 1):D4}",
                        PurchaseRequisitionId = pr.Id,
                        PRReference = pr.PRReference,
                        Company = pr.Company ?? "Default Company",
                        DeliveryAddress = deliveryAddress,
                        Attention = pr.ContactPerson,
                        PhoneNo = pr.ContactPhoneNo,
                        IssueDate = DateTime.Now,
                        DeliveryDate = pr.ExpectedDeliveryDate ?? DateTime.Now.AddDays(30),
                        POOriginator = BaseService.Username,
                        Vendor = defaultVendor,
                        VendorAddress = pr.VendorFullAddress ?? "",
                        PaymentTerms = paymentTerms,
                        POStatus = "Auto-Generated from Approved PR",
                        CurrentStatus = POWorkflowStatus.Issued,
                        CreatedDate = DateTime.Now,
                        SubmittedBy = BaseService.Username,
                        SubmittedDate = DateTime.Now,
                        OrderIssuedBy = BaseService.Username,
                        Currency = currency,
                        Items = pr.Items.Select(item => new PurchaseOrderItem
                        {
                            Description = item.Description ?? "Item",
                            Details = $"{item.Action} - {item.Description}",
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            DiscountPercent = item.DiscountPercent,
                            DiscountAmount = item.DiscountAmount,
                            Amount = item.Amount,
                            GST = item.GST ?? "0%",
                            PRNo = pr.PRReference
                        }).ToList()
                    };


                    // Contact & Delivery Information - FROM PR
                    ViewBag.PRContactPerson = pr.ContactPerson;
                    ViewBag.PRContactPhone = pr.ContactPhoneNo;
                    await SetupViewBag();

                    _context.PurchaseOrders.Add(po);
                    pr.POGenerated = true;
                    pr.POGeneratedDate = DateTime.Now;
                    pr.POReference = poReference;
                    await _context.SaveChangesAsync();
                    poId = po.Id;
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
                return RedirectToAction(nameof(Details), new { id = poId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving PO");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id = poId });
            }
        }

        // GET: Create PO from Approved PR
        public async Task<IActionResult> CreateFromPR(PurchaseOrder po, int prId)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Get PR to verify
                    var pr = await _context.PurchaseRequisitions
                        .Include(p => p.Items)
                        .FirstOrDefaultAsync(p => p.Id == prId);
                    if (pr == null)
                    {
                        TempData["Error"] = "Purchase Requisition not found.";
                        return RedirectToAction("CompletedRequests", "PurchaseRequisition");
                    }
                    // Ensure vendor details are preserved if they exist in the form
                    if (string.IsNullOrEmpty(po.VendorAddress) && !string.IsNullOrEmpty(po.Vendor))
                    {
                        // Re-fetch vendor details if missing
                        var vendor = await _context.SAPDropdownItems
                            .Where(s => s.TypeName == "Vendor")
                            .Where(s => s.DDName == po.Vendor || s.VendorName == po.Vendor)
                            .FirstOrDefaultAsync();
                        if (vendor != null)
                        {
                            po.VendorAddress = vendor.VendorAddress;
                            po.Attention = vendor.ContactPerson;
                            po.PhoneNo = vendor.PhoneNumber;
                            po.Email = vendor.Email;
                            po.FaxNo = vendor.FaxNumber;
                        }
                    }
                    po.PurchaseRequisitionId = pr.Id;
                    po.PRReference = pr.PRReference;
                    po.CreatedDate = DateTime.Now;
                    po.POStatus = "Draft";
                    po.CurrentStatus = POWorkflowStatus.Draft;
                    _context.Add(po);
                    await _context.SaveChangesAsync();
                    // Update PR
                    pr.POGenerated = true;
                    pr.POReference = po.POReference;
                    pr.SubmittedDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Purchase Order created successfully!";
                    return RedirectToAction(nameof(Details), new { id = po.Id });
                }
                await SetupViewBag();
                return View(po);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving PO");
                TempData["Error"] = $"Error: {ex.Message}";
                return View(po);
            }
        }

        // POST: PurchaseOrder/CreateFromPR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromPR(PurchaseOrder purchaseOrder)
        {
            try
            {
                // Remove validation for Items and vendor fields
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Items")).ToList())
                {
                    ModelState.Remove(key);
                }

                //  Get current user
                var currentUser = GetCurrentUser();

                // Vendor is pre-filled from PR, so it should already be valid
                if (string.IsNullOrEmpty(purchaseOrder.Vendor))
                {
                    // Try to get vendor from PR again if missing
                    if (purchaseOrder.PurchaseRequisitionId.HasValue)
                    {
                        var pr = await _context.PurchaseRequisitions
                            .Include(p => p.Items)
                            .FirstOrDefaultAsync(p => p.Id == purchaseOrder.PurchaseRequisitionId.Value);

                        var firstItem = pr?.Items?.FirstOrDefault();
                        if (firstItem != null && !string.IsNullOrEmpty(firstItem.SuggestedSupplier))
                        {
                            purchaseOrder.Vendor = firstItem.SuggestedSupplier;
                        }
                    }

                    if (string.IsNullOrEmpty(purchaseOrder.Vendor))
                    {
                        ModelState.AddModelError("Vendor", "Vendor is required.");
                    }
                }

                if (!purchaseOrder.Items?.Any() == true)
                {
                    ModelState.AddModelError("", "At least one item is required.");
                }
                //Get currency from PR
                if (purchaseOrder.PurchaseRequisitionId.HasValue && string.IsNullOrEmpty(purchaseOrder.Currency))
                {
                    var pr = await _context.PurchaseRequisitions
                        .FirstOrDefaultAsync(p => p.Id == purchaseOrder.PurchaseRequisitionId.Value);

                    if (pr != null)
                    {
                        purchaseOrder.Currency = pr.QuotationCurrency ?? "SGD";
                        _logger.LogInformation($" Set PO currency from PR: {purchaseOrder.Currency}");
                    }
                }

                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Please fix the validation errors.";
                    await SetupViewBag();
                    // Reload PR data for display
                    if (purchaseOrder.PurchaseRequisitionId.HasValue)
                    {
                        var pr = await _context.PurchaseRequisitions
                            .Include(p => p.Items)
                            .Include(p => p.CostCenters)
                            .Include(p => p.WorkflowSteps)
                            .FirstOrDefaultAsync(p => p.Id == purchaseOrder.PurchaseRequisitionId.Value);
                        ViewBag.SourcePR = pr;
                        ViewBag.PRCostCenters = pr?.CostCenters;
                        ViewBag.PRWorkflowSteps = pr?.WorkflowSteps;
                        ViewBag.PRTotalAmount = pr?.TotalAmount;
                        ViewBag.PRApprovedBy = pr?.FinalApprover;
                        ViewBag.PRApprovalDate = pr?.FinalApprovalDate;
                        ViewBag.PROriginator = pr?.SubmittedBy;
                        ViewBag.PaymentTERMS = pr?.Items;
                        ViewBag.Currency = pr?.QuotationCurrency ?? "SGD";
                    }
                    return View("CreateFromPR", purchaseOrder);
                }

                
                purchaseOrder.CreatedDate = DateTime.Now;
                purchaseOrder.POStatus = "Draft";
                purchaseOrder.CurrentStatus = POWorkflowStatus.Draft;
                purchaseOrder.SubmittedBy = BaseService.Username;
                purchaseOrder.ActionTime = DateTime.Now;
                purchaseOrder.ActionBy = GetCurrentUser();
                purchaseOrder.POOriginator = currentUser;
                purchaseOrder.OrderIssuedBy = currentUser;

                if (purchaseOrder.Items?.Any() == true)
                {
                    foreach (var item in purchaseOrder.Items)
                    {
                        var subtotal = item.Quantity * item.UnitPrice;
                        var discount = item.DiscountPercent > 0
                            ? (subtotal * item.DiscountPercent / 100)
                            : item.DiscountAmount;
                        item.Amount = subtotal - discount;
                    }
                }

                if (purchaseOrder.PurchaseRequisitionId.HasValue)
                {
                    var pr = await _context.PurchaseRequisitions.FindAsync(purchaseOrder.PurchaseRequisitionId.Value);
                    if (pr != null)
                    {
                        pr.POGenerated = true;
                        pr.POGeneratedDate = DateTime.Now;
                        pr.POReference = purchaseOrder.POReference;
                    }
                }

                _context.Add(purchaseOrder);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"PO {purchaseOrder.POReference} created from PR {purchaseOrder.PRReference}");
                TempData["Success"] = $"Purchase Order {purchaseOrder.POReference} created successfully!";
                return RedirectToAction(nameof(Details), new { id = purchaseOrder.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PO from PR");
                TempData["Error"] = $"Error: {ex.Message}";
                await SetupViewBag();
                // Reload PR data on error...
                return View("CreateFromPR", purchaseOrder);
            }
        }
        // POST: Submit for Approval
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(int id)
        {
            try
            {
                var po = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .Include(p => p.WorkflowSteps)
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (po == null)
                {
                    TempData["Error"] = "Purchase Order not found.";
                    return RedirectToAction(nameof(Index));
                }
                // Generate PO workflow steps based on amount
                po.WorkflowSteps = await _poApprovalService.GeneratePOWorkflowStepsAsync(po);
                po.TotalApprovalSteps = po.WorkflowSteps.Count;
                po.CurrentApprovalStep = po.WorkflowSteps.Any() ? 1 : 0;
                po.CurrentStatus = POWorkflowStatus.Submitted;
                po.SubmittedDate = DateTime.Now;
                po.SubmittedBy = BaseService.Username;
                po.ActionTime = DateTime.Now;
                po.ActionBy = GetCurrentUser();
                await _context.SaveChangesAsync();
                // Notify first approver if exists
                var firstStep = po.WorkflowSteps.FirstOrDefault(s => s.StepOrder == 1);
                if (firstStep != null)
                {
                    await _powerAutomateService.TriggerPOApprovalWorkflowAsync(
                        firstStep.ApproverEmail, po.POReference, po);
                }
                TempData["Success"] = $"PO {po.POReference} submitted for approval";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting PO {Id}", id);
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        // Download PO PDF
        [HttpGet("DownloadPDF/{id}")]
        public async Task<IActionResult> DownloadPDF(int id)
        {
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.PurchaseRequisition)
                    .FirstOrDefaultAsync(m => m.Id == id);
                if (purchaseOrder == null)
                {
                    TempData["Error"] = "Purchase Order not found.";
                    return RedirectToAction(nameof(Index));
                }
                // ============================================================
                // FETCH AND POPULATE VENDOR DETAILS
                // ============================================================
                if (!string.IsNullOrEmpty(purchaseOrder.Vendor))
                {
                    var vendor = await _context.SAPDropdownItems
                        .Where(s => s.TypeName == "Vendor")
                        .Where(s => s.DDName == purchaseOrder.Vendor || s.VendorName == purchaseOrder.Vendor)
                        .FirstOrDefaultAsync();
                    if (vendor != null)
                    {
                        purchaseOrder.Vendor = vendor.DDName ?? vendor.VendorName;
                        purchaseOrder.VendorAddress = vendor.VendorAddress;
                        purchaseOrder.Attention = vendor.ContactPerson;
                        purchaseOrder.PhoneNo = vendor.PhoneNumber;
                        purchaseOrder.Email = vendor.Email;
                        purchaseOrder.FaxNo = vendor.FaxNumber;
                        _logger.LogInformation($" Vendor details loaded for PDF: {vendor.DDName}");
                    }
                }
                if (string.IsNullOrEmpty(purchaseOrder.DeliveryAddress) &&
                purchaseOrder.PurchaseRequisition != null)
                {
                    purchaseOrder.DeliveryAddress = purchaseOrder.PurchaseRequisition.DeliveryAddress ?? "";
                    _logger.LogInformation($"? Using PR's delivery address for PDF: {purchaseOrder.DeliveryAddress}");
                }
                else
                {
                    _logger.LogInformation($"? Using PO's delivery address for PDF: {purchaseOrder.DeliveryAddress}");
                }
                // ============================================================
                // GET CONTACT PERSON FROM PR
                // ============================================================
                if (purchaseOrder.PurchaseRequisition != null)
                {
                    // POOriginator should be the PR contact person for delivery section
                    if (string.IsNullOrEmpty(purchaseOrder.POOriginator))
                    {
                        purchaseOrder.POOriginator = purchaseOrder.PurchaseRequisition.ContactPerson
                            ?? purchaseOrder.PurchaseRequisition.SubmittedBy
                            ?? "N/A";
                    }

                    _logger.LogInformation($"? POOriginator set to: {purchaseOrder.POOriginator}");
                }
                // ============================================================
                //  FIX: ENSURE CURRENCY IS SET FROM PR
                // ============================================================
                if (purchaseOrder.PurchaseRequisition != null)
                {
                    // Priority 1: Use PR's QuotationCurrency
                    if (!string.IsNullOrEmpty(purchaseOrder.PurchaseRequisition.QuotationCurrency))
                    {
                        purchaseOrder.Currency = purchaseOrder.PurchaseRequisition.QuotationCurrency;
                        _logger.LogInformation($" Currency set from PR: {purchaseOrder.Currency}");
                    }
                    // Priority 2: If PO already has currency, keep it
                    else if (string.IsNullOrEmpty(purchaseOrder.Currency))
                    {
                        // Priority 3: Default to SGD
                        purchaseOrder.Currency = "SGD";
                        _logger.LogWarning($" No currency found in PR, defaulting to SGD");
                    }
                }
                else
                {
                    // No PR relationship - use PO's currency or default to SGD
                    if (string.IsNullOrEmpty(purchaseOrder.Currency))
                    {
                        purchaseOrder.Currency = "SGD";
                        _logger.LogWarning($" No PR relationship found, defaulting to SGD");
                    }
                }

                // Generate PDF with complete data
                var pdfBytes = await _pdfService.GeneratePOPdfAsync(purchaseOrder);
                var fileName = $"PO_{purchaseOrder.POReference}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for PO {Id}", id);
                TempData["Error"] = "Error generating PDF.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        private async Task SetupViewBag()
        {
            ViewBag.Companies = await _context.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .Select(c => new {
                Value = c.CompanyName,
                Text = c.CompanyName,
                FullAddress = c.FullAddress
            })
            .ToListAsync();
            //  FIX: Add null check and error handling for vendors
            try
            {
                var vendors = await _context.SAPDropdownItems
                    .Where(s => s.TypeName == "Vendor")
                    .OrderBy(s => s.DDName)
                    .Select(s => s.DDName)
                    .ToListAsync();
                // If no vendors found, add a default message
                if (!vendors.Any())
                {
                    ViewBag.VendorOptions = new List<string> { "No Vendors Available" };
                    _logger.LogWarning("No vendors found in SAPDropdownItems table");
                }
                else
                {
                    ViewBag.VendorOptions = vendors;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading vendors from database");
                ViewBag.VendorOptions = new List<string> { "Error Loading Vendors" };
            }
            // Get approved PRs
            try
            {
                var approvedPRs = await _context.PurchaseRequisitions
                    .Where(pr => pr.CurrentStatus == WorkflowStatus.Approved && !pr.POGenerated)
                    .Select(pr => new SelectListItem
                    {
                        Value = pr.Id.ToString(),
                        Text = $"{pr.PRReference} - {pr.ShortDescription}"
                    })
                    .ToListAsync();
                ViewBag.ApprovedPRs = approvedPRs ?? new List<SelectListItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading approved PRs");
                ViewBag.ApprovedPRs = new List<SelectListItem>();
            }
        }

        [HttpGet]
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index(POFilterViewModel filters)
        {
            try
            {
                var query = _context.PurchaseOrders
                    .Include(po => po.Items)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(filters.POReference))
                {
                    query = query.Where(po => po.POReference.Contains(filters.POReference));
                }

                if (!string.IsNullOrEmpty(filters.PRReference))
                {
                    query = query.Where(po => po.PRReference.Contains(filters.PRReference));
                }

                if (!string.IsNullOrEmpty(filters.Vendor))
                {
                    query = query.Where(po => po.Vendor == filters.Vendor);
                }

                if (!string.IsNullOrEmpty(filters.Company))
                {
                    query = query.Where(po => po.Company.Contains(filters.Company));
                }

                if (!string.IsNullOrEmpty(filters.POOriginator))
                {
                    query = query.Where(po => po.POOriginator.Contains(filters.POOriginator));
                }

                if (filters.Status.HasValue)
                {
                    query = query.Where(po => po.CurrentStatus == filters.Status.Value);
                }

                if (filters.DateFrom.HasValue)
                {
                    query = query.Where(po => po.IssueDate >= filters.DateFrom.Value);
                }

                if (filters.DateTo.HasValue)
                {
                    query = query.Where(po => po.IssueDate <= filters.DateTo.Value.AddDays(1));
                }

                if (filters.AmountFrom.HasValue)
                {
                    query = query.Where(po => po.Items.Sum(i => i.Amount) >= filters.AmountFrom.Value);
                }

                if (filters.AmountTo.HasValue)
                {
                    query = query.Where(po => po.Items.Sum(i => i.Amount) <= filters.AmountTo.Value);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply sorting
                query = filters.SortBy switch
                {
                    "POReference" => filters.SortOrder == "asc"
                        ? query.OrderBy(po => po.POReference)
                        : query.OrderByDescending(po => po.POReference),
                    "PRReference" => filters.SortOrder == "asc"
                        ? query.OrderBy(po => po.PRReference)
                        : query.OrderByDescending(po => po.PRReference),
                    "Vendor" => filters.SortOrder == "asc"
                        ? query.OrderBy(po => po.Vendor)
                        : query.OrderByDescending(po => po.Vendor),
                    "POOriginator" => filters.SortOrder == "asc"
                        ? query.OrderBy(po => po.POOriginator)
                        : query.OrderByDescending(po => po.POOriginator),
                    "Status" => filters.SortOrder == "asc"
                        ? query.OrderBy(po => po.CurrentStatus)
                        : query.OrderByDescending(po => po.CurrentStatus),
                    _ => filters.SortOrder == "asc"
                        ? query.OrderBy(po => po.IssueDate)
                        : query.OrderByDescending(po => po.IssueDate)
                };

                // Apply pagination
                var pos = await query
                    .Skip((filters.PageNumber - 1) * filters.PageSize)
                    .Take(filters.PageSize)
                    .ToListAsync();

                // Get dropdown data
                var vendors = await _context.PurchaseOrders
                    .Select(po => po.Vendor)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToListAsync();

                var companies = await _context.PurchaseOrders
                    .Select(po => po.Company)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                var viewModel = new POIndexViewModel
                {
                    PurchaseOrders = pos,
                    Filters = filters,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)filters.PageSize),
                    Vendors = vendors,
                    Companies = companies
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading PO index");
                TempData["Error"] = "Error loading purchase orders.";
                return View(new POIndexViewModel());
            }
        }
    }
}
