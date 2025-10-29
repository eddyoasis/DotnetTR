# JWT Token Refresh Implementation Guide

## 🚀 **Overview**

This document describes the comprehensive JWT token refresh mechanism implemented for the Trading Limit System, providing automatic token renewal, enhanced security, and seamless user experience.

## ✅ **Features Implemented**

### 1. **Automatic Token Refresh**
- **Middleware Integration**: CookieAuthMiddleware automatically detects expired tokens
- **Seamless Renewal**: Users experience no interruption during token refresh
- **Background Processing**: Token refresh happens transparently

### 2. **Secure Refresh Token Management**
- **Database Storage**: Refresh tokens stored securely in database
- **Token Rotation**: Old refresh tokens are automatically revoked when new ones are issued
- **Device Tracking**: Each refresh token is associated with device/IP for security monitoring

### 3. **Comprehensive API Endpoints**
- **Token Refresh**: `/api/token/refresh` - Manual token refresh
- **Token Revocation**: `/api/token/revoke` - Revoke specific tokens
- **Bulk Revocation**: `/api/token/revoke-all` - Revoke all user tokens
- **Session Management**: `/api/token/sessions/{username}` - View active sessions

## 🏗️ **Architecture Components**

### **1. RefreshToken Model**
```csharp
public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevokeReason { get; set; }
    public string? ReplacedByToken { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    
    // Computed properties
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
```

### **2. IRefreshTokenService Interface**
```csharp
public interface IRefreshTokenService
{
    Task<RefreshToken> GenerateRefreshTokenAsync(string username, string ipAddress, string userAgent, string? deviceId = null);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task<bool> RevokeRefreshTokenAsync(string token, string revokedBy, string reason);
    Task<int> RevokeAllUserTokensAsync(string username, string revokedBy, string reason);
    Task<(string? NewAccessToken, RefreshToken? NewRefreshToken)> RefreshAccessTokenAsync(string refreshToken, string ipAddress, string userAgent);
    Task<int> CleanupExpiredTokensAsync();
    Task<IEnumerable<RefreshToken>> GetUserActiveTokensAsync(string username);
    Task<bool> ValidateRefreshTokenAsync(string token, string username);
    Task<RefreshToken?> RotateRefreshTokenAsync(string oldToken, string ipAddress, string userAgent, string? deviceId = null);
}
```

### **3. Enhanced CookieAuthMiddleware**
```csharp
// Automatic token refresh on expiration
if (IsJwtTokenExpired(jwtInfo))
{
    _logger.LogInformation("JWT token expired for user: {Username}, attempting refresh", jwtInfo.Username);
    
    var refreshSuccess = await TryRefreshTokenAsync(context, jwtInfo.Username);
    if (!refreshSuccess)
    {
        // Handle authentication failure
        await HandleUnauthenticatedRequest(context, isAjax, hasReturnUrl);
        return;
    }
    
    _logger.LogInformation("Token refreshed successfully for user: {Username}", jwtInfo.Username);
}
```

## 🔧 **Implementation Details**

### **Database Schema**
```sql
CREATE TABLE RefreshTokens (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Token NVARCHAR(500) NOT NULL UNIQUE,
    Username NVARCHAR(255) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RevokedAt DATETIME2 NULL,
    RevokedBy NVARCHAR(500) NULL,
    RevokeReason NVARCHAR(1000) NULL,
    ReplacedByToken NVARCHAR(500) NULL,
    DeviceId NVARCHAR(50) NOT NULL,
    IpAddress NVARCHAR(255) NOT NULL,
    UserAgent NVARCHAR(500) NOT NULL
);

-- Indexes for performance
CREATE INDEX IX_RefreshTokens_Token ON RefreshTokens (Token);
CREATE INDEX IX_RefreshTokens_Username ON RefreshTokens (Username);
CREATE INDEX IX_RefreshTokens_ExpiresAt ON RefreshTokens (ExpiresAt);
CREATE INDEX IX_RefreshTokens_Username_DeviceId ON RefreshTokens (Username, DeviceId);
```

### **Service Registration (Program.cs)**
```csharp
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
```

### **Token Lifecycle**
1. **Login**: Generate both access and refresh tokens
2. **Request**: Use access token for authentication
3. **Expiration**: Middleware detects expired access token
4. **Refresh**: Automatically use refresh token to get new access token
5. **Rotation**: Old refresh token is revoked, new one issued
6. **Cleanup**: Expired tokens removed periodically

## 🛡️ **Security Features**

### **1. Token Security**
- **Cryptographically Secure**: Uses `RandomNumberGenerator` for token generation
- **64-byte Tokens**: Long, unguessable token values
- **Base64 Encoding**: Safe for HTTP transmission

### **2. Device Tracking**
```csharp
private static string GenerateDeviceId(string userAgent, string ipAddress)
{
    var combined = $"{userAgent}:{ipAddress}";
    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
    return Convert.ToBase64String(hash)[..16];
}
```

### **3. Token Rotation**
- **Automatic Rotation**: Each refresh generates new tokens
- **Audit Trail**: Old tokens linked to new ones via `ReplacedByToken`
- **Revocation Tracking**: Full audit of when and why tokens were revoked

### **4. IP and User Agent Validation**
- **Device Binding**: Tokens associated with specific devices
- **Anomaly Detection**: Track login patterns for security monitoring
- **Session Management**: Users can view and manage active sessions

## 🔄 **API Usage Examples**

### **1. Manual Token Refresh**
```javascript
// Request
POST /api/token/refresh
Content-Type: application/json

{
    "refreshToken": "base64-encoded-refresh-token"
}

// Response
{
    "accessToken": "jwt-access-token",
    "refreshToken": "new-refresh-token",
    "expiresAt": "2025-10-30T10:30:00Z",
    "tokenType": "Bearer"
}
```

### **2. Revoke Token**
```javascript
POST /api/token/revoke
{
    "refreshToken": "token-to-revoke"
}
```

### **3. View Active Sessions**
```javascript
GET /api/token/sessions/username

// Response
[
    {
        "deviceId": "ABC123",
        "ipAddress": "192.168.1.100",
        "userAgent": "Mozilla/5.0...",
        "createdAt": "2025-10-29T10:00:00Z",
        "expiresAt": "2025-11-05T10:00:00Z",
        "isCurrentSession": true
    }
]
```

## 🔧 **Configuration**

### **JWT Settings (appsettings.json)**
```json
{
    "JwtAppSettings": {
        "Key": "your-secret-key-32-characters-long",
        "Issuer": "TradingLimitSystem",
        "Audience": "TradingLimitUsers"
    },
    "Authentication": {
        "RefreshTokenExpirationDays": 7,
        "AccessTokenExpirationHours": 1,
        "MaxActiveTokensPerUser": 5,
        "CleanupIntervalHours": 24
    }
}
```

## 📊 **Monitoring and Cleanup**

### **Automatic Cleanup Service**
```csharp
// Cleanup expired tokens (run as background service)
public async Task<int> CleanupExpiredTokensAsync()
{
    var cutoffDate = DateTime.UtcNow.AddDays(-30);
    var expiredTokens = await _context.RefreshTokens
        .Where(rt => rt.ExpiresAt < DateTime.UtcNow || 
                    (rt.RevokedAt != null && rt.RevokedAt < cutoffDate))
        .ToListAsync();
    
    _context.RefreshTokens.RemoveRange(expiredTokens);
    await _context.SaveChangesAsync();
    return expiredTokens.Count;
}
```

### **Security Monitoring**
- **Failed Refresh Attempts**: Log suspicious refresh attempts
- **Multiple Device Detection**: Alert on unusual device patterns
- **Token Theft Detection**: Monitor for rapid token rotation

## 🚨 **Error Handling**

### **Common Scenarios**
1. **Expired Refresh Token**: Return 401, require re-login
2. **Invalid Refresh Token**: Return 401, clear cookies
3. **Token Theft Detected**: Revoke all user tokens
4. **Database Errors**: Graceful degradation, log errors

### **Logging Examples**
```csharp
_logger.LogInformation("Generated refresh token for user: {Username}, Device: {DeviceId}", 
    username, refreshToken.DeviceId);

_logger.LogWarning("Invalid or inactive refresh token used for refresh");

_logger.LogError(ex, "Error during automatic token refresh for user: {Username}", username);
```

## 🔄 **Migration and Deployment**

### **Database Migration**
```bash
# Create migration
dotnet ef migrations add AddRefreshTokenSupport

# Apply migration
dotnet ef database update
```

### **Deployment Checklist**
1. ✅ Database migration applied
2. ✅ JWT configuration updated
3. ✅ Refresh token service registered
4. ✅ Middleware updated
5. ✅ API endpoints available
6. ✅ Logging configured
7. ✅ Cleanup service scheduled

## 📈 **Performance Considerations**

### **Database Optimization**
- **Indexes**: Critical indexes on Token, Username, ExpiresAt
- **Cleanup**: Regular cleanup prevents table bloat
- **Partitioning**: Consider partitioning for high-volume scenarios

### **Caching Strategy**
```csharp
// Consider caching frequently accessed refresh tokens
public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
{
    // Check cache first
    var cached = await _cache.GetAsync($"refresh_token:{token}");
    if (cached != null) return cached;
    
    // Fallback to database
    var token = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);
    
    // Cache for short period
    await _cache.SetAsync($"refresh_token:{token}", token, TimeSpan.FromMinutes(5));
    
    return token;
}
```

## 🎯 **Best Practices**

### **Security**
1. **Short-lived Access Tokens**: 1-hour expiration recommended
2. **Longer Refresh Tokens**: 7-day expiration typical
3. **Token Rotation**: Always rotate refresh tokens on use
4. **Secure Storage**: HTTPOnly, Secure cookies for web apps
5. **IP Validation**: Consider IP binding for sensitive applications

### **User Experience**
1. **Seamless Refresh**: Users should not notice token expiration
2. **Clear Logout**: Properly revoke tokens on explicit logout
3. **Session Management**: Allow users to view/revoke active sessions
4. **Error Messaging**: Clear messages for authentication failures

### **Monitoring**
1. **Token Metrics**: Track token generation, usage, expiration
2. **Security Alerts**: Monitor for suspicious patterns
3. **Performance**: Track refresh operation latency
4. **Cleanup**: Monitor cleanup effectiveness

## 🏆 **Benefits Achieved**

1. **Enhanced Security**: Token rotation, device tracking, audit trails
2. **Better UX**: Seamless authentication without interruptions
3. **Scalability**: Efficient database design and cleanup
4. **Compliance**: Proper session management and audit logging
5. **Flexibility**: Multiple revocation strategies and session control
6. **Monitoring**: Comprehensive logging and security alerts

The JWT refresh token implementation provides enterprise-grade authentication with automatic renewal, comprehensive security features, and excellent user experience for your Trading Limit System! 🚀