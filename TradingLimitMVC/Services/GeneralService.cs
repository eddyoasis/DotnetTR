using ClassLibrary1.Services;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using TradingLimitMVC.Models.AppSettings;
using ClassLibrary1Models = ClassLibrary1.Models;
using ClassLibrary1Services = ClassLibrary1.Services;

namespace TradingLimitMVC.Services
{
    public interface IGeneralService
    {
        Task<ClassLibrary1Models.PowerAutomateResponse?> GetUserInfoByEmailAsync(string? email = null);
        Task<ClassLibrary1Models.PowerAutomateResponse?> GetCurrentUserInfoAsync();
        Task<bool> IsServiceAvailableAsync();
        Task<string> GetCurrentUserEmailAsync();
        Task<string> GetCurrentUserNameAsync();
        Task<string> GetCurrentUserDepartmentAsync();
        Task<string> GetCurrentUserJobTitleAsync();
    }

    public class GeneralService : IGeneralService
    {
        private readonly ILogger<GeneralService> _logger;
        private readonly ClassLibrary1Services.IPowerAutomateService _nugetPowerAutomateService;
        private readonly ClassLibrary1Models.PowerAutomateSettings _powerAutomateSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private ClassLibrary1Models.PowerAutomateResponse? _cachedUserProfile;
        private string? _cachedUserEmail;
        private DateTime? _cacheTimestamp;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);

        public GeneralService(
            IOptionsSnapshot<PowerWorkflowAppSetting> powerWorkflowAppSetting,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GeneralService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            try
            {
                _nugetPowerAutomateService = new ClassLibrary1Services.PowerAutomateService();

                var powerWorkflowAppSettingValue = powerWorkflowAppSetting?.Value 
                    ?? throw new ArgumentNullException(nameof(powerWorkflowAppSetting), "PowerWorkflow settings are required");

                if (string.IsNullOrWhiteSpace(powerWorkflowAppSettingValue.GetUserProfileUrl))
                {
                    throw new InvalidOperationException("GetUserProfileUrl is required in PowerWorkflow settings");
                }

                var settings = PowerAutomateServiceExtensions.ParseFromUrl(powerWorkflowAppSettingValue.GetUserProfileUrl);
                settings.ApiKey = powerWorkflowAppSettingValue.ApiKey;
                _powerAutomateSettings = settings;

                _logger.LogInformation("GeneralService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize GeneralService");
                throw;
            }
        }

        public async Task<ClassLibrary1Models.PowerAutomateResponse?> GetUserInfoByEmailAsync(string? email = null)
        {
            try
            {
                // Use provided email or get current user's email
                var targetEmail = email ?? await GetCurrentUserEmailAsync();
                
                if (string.IsNullOrWhiteSpace(targetEmail))
                {
                    _logger.LogWarning("No email provided and unable to determine current user email");
                    return null;
                }

                // Check cache first
                if (IsCacheValid() && _cachedUserEmail == targetEmail && _cachedUserProfile is not null)
                {
                    _logger.LogDebug("Returning cached user profile for {Email}", targetEmail);
                    return _cachedUserProfile;
                }

                _logger.LogInformation("Fetching user info for email: {Email}", targetEmail);
                
                var response = await _nugetPowerAutomateService.GetUserInfoByEmailAsync(_powerAutomateSettings, targetEmail);
                
                if (response is not null)
                {
                    // Cache the result
                    _cachedUserProfile = response;
                    _cachedUserEmail = targetEmail;
                    _cacheTimestamp = DateTime.UtcNow;

                    BaseService.ManagerDisplayName = response.ManagerDisplayName;
                    BaseService.ManagerMail = response.ManagerMail;

                    _logger.LogInformation("Successfully retrieved and cached user info for {Email}", targetEmail);
                }
                else
                {
                    _logger.LogWarning("No user profile returned for email: {Email}", targetEmail);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user info for email: {Email}", email);
                return null;
            }
        }

        public async Task<ClassLibrary1Models.PowerAutomateResponse?> GetCurrentUserInfoAsync()
        {
            try
            {
                var currentEmail = await GetCurrentUserEmailAsync();
                return await GetUserInfoByEmailAsync(currentEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user info");
                return null;
            }
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                // Test with a known email or the current user's email
                var testEmail = await GetCurrentUserEmailAsync();
                if (string.IsNullOrWhiteSpace(testEmail))
                {
                    return false;
                }

                var result = await GetUserInfoByEmailAsync(testEmail);
                return result is not null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service availability check failed");
                return false;
            }
        }

        public Task<string> GetCurrentUserEmailAsync()
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    // Try different claim types for email
                    var email = user.FindFirst(ClaimTypes.Email)?.Value
                             ?? user.FindFirst("email")?.Value
                             ?? user.FindFirst("preferred_username")?.Value
                             ?? user.FindFirst(ClaimTypes.Name)?.Value;
                    
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogDebug("Retrieved current user email: {Email}", email);
                        return Task.FromResult(email);
                    }
                }

                // Fallback to BaseService if available
                if (!string.IsNullOrWhiteSpace(BaseService.Email))
                {
                    _logger.LogDebug("Using BaseService email: {Email}", BaseService.Email);
                    return Task.FromResult(BaseService.Email);
                }

                _logger.LogWarning("Unable to determine current user email");
                return Task.FromResult(string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user email");
                return Task.FromResult(string.Empty);
            }
        }

        public async Task<string> GetCurrentUserNameAsync()
        {
            try
            {
                var userProfile = await GetCurrentUserInfoAsync();
                return userProfile?.DisplayName ?? BaseService.Username ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user name");
                return string.Empty;
            }
        }

        public async Task<string> GetCurrentUserDepartmentAsync()
        {
            try
            {
                var userProfile = await GetCurrentUserInfoAsync();
                return userProfile?.Department ?? BaseService.Department ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user department");
                return string.Empty;
            }
        }

        public async Task<string> GetCurrentUserJobTitleAsync()
        {
            try
            {
                var userProfile = await GetCurrentUserInfoAsync();
                return userProfile?.JobTitle ?? BaseService.JobTitle ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user job title");
                return string.Empty;
            }
        }

        private bool IsCacheValid()
        {
            return _cacheTimestamp.HasValue && 
                   DateTime.UtcNow - _cacheTimestamp.Value < _cacheExpiry;
        }
    }
}