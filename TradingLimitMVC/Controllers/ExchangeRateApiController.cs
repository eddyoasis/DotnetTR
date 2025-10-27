using Microsoft.AspNetCore.Mvc;
using TradingLimitMVC.Services;

namespace TradingLimitMVC.Controllers
{
    [Route("api/exchangerate")]
    [ApiController]
    public class ExchangeRateApiController : ControllerBase
    {
        private readonly IExchangeRateService _exchangeRateService;

        public ExchangeRateApiController(IExchangeRateService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        [HttpGet("{currency}")]
        public async Task<IActionResult> GetRate(string currency)
        {
            try
            {
                var rate = await _exchangeRateService.GetExchangeRateAsync(currency);
                return Ok(new { currency, rate });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
