using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;

namespace TradingLimitMVC.Services
{
    public interface IExchangeRateService
    {
        Task<decimal> GetExchangeRateAsync(string fromCurrency);
        Task<decimal> ConvertToSGDAsync(decimal amount, string fromCurrency);
        Task<decimal> GetCFOThresholdAsync();
        Task<decimal> GetCEOThresholdAsync();
        Task<decimal> GetFixedAssetCFOThresholdAsync();
    }

    public class ExchangeRateService : IExchangeRateService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ExchangeRateService> _logger;
        private readonly IConfiguration _configuration;

        public ExchangeRateService(
            ApplicationDbContext context,
            ILogger<ExchangeRateService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<decimal> GetExchangeRateAsync(string fromCurrency)
        {
            if (fromCurrency == "SGD") return 1.0m;

            // PRIORITY 1: Database
            var key = $"ExchangeRate_{fromCurrency}_to_SGD";
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting != null)
            {
                _logger.LogInformation($" Exchange rate from DB: {fromCurrency} = {setting.Value}");
                return decimal.Parse(setting.Value);
            }

            // PRIORITY 2: appsettings.json
            var rateKey = $"{fromCurrency}_to_SGD";
            var rateFromConfig = _configuration[$"AppSettings:ExchangeRates:{rateKey}"];

            if (!string.IsNullOrEmpty(rateFromConfig) && decimal.TryParse(rateFromConfig, out var configRate))
            {
                _logger.LogInformation($" Exchange rate from appsettings.json: {fromCurrency} = {configRate}");
                return configRate;
            }

            // NO HARDCODED FALLBACK - Throw exception
            _logger.LogError($" Exchange rate not found for {fromCurrency}");
            throw new InvalidOperationException($"Exchange rate not configured for {fromCurrency}. Please add to database or appsettings.json");
        }

        public async Task<decimal> ConvertToSGDAsync(decimal amount, string fromCurrency)
        {
            var rate = await GetExchangeRateAsync(fromCurrency);
            return amount * rate;
        }

        public async Task<decimal> GetCFOThresholdAsync()
        {
            // PRIORITY 1: Database
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "CFO_Threshold_SGD");

            if (setting != null)
            {
                _logger.LogInformation($" CFO Threshold from DB: {setting.Value}");
                return decimal.Parse(setting.Value);
            }

            // PRIORITY 2: appsettings.json
            var thresholdFromConfig = _configuration["AppSettings:ApprovalThresholds:CFO_Threshold_SGD"];

            if (!string.IsNullOrEmpty(thresholdFromConfig) && decimal.TryParse(thresholdFromConfig, out var configThreshold))
            {
                _logger.LogInformation($" CFO Threshold from appsettings.json: {configThreshold}");
                return configThreshold;
            }

            // NO HARDCODED FALLBACK - Throw exception
            _logger.LogError($" CFO Threshold not configured");
            throw new InvalidOperationException("CFO_Threshold_SGD not configured. Please add to database or appsettings.json");
        }

        public async Task<decimal> GetCEOThresholdAsync()
        {
            // PRIORITY 1: Database
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "CEO_Threshold_SGD");

            if (setting != null)
            {
                _logger.LogInformation($" CEO Threshold from DB: {setting.Value}");
                return decimal.Parse(setting.Value);
            }

            // PRIORITY 2: appsettings.json
            var thresholdFromConfig = _configuration["AppSettings:ApprovalThresholds:CEO_Threshold_SGD"];

            if (!string.IsNullOrEmpty(thresholdFromConfig) && decimal.TryParse(thresholdFromConfig, out var configThreshold))
            {
                _logger.LogInformation($" CEO Threshold from appsettings.json: {configThreshold}");
                return configThreshold;
            }

            // NO HARDCODED FALLBACK - Throw exception
            _logger.LogError($" CEO Threshold not configured");
            throw new InvalidOperationException("CEO_Threshold_SGD not configured. Please add to database or appsettings.json");
        }

        public async Task<decimal> GetFixedAssetCFOThresholdAsync()
        {
            // PRIORITY 1: Database
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "FixedAsset_CFO_Threshold_SGD");

            if (setting != null)
            {
                _logger.LogInformation($" Fixed Asset CFO Threshold from DB: {setting.Value}");
                return decimal.Parse(setting.Value);
            }

            // PRIORITY 2: appsettings.json
            var thresholdFromConfig = _configuration["AppSettings:ApprovalThresholds:FixedAsset_CFO_Threshold_SGD"];

            if (!string.IsNullOrEmpty(thresholdFromConfig) && decimal.TryParse(thresholdFromConfig, out var configThreshold))
            {
                _logger.LogInformation($" Fixed Asset CFO Threshold from appsettings.json: {configThreshold}");
                return configThreshold;
            }

            // NO HARDCODED FALLBACK - Throw exception
            _logger.LogError($" Fixed Asset CFO Threshold not configured");
            throw new InvalidOperationException("FixedAsset_CFO_Threshold_SGD not configured. Please add to database or appsettings.json");
        }
    }
}
