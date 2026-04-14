using System.Security.Claims;
using AuthService.Data;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AuthService.Services
{
    public interface IJwtRevocationValidationService
    {
        Task<bool> ValidateTokenAsync(string token, ClaimsPrincipal userClaims);
        Task<bool> IsTokenRevokedInCacheAsync(string tokenJti);
        Task<bool> IsTokenRevokedInDatabaseAsync(string tokenJti);
        Task CacheRevokedTokenAsync(string tokenJti, TimeSpan ttl);
    }

    public class JwtRevocationValidationService : IJwtRevocationValidationService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly AuthDbContext _context;
        private readonly ILogger<JwtRevocationValidationService> _logger;

        public JwtRevocationValidationService(
            IConnectionMultiplexer redis,
            AuthDbContext context,
            ILogger<JwtRevocationValidationService> logger)
        {
            _redis = redis;
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ValidateTokenAsync(string token, ClaimsPrincipal userClaims)
        {
            try
            {
                if (userClaims == null || !userClaims.HasClaim(c => c.Type == JwtRegisteredClaimNames.Jti))
                {
                    _logger.LogWarning("Token validation failed: No JTI claim found in user claims");
                    return false;
                }

                var tokenJti = userClaims.FindFirstValue(JwtRegisteredClaimNames.Jti);

                if (string.IsNullOrEmpty(tokenJti))
                {
                    _logger.LogWarning("Token validation failed: JTI claim is empty");
                    return false;
                }

                _logger.LogDebug("Validating token {TokenJti}", tokenJti);

                // Step 1: Check Redis cache first (fast path)
                var isRevokedInCache = await IsTokenRevokedInCacheAsync(tokenJti);
                if (isRevokedInCache)
                {
                    _logger.LogInformation("Token {TokenJti} is revoked (found in cache)", tokenJti);
                    return false;
                }

                // Step 2: Fallback to database check (cache miss or failure)
                var isRevokedInDb = await IsTokenRevokedInDatabaseAsync(tokenJti);
                if (isRevokedInDb)
                {
                    _logger.LogInformation("Token {TokenJti} is revoked (found in database)", tokenJti);
                    return false;
                }

                // Token is valid
                _logger.LogDebug("Token {TokenJti} is valid", tokenJti);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                // Fail closed - if we can't validate, reject the token
                return false;
            }
        }

        public async Task<bool> IsTokenRevokedInCacheAsync(string tokenJti)
        {
            try
            {
                var db = _redis.GetDatabase();
                var cacheKey = $"revoked:{tokenJti}";
                var isRevoked = await db.KeyExistsAsync(cacheKey);

                if (isRevoked)
                {
                    _logger.LogDebug("Token {TokenJti} found in revocation cache", tokenJti);
                }

                return isRevoked;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache unavailable, falling back to database for token {TokenJti}", tokenJti);
                return false; // Return false to force database fallback
            }
        }

        public async Task<bool> IsTokenRevokedInDatabaseAsync(string tokenJti)
        {
            try
            {
                if (string.IsNullOrEmpty(tokenJti))
                {
                    _logger.LogWarning("IsTokenRevokedInDatabaseAsync called with null or empty token JTI");
                    return false;
                }

                var isRevoked = await _context.RevokedAccessTokens
                    .AnyAsync(rt => rt.TokenJti == tokenJti && rt.ExpiresAt > DateTime.UtcNow);

                if (isRevoked)
                {
                    _logger.LogDebug("Token {TokenJti} found in revocation database", tokenJti);
                }

                return isRevoked;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token revocation status in database for {TokenJti}", tokenJti);
                // Return true on error as a safety measure - fail closed
                return true;
            }
        }

        public async Task CacheRevokedTokenAsync(string tokenJti, TimeSpan ttl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var cacheKey = $"revoked:{tokenJti}";

                await db.StringSetAsync(cacheKey, "true", ttl);
                _logger.LogDebug("Cached revoked token {TokenJti} with TTL {TTL}", tokenJti, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache revoked token {TokenJti} in Redis", tokenJti);
                // Non-critical - database still has the record
            }
        }
    }
}