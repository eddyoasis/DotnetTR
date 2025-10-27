using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;

namespace TradingLimitMVC.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin/ExchangeRates
        public async Task<IActionResult> ExchangeRates()
        {
            var settings = await _context.SystemSettings
                .Where(s => s.Category == "ExchangeRate")
                .OrderBy(s => s.Key)
                .ToListAsync();

            // Initialize default rates if empty
            if (!settings.Any())
            {
                await InitializeDefaultExchangeRates();
                settings = await _context.SystemSettings
                    .Where(s => s.Category == "ExchangeRate")
                    .OrderBy(s => s.Key)
                    .ToListAsync();
            }

            return View(settings);
        }

        // POST: Admin/UpdateExchangeRate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateExchangeRate(string currency, decimal rate, string description)
        {
            try
            {
                if (rate <= 0)
                {
                    TempData["Error"] = "Exchange rate must be greater than 0";
                    return RedirectToAction(nameof(ExchangeRates));
                }

                var key = $"ExchangeRate_{currency}_to_SGD";
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == key);

                if (setting == null)
                {
                    setting = new SystemSetting
                    {
                        Category = "ExchangeRate",
                        Key = key,
                        Value = rate.ToString("F4"),
                        Description = description ?? $"{currency} to SGD exchange rate",
                        UpdatedDate = DateTime.Now,
                        UpdatedBy = GetCurrentUser()
                    };
                    _context.Add(setting);
                }
                else
                {
                    setting.Value = rate.ToString("F4");
                    setting.Description = description ?? setting.Description;
                    setting.UpdatedDate = DateTime.Now;
                    setting.UpdatedBy = GetCurrentUser();
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Exchange rate updated: {currency} to SGD = {rate} by {GetCurrentUser()}");
                TempData["Success"] = $"{currency} to SGD rate updated to {rate}";

                return RedirectToAction(nameof(ExchangeRates));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating exchange rate");
                TempData["Error"] = "Error updating exchange rate: " + ex.Message;
                return RedirectToAction(nameof(ExchangeRates));
            }
        }

        // GET: Admin/ApprovalThresholds
        public async Task<IActionResult> ApprovalThresholds()
        {
            var thresholds = await _context.SystemSettings
                .Where(s => s.Category == "ApprovalThreshold")
                .OrderBy(s => s.Key)
                .ToListAsync();

            if (!thresholds.Any())
            {
                await InitializeDefaultThresholds();
                thresholds = await _context.SystemSettings
                    .Where(s => s.Category == "ApprovalThreshold")
                    .OrderBy(s => s.Key)
                    .ToListAsync();
            }

            return View(thresholds);
        }

        // POST: Admin/UpdateThreshold
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateThreshold(string key, decimal value, string description)
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == key);

                if (setting == null)
                {
                    setting = new SystemSetting
                    {
                        Category = "ApprovalThreshold",
                        Key = key,
                        Value = value.ToString("F2"),
                        Description = description,
                        UpdatedDate = DateTime.Now,
                        UpdatedBy = GetCurrentUser()
                    };
                    _context.Add(setting);
                }
                else
                {
                    setting.Value = value.ToString("F2");
                    setting.Description = description ?? setting.Description;
                    setting.UpdatedDate = DateTime.Now;
                    setting.UpdatedBy = GetCurrentUser();
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Threshold {key} updated to SGD {value}";

                return RedirectToAction(nameof(ApprovalThresholds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating threshold");
                TempData["Error"] = "Error updating threshold: " + ex.Message;
                return RedirectToAction(nameof(ApprovalThresholds));
            }
        }

        private async Task InitializeDefaultExchangeRates()
        {
            var defaults = new[]
            {
                new SystemSetting { Category = "ExchangeRate", Key = "ExchangeRate_SGD_to_SGD", Value = "1.0000", Description = "SGD base currency" },
                new SystemSetting { Category = "ExchangeRate", Key = "ExchangeRate_USD_to_SGD", Value = "1.3500", Description = "USD to SGD rate" },
                new SystemSetting { Category = "ExchangeRate", Key = "ExchangeRate_NTD_to_SGD", Value = "0.0430", Description = "Taiwan Dollar to SGD rate" },
                new SystemSetting { Category = "ExchangeRate", Key = "ExchangeRate_MYR_to_SGD", Value = "0.3000", Description = "Malaysian Ringgit to SGD rate" }
            };

            _context.SystemSettings.AddRange(defaults);
            await _context.SaveChangesAsync();
        }

        private async Task InitializeDefaultThresholds()
        {
            var defaults = new[]
            {
                new SystemSetting { Category = "ApprovalThreshold", Key = "CFO_Threshold_SGD", Value = "430.00", Description = "CFO approval required above this amount (10,000 NTD equiv)" },
                new SystemSetting { Category = "ApprovalThreshold", Key = "CEO_Threshold_SGD", Value = "43000.00", Description = "CEO approval required above this amount (1,000,000 NTD equiv)" }
            };

            _context.SystemSettings.AddRange(defaults);
            await _context.SaveChangesAsync();
        }

        private string GetCurrentUser()
        {
            return "Admin User"; // Replace with actual authentication
        }
    }
}
