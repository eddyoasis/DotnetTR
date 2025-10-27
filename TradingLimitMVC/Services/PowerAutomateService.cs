using TradingLimitMVC.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TradingLimitMVC.Services
{
    public interface IPowerAutomateService
    {
        Task<bool> TriggerApprovalWorkflowAsync(string email, string prReference = "", PurchaseRequisition pr = null);
        Task TriggerPOApprovalWorkflowAsync(string approverEmail, string? pOReference, PurchaseOrder po);
        Task<bool> TriggerRejectionNotificationAsync(string submitterEmail, string prReference, string rejectedBy, string rejectionReason);
    }

    public class PowerAutomateService : IPowerAutomateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PowerAutomateService> _logger;
        private readonly string _powerAutomateUrl;
        private readonly string _apiKey;
        private readonly bool _requiresAuth;

        public PowerAutomateService(HttpClient httpClient, ILogger<PowerAutomateService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _powerAutomateUrl = configuration["PowerAutomate:WorkflowUrl"] ?? "";
            _requiresAuth = configuration.GetValue<bool>("PowerAutomate:RequiresAuth", true);
            _apiKey = configuration["PowerAutomate:ApiKey"] ?? "37adabcc-355c-4dc6-9866-c6c603a6d045";
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<bool> TriggerRejectionNotificationAsync(string submitterEmail, string prReference, string rejectedBy, string rejectionReason)
        {
            try
            {
                // Build rejection notification payload
                var requestBody = new
                {
                    recipientEmail = submitterEmail,
                    notificationType = "PRRejection",
                    prReference = prReference,
                    rejectedBy = rejectedBy,
                    rejectionReason = rejectionReason,
                    rejectionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    editLink = $"https://localhost:7218/PurchaseRequisition/Edit/{prReference}",
                    subject = $"PR {prReference} Rejected - Action Required"
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // TODO: Update this URL to your rejection notification flow endpoint
                // For now, using the same URL (you should create a separate Power Automate flow)
                var response = await _httpClient.PostAsync(_powerAutomateUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Rejection notification sent to {submitterEmail} for PR {prReference}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to send rejection notification: Status={response.StatusCode}, Error={errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception sending rejection notification for PR {prReference}");
                return false;
            }
        }


        public async Task<bool> TriggerApprovalWorkflowAsync(string email, string prReference = "", PurchaseRequisition pr = null)
        {
            try
            {
                if (pr == null)
                {
                    _logger.LogWarning("Cannot trigger workflow - PR object is null");
                    return false;
                }
                // Build cost centers list
                var costCentersList = new List<object>();
                if (pr.CostCenters != null)
                {
                    foreach (var cc in pr.CostCenters)
                    {
                        costCentersList.Add(new
                        {
                            costcenter = cc.Name,
                            amount = cc.Amount ?? 0
                        });
                    }
                }
                // Build purchase items list
                var purchaseItemsList = new List<object>();
                if (pr.Items != null)
                {
                    foreach (var item in pr.Items)
                    {
                        purchaseItemsList.Add(new
                        {
                            description = item.Description,
                            quantity = item.Quantity,
                            unitPrice = item.UnitPrice,
                            discountPercentage = item.DiscountPercent,
                            discountAmount = item.DiscountAmount,
                            amount = item.Amount,
                            GST = item.GST,
                            suggestedSupplier = item.SuggestedSupplier,
                            paymentTerms = item.PaymentTerms,
                            fixedAssets = item.IsFixedAsset,
                            assetsClass = item.AssetsClass,
                            maintenaceFrom = item.MaintenanceFrom?.ToString("yyyyMMdd"),
                            maintenaceTo = item.MaintenanceTo?.ToString("yyyyMMdd")
                        });
                    }
                }

                var requestBody = new
                {
                    emailRequestor = GetSubmitterEmail(pr.SubmittedBy),
                    emailApprover = email,
                    Email = email, // You have both emailApprover and Email
                    PRReference = prReference,
                    company = pr.Company,
                    department = pr.Department,
                    isITRelated = pr.IsITRelated,
                    isNoPORequired = pr.NoPORequired,
                    hasSignedPAF = pr.SignedPDF,
                    costcenters = costCentersList,
                    expectedDeliveryDate = pr.ExpectedDeliveryDate?.ToString("yyyyMMdd"),
                    deliveryAddress = pr.DeliveryAddress,
                    contactPerson = pr.ContactPerson,
                    contactPhoneNo = pr.ContactPhoneNo,
                    quotationCurrency = pr.QuotationCurrency ?? "USD",
                    shortDescription = pr.ShortDescription,
                    typeOfPurchase = pr.TypeOfPurchase,
                    reason = pr.Reason,
                    totalAmount = pr.TotalAmount,
                    purchaseitems = purchaseItemsList
                };
                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Clear();
                if (_requiresAuth && !string.IsNullOrEmpty(_apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                }
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.UserAgent.Clear();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TradingLimitMVC/1.0");
                _logger.LogInformation("Triggering Power Automate for {Email}, PR: {PRReference}", email, prReference);
                _logger.LogInformation("Payload: {Payload}", json);
                var response = await _httpClient.PostAsync(_powerAutomateUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Power Automate success: {Response}", responseContent);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Power Automate failed: Status={Status}, Error={Error}",
                        response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception triggering Power Automate for {Email}", email);
                return false;
            }
        }
        // Helper method to get submitter email
        private string GetSubmitterEmail(string submitterName)
        {
            _logger.LogInformation($" GetSubmitterEmail called with: {submitterName}");
            _logger.LogInformation($" BaseService.Email: {BaseService.Email}");
            _logger.LogInformation($" BaseService.Username: {BaseService.Username}");
            // 1. If submitterName is already an email, return it
            if (!string.IsNullOrEmpty(submitterName) && submitterName.Contains("@"))
            {
                _logger.LogInformation($" Returning email from submitterName: {submitterName}");
                return submitterName;
            }
            // 2. Try to get email from BaseService (set by CookieAuthMiddleware)
            if (!string.IsNullOrEmpty(BaseService.Email))
            {
                _logger.LogInformation($" Returning email from BaseService: {BaseService.Email}");
                return BaseService.Email;
            }
            // 3. Fallback to default email
            _logger.LogWarning($" Using fallback email for: {submitterName}");
            return "elahvarasi.raju@kgi.com";
        }

        public Task TriggerPOApprovalWorkflowAsync(string approverEmail, string? pOReference, PurchaseOrder po)
        {
            throw new NotImplementedException();
        }
    }

}
