using ClassLibrary1.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using TradingLimitMVC.Services;
using System.Security.Claims;

namespace TradingLimitMVC.Middlewares
{
    public class CookieAuthMiddleware(
        RequestDelegate next,
        ILogger<CookieAuthMiddleware> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        private readonly ILogger<CookieAuthMiddleware> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        
        // Load whitelist from configuration with fallback defaults
        private readonly List<string> _whiteList = configuration
            .GetSection("Authentication:WhitelistedPaths")
            .Get<List<string>>() ?? 
            ["/Login/Index", "/Test/TestPOST", "/Margin/Logout", "/Login", "/Login/Login"];

        public async Task Invoke(HttpContext context)
        {
            // Add security headers to all responses
            AddSecurityHeaders(context);
            
            // Detect AJAX requests to handle different response types
            bool isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            //If has return url from email, set it to cookie for login and redirect
            var returnUrl = context.Request.Query["ReturnUrl"].ToString();
            var hasReturnUrl = !string.IsNullOrEmpty(returnUrl);
            if (hasReturnUrl)
            {
                context.Response.Cookies.Append("WebReturnUrl", returnUrl, new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTime.UtcNow.AddMinutes(5)
                });
            }

            if (!hasReturnUrl && _whiteList.Contains(context.Request.Path.Value ?? ""))
            {
                await next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true) // Always false at this stage
            {
                if (context.Request.Cookies.TryGetValue("AuthToken", out var authToken))
                {
                    try
                    {
                        var jwtInfo = JwtTokenHelper.DecodeJwtToken(authToken);
                        
                        if (jwtInfo == null)
                        {
                            _logger.LogWarning("Failed to decode JWT token for user request from {RemoteIpAddress}", 
                                context.Connection.RemoteIpAddress);
                            await HandleUnauthenticatedRequest(context, isAjax, hasReturnUrl);
                            return;
                        }

                        _logger.LogDebug("JWT token decoded successfully for user: {Username}, Email: {Email}, JobTitle: {JobTitle}", 
                            jwtInfo.Username, jwtInfo.Email, jwtInfo.JobTitle);
                        
                        if (jwtInfo.Claims != null && _logger.IsEnabled(LogLevel.Trace))
                        {
                            _logger.LogTrace("JWT Claims: {@Claims}", 
                                jwtInfo.Claims.Select(c => new { c.Type, c.Value }).ToList());
                        }
                        if (!string.IsNullOrEmpty(jwtInfo.Username))
                        {
                            // Validate JWT token and handle expiration with automatic refresh
                            if (IsJwtTokenExpired(jwtInfo))
                            {
                                _logger.LogInformation("JWT token expired for user: {Username}, attempting refresh", jwtInfo.Username);
                                
                                // Try to refresh the token automatically
                                var refreshSuccess = await TryRefreshTokenAsync(context, jwtInfo.Username);
                                if (!refreshSuccess)
                                {
                                    _logger.LogWarning("Token refresh failed for user: {Username}", jwtInfo.Username);
                                    context.Response.Cookies.Delete("AuthToken");
                                    context.Response.Cookies.Delete("RefreshToken");
                                    await HandleUnauthenticatedRequest(context, isAjax, hasReturnUrl);
                                    return;
                                }
                                
                                // If refresh succeeded, continue with the request
                                _logger.LogInformation("Token refreshed successfully for user: {Username}", jwtInfo.Username);
                            }

                            var identity = new ClaimsIdentity(jwtInfo.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
                            var principal = new ClaimsPrincipal(identity);
                            context.User = principal;

                            // Set BaseService properties with null safety
                            BaseService.Username = jwtInfo.Username;
                            BaseService.Email = jwtInfo.Email ?? string.Empty;
                            BaseService.Department = jwtInfo.Department ?? string.Empty;
                            BaseService.JobTitle = jwtInfo.JobTitle ?? string.Empty;
                            
                            _logger.LogDebug("BaseService properties set for user: {Username}", jwtInfo.Username);
                        }
                        else
                        {
                            _logger.LogWarning("JWT token contains null or empty username");
                            await HandleUnauthenticatedRequest(context, isAjax, hasReturnUrl);
                            return;
                        }


                        if (hasReturnUrl)
                        {
                            context.Response.Cookies.Delete("WebReturnUrl");
                            var redirectUrl = $"{returnUrl}?approverRole={Uri.EscapeDataString(jwtInfo.JobTitle ?? "")}";
                            _logger.LogInformation("Redirecting authenticated user to return URL: {ReturnUrl}", returnUrl);
                            context.Response.Redirect(redirectUrl);
                            return;
                        }

                        await next(context);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing JWT token for authentication");
                        await HandleUnauthenticatedRequest(context, isAjax, hasReturnUrl);
                        return;
                    }
                }

                // No valid authentication token found
                _logger.LogInformation("No valid authentication token found for request to {Path}", context.Request.Path);
                await HandleUnauthenticatedRequest(context, isAjax, hasReturnUrl);
                return;
            }

            // User is already authenticated, continue to next middleware
            await next(context);
        }

        private async Task HandleUnauthenticatedRequest(HttpContext context, bool isAjax, bool hasReturnUrl)
        {
            if (isAjax)
            {
                context.Response.StatusCode = 401;
                context.Response.Headers["X-Auth-Required"] = "true";
                await context.Response.WriteAsync("Authentication required");
                _logger.LogDebug("Returned 401 for AJAX request to {Path}", context.Request.Path);
            }
            else
            {
                if (hasReturnUrl)
                {
                    // Allow the request to proceed to login page with return URL
                    return;
                }

                var loginUrl = "/Login/Index";
                _logger.LogDebug("Redirecting unauthenticated user from {Path} to {LoginUrl}", context.Request.Path, loginUrl);
                context.Response.Redirect(loginUrl);
            }
        }

        private static bool IsJwtTokenExpired(dynamic jwtInfo)
        {
            try
            {
                // Check if JWT has expiration claim
                if (jwtInfo?.Claims != null)
                {
                    var claims = (IEnumerable<Claim>)jwtInfo.Claims;
                    var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
                    if (expClaim?.Value != null && long.TryParse(expClaim.Value, out long exp))
                    {
                        var expirationTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                        return expirationTime <= DateTimeOffset.UtcNow;
                    }
                }
                return false; // If no expiration claim, assume not expired
            }
            catch
            {
                return true; // If error parsing, treat as expired for security
            }
        }

        private async Task<bool> TryRefreshTokenAsync(HttpContext context, string username)
        {
            try
            {
                // Check if refresh token exists in cookies
                if (!context.Request.Cookies.TryGetValue("RefreshToken", out var refreshToken) || 
                    string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogDebug("No refresh token found in cookies for user: {Username}", username);
                    return false;
                }

                // Get refresh token service from DI container
                using var scope = _serviceProvider.CreateScope();
                var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

                // Validate refresh token belongs to the user
                if (!await refreshTokenService.ValidateRefreshTokenAsync(refreshToken, username))
                {
                    _logger.LogWarning("Invalid refresh token for user: {Username}", username);
                    return false;
                }

                // Get client information
                var ipAddress = GetClientIpAddress(context);
                var userAgent = context.Request.Headers["User-Agent"].ToString();

                // Attempt to refresh the access token
                var (newAccessToken, newRefreshToken) = await refreshTokenService
                    .RefreshAccessTokenAsync(refreshToken, ipAddress, userAgent);

                if (newAccessToken == null || newRefreshToken == null)
                {
                    _logger.LogWarning("Failed to refresh tokens for user: {Username}", username);
                    return false;
                }

                // Update cookies with new tokens
                SetTokenCookies(context, newAccessToken, newRefreshToken.Token);

                _logger.LogInformation("Successfully refreshed tokens for user: {Username}", username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic token refresh for user: {Username}", username);
                return false;
            }
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            // Try to get IP from various headers (for load balancer scenarios)
            var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            }
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Connection.RemoteIpAddress?.ToString();
            }

            return ipAddress ?? "Unknown";
        }

        private static void SetTokenCookies(HttpContext context, string accessToken, string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(1) // Access token expires in 1 hour
            };

            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7) // Refresh token expires in 7 days
            };

            context.Response.Cookies.Append("AuthToken", accessToken, cookieOptions);
            context.Response.Cookies.Append("RefreshToken", refreshToken, refreshCookieOptions);
        }

        private static void AddSecurityHeaders(HttpContext context)
        {
            var response = context.Response;
            
            // Add security headers using indexer to avoid duplicate key exceptions
            if (!response.Headers.ContainsKey("X-Content-Type-Options"))
                response.Headers["X-Content-Type-Options"] = "nosniff";
                
            if (!response.Headers.ContainsKey("X-Frame-Options"))
                response.Headers["X-Frame-Options"] = "DENY";
                
            if (!response.Headers.ContainsKey("X-XSS-Protection"))
                response.Headers["X-XSS-Protection"] = "1; mode=block";
                
            if (!response.Headers.ContainsKey("Referrer-Policy"))
                response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        }
    }
}
