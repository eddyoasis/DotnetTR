using TradingLimitMVC.Models;

namespace TradingLimitMVC.Services
{
    public interface IRefreshTokenService
    {
        /// <summary>
        /// Generates a new refresh token for the specified user
        /// </summary>
        Task<RefreshToken> GenerateRefreshTokenAsync(string username, string ipAddress, string userAgent, string? deviceId = null);

        /// <summary>
        /// Validates and retrieves a refresh token
        /// </summary>
        Task<RefreshToken?> GetRefreshTokenAsync(string token);

        /// <summary>
        /// Revokes a refresh token
        /// </summary>
        Task<bool> RevokeRefreshTokenAsync(string token, string revokedBy, string reason);

        /// <summary>
        /// Revokes all refresh tokens for a specific user
        /// </summary>
        Task<int> RevokeAllUserTokensAsync(string username, string revokedBy, string reason);

        /// <summary>
        /// Refreshes an access token using a valid refresh token
        /// </summary>
        Task<(string? NewAccessToken, RefreshToken? NewRefreshToken)> RefreshAccessTokenAsync(string refreshToken, string ipAddress, string userAgent);

        /// <summary>
        /// Cleans up expired and revoked refresh tokens
        /// </summary>
        Task<int> CleanupExpiredTokensAsync();

        /// <summary>
        /// Gets active refresh tokens for a user (for security monitoring)
        /// </summary>
        Task<IEnumerable<RefreshToken>> GetUserActiveTokensAsync(string username);

        /// <summary>
        /// Validates if a refresh token is active and belongs to the user
        /// </summary>
        Task<bool> ValidateRefreshTokenAsync(string token, string username);

        /// <summary>
        /// Rotates a refresh token (revokes old, creates new)
        /// </summary>
        Task<RefreshToken?> RotateRefreshTokenAsync(string oldToken, string ipAddress, string userAgent, string? deviceId = null);
    }
}