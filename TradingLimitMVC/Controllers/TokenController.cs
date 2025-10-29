using Microsoft.AspNetCore.Mvc;
using TradingLimitMVC.Services;
using TradingLimitMVC.Models.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace TradingLimitMVC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(
            IRefreshTokenService refreshTokenService,
            ILogger<TokenController> logger)
        {
            _refreshTokenService = refreshTokenService;
            _logger = logger;
        }

        /// <summary>
        /// Refreshes an access token using a valid refresh token
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var ipAddress = GetClientIpAddress();
                var userAgent = Request.Headers["User-Agent"].ToString();

                var (newAccessToken, newRefreshToken) = await _refreshTokenService
                    .RefreshAccessTokenAsync(request.RefreshToken, ipAddress, userAgent);

                if (newAccessToken == null || newRefreshToken == null)
                {
                    _logger.LogWarning("Token refresh failed for request from IP: {IpAddress}", ipAddress);
                    return Unauthorized(new { message = "Invalid or expired refresh token" });
                }

                var response = new RefreshTokenResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken.Token,
                    ExpiresAt = newRefreshToken.ExpiresAt,
                    TokenType = "Bearer"
                };

                // Set new tokens as secure cookies
                SetTokenCookies(newAccessToken, newRefreshToken.Token);

                _logger.LogInformation("Token refreshed successfully for user: {Username}", newRefreshToken.Username);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new { message = "An error occurred during token refresh" });
            }
        }

        /// <summary>
        /// Revokes a refresh token
        /// </summary>
        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _refreshTokenService.RevokeRefreshTokenAsync(
                    request.RefreshToken, 
                    "UserRevocation", 
                    "Token revoked by user request");

                if (success)
                {
                    // Clear cookies
                    Response.Cookies.Delete("AuthToken");
                    Response.Cookies.Delete("RefreshToken");

                    return Ok(new { message = "Token revoked successfully" });
                }

                return BadRequest(new { message = "Failed to revoke token" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token revocation");
                return StatusCode(500, new { message = "An error occurred during token revocation" });
            }
        }

        /// <summary>
        /// Revokes all refresh tokens for the current user
        /// </summary>
        [HttpPost("revoke-all")]
        public async Task<IActionResult> RevokeAllTokens([FromBody] RevokeAllTokensRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var revokedCount = await _refreshTokenService.RevokeAllUserTokensAsync(
                    request.Username, 
                    "UserRevocation", 
                    "All tokens revoked by user request");

                // Clear cookies
                Response.Cookies.Delete("AuthToken");
                Response.Cookies.Delete("RefreshToken");

                return Ok(new { message = $"Revoked {revokedCount} tokens successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during all tokens revocation for user: {Username}", request.Username);
                return StatusCode(500, new { message = "An error occurred during token revocation" });
            }
        }

        /// <summary>
        /// Gets active sessions (refresh tokens) for security monitoring
        /// </summary>
        [HttpGet("sessions/{username}")]
        public async Task<IActionResult> GetActiveSessions(string username)
        {
            try
            {
                var activeTokens = await _refreshTokenService.GetUserActiveTokensAsync(username);
                
                var sessions = activeTokens.Select(token => new SessionInfo
                {
                    DeviceId = token.DeviceId,
                    IpAddress = token.IpAddress,
                    UserAgent = token.UserAgent,
                    CreatedAt = token.CreatedAt,
                    ExpiresAt = token.ExpiresAt,
                    IsCurrentSession = Request.Cookies["RefreshToken"] == token.Token
                });

                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active sessions for user: {Username}", username);
                return StatusCode(500, new { message = "An error occurred retrieving sessions" });
            }
        }

        private string GetClientIpAddress()
        {
            // Try to get IP from various headers (for load balancer scenarios)
            var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = Request.Headers["X-Real-IP"].FirstOrDefault();
            }
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            return ipAddress ?? "Unknown";
        }

        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(1) // Access token expires in 1 hour
            };

            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7) // Refresh token expires in 7 days
            };

            Response.Cookies.Append("AuthToken", accessToken, cookieOptions);
            Response.Cookies.Append("RefreshToken", refreshToken, refreshCookieOptions);
        }
    }

    // Request/Response DTOs
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }

    public class RevokeTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RevokeAllTokensRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;
    }

    public class SessionInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsCurrentSession { get; set; }
    }
}