using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Models.AppSettings;
using Microsoft.Extensions.Options;
using ClassLibrary1.Helpers;
using ClassLibrary1.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TradingLimitMVC.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RefreshTokenService> _logger;
        private readonly TradingLimitMVC.Models.AppSettings.JwtAppSetting _jwtSettings;
        private readonly IConfiguration _configuration;

        public RefreshTokenService(
            ApplicationDbContext context,
            ILogger<RefreshTokenService> logger,
            IOptions<TradingLimitMVC.Models.AppSettings.JwtAppSetting> jwtSettings,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _jwtSettings = jwtSettings.Value;
            _configuration = configuration;
        }

        public async Task<RefreshToken> GenerateRefreshTokenAsync(string username, string ipAddress, string userAgent, string? deviceId = null)
        {
            var refreshToken = new RefreshToken
            {
                Token = GenerateSecureToken(),
                Username = username,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // 7 days default
                CreatedAt = DateTime.UtcNow,
                DeviceId = deviceId ?? GenerateDeviceId(userAgent, ipAddress),
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated refresh token for user: {Username}, Device: {DeviceId}", 
                username, refreshToken.DeviceId);

            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            try
            {
                return await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving refresh token");
                return null;
            }
        }

        public async Task<bool> RevokeRefreshTokenAsync(string token, string revokedBy, string reason)
        {
            try
            {
                var refreshToken = await GetRefreshTokenAsync(token);
                if (refreshToken == null || refreshToken.IsRevoked)
                {
                    _logger.LogWarning("Attempted to revoke non-existent or already revoked token");
                    return false;
                }

                refreshToken.RevokedAt = DateTime.UtcNow;
                refreshToken.RevokedBy = revokedBy;
                refreshToken.RevokeReason = reason;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Revoked refresh token for user: {Username}, Reason: {Reason}", 
                    refreshToken.Username, reason);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token");
                return false;
            }
        }

        public async Task<int> RevokeAllUserTokensAsync(string username, string revokedBy, string reason)
        {
            try
            {
                var activeTokens = await _context.RefreshTokens
                    .Where(rt => rt.Username == username && rt.RevokedAt == null && !rt.IsExpired)
                    .ToListAsync();

                foreach (var token in activeTokens)
                {
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedBy = revokedBy;
                    token.RevokeReason = reason;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Revoked {Count} refresh tokens for user: {Username}", 
                    activeTokens.Count, username);

                return activeTokens.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all user tokens for user: {Username}", username);
                return 0;
            }
        }

        public async Task<(string? NewAccessToken, RefreshToken? NewRefreshToken)> RefreshAccessTokenAsync(
            string refreshToken, string ipAddress, string userAgent)
        {
            try
            {
                var storedToken = await GetRefreshTokenAsync(refreshToken);
                
                if (storedToken == null || !storedToken.IsActive)
                {
                    _logger.LogWarning("Invalid or inactive refresh token used for refresh");
                    return (null, null);
                }

                // Generate new access token
                var newAccessToken = await GenerateAccessTokenAsync(storedToken.Username);
                
                // Rotate refresh token for security
                var newRefreshToken = await RotateRefreshTokenAsync(refreshToken, ipAddress, userAgent, storedToken.DeviceId);

                _logger.LogInformation("Successfully refreshed tokens for user: {Username}", storedToken.Username);

                return (newAccessToken, newRefreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing access token");
                return (null, null);
            }
        }

        public async Task<RefreshToken?> RotateRefreshTokenAsync(string oldToken, string ipAddress, string userAgent, string? deviceId = null)
        {
            try
            {
                var oldRefreshToken = await GetRefreshTokenAsync(oldToken);
                if (oldRefreshToken == null || !oldRefreshToken.IsActive)
                {
                    return null;
                }

                // Create new refresh token
                var newRefreshToken = await GenerateRefreshTokenAsync(
                    oldRefreshToken.Username, ipAddress, userAgent, deviceId);

                // Revoke old token and link to new one
                oldRefreshToken.RevokedAt = DateTime.UtcNow;
                oldRefreshToken.RevokedBy = "TokenRotation";
                oldRefreshToken.RevokeReason = "Rotated for security";
                oldRefreshToken.ReplacedByToken = newRefreshToken.Token;

                await _context.SaveChangesAsync();

                return newRefreshToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating refresh token");
                return null;
            }
        }

        public async Task<int> CleanupExpiredTokensAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-30); // Keep revoked tokens for 30 days for audit

                var expiredTokens = await _context.RefreshTokens
                    .Where(rt => rt.ExpiresAt < DateTime.UtcNow || 
                                (rt.RevokedAt != null && rt.RevokedAt < cutoffDate))
                    .ToListAsync();

                _context.RefreshTokens.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} expired refresh tokens", expiredTokens.Count);

                return expiredTokens.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
                return 0;
            }
        }

        public async Task<IEnumerable<RefreshToken>> GetUserActiveTokensAsync(string username)
        {
            try
            {
                return await _context.RefreshTokens
                    .Where(rt => rt.Username == username && rt.IsActive)
                    .OrderByDescending(rt => rt.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active tokens for user: {Username}", username);
                return Enumerable.Empty<RefreshToken>();
            }
        }

        public async Task<bool> ValidateRefreshTokenAsync(string token, string username)
        {
            try
            {
                var refreshToken = await GetRefreshTokenAsync(token);
                return refreshToken != null && 
                       refreshToken.IsActive && 
                       refreshToken.Username.Equals(username, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating refresh token for user: {Username}", username);
                return false;
            }
        }

        private static string GenerateSecureToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private static string GenerateDeviceId(string userAgent, string ipAddress)
        {
            var combined = $"{userAgent}:{ipAddress}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hash)[..16]; // Take first 16 characters
        }

        private Task<string> GenerateAccessTokenAsync(string username)
        {
            try
            {
                // For now, return a simple JWT token using manual creation
                // In a real implementation, you would want to fetch user details and use proper JWT creation
                var claims = new[]
                {
                    new System.Security.Claims.Claim("username", username),
                    new System.Security.Claims.Claim("email", $"{username}@company.com"),
                    new System.Security.Claims.Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), System.Security.Claims.ClaimValueTypes.Integer64),
                    new System.Security.Claims.Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(), System.Security.Claims.ClaimValueTypes.Integer64)
                };

                var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtSettings.Key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                    issuer: _jwtSettings.Issuer,
                    audience: _jwtSettings.Audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddHours(1),
                    signingCredentials: creds);

                var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
                return Task.FromResult(tokenString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token for user: {Username}", username);
                throw;
            }
        }
    }
}