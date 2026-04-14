using AuthService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AuthService.Services
{
    public interface IRevokedAccessTokenService
    {
        Task RevokedTokenAsync(string tokenJti, string userId, string? reason = null);
        Task<bool> IsTokenRevokedAsync(string tokenJti);
        Task RevokeAllUserTokensAsync(string userId);
        Task CleanupExpiredTokensAsync();
    }

    public class RevokedAccessTokenService : IRevokedAccessTokenService
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<RevokedAccessTokenService> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IJwtRevocationValidationService _revocationValidationService;

        public RevokedAccessTokenService(
            AuthDbContext context,
            ILogger<RevokedAccessTokenService> logger,
            IConnectionMultiplexer redis,
            IJwtRevocationValidationService revocationValidationService)
        {
            _context = context;
            _logger = logger;
            _redis = redis;
            _revocationValidationService = revocationValidationService;
        }

        public async Task RevokedTokenAsync(string tokenJti, string userId, string? reason = null)
        {
            try
            {
                if (string.IsNullOrEmpty(tokenJti))
                {
                    throw new ArgumentException("Token JTI cannot be null or empty", nameof(tokenJti));
                }

                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                }

                _logger.LogInformation("Revoking token {TokenJti} for user {UserId}. Reason: {Reason}", tokenJti, userId, reason ?? "Not specified");

                var expiresAt = DateTime.UtcNow.AddHours(1); // Access tokens typically expire in 1 hour

                var revokedToken = new RevokedAccessToken
                {
                    Id = Guid.NewGuid(),
                    TokenJti = tokenJti,
                    UserId = userId,
                    Reason = reason,
                    RevokedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };

                _context.RevokedAccessTokens.Add(revokedToken);
                await _context.SaveChangesAsync();

                // Cache the revoked token in Redis for fast lookups
                var ttl = expiresAt - DateTime.UtcNow;
                if (ttl > TimeSpan.Zero)
                {
                    await _revocationValidationService.CacheRevokedTokenAsync(tokenJti, ttl);
                }

                _logger.LogInformation("Successfully revoked token {TokenJti}", tokenJti);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token {TokenJti} for user {UserId}", tokenJti, userId);
                throw;
            }
        }

        public async Task<bool> IsTokenRevokedAsync(string tokenJti)
        {
            try
            {
                if (string.IsNullOrEmpty(tokenJti))
                {
                    _logger.LogWarning("IsTokenRevokedAsync called with null or empty token JTI");
                    return false;
                }

                var isRevoked = await _context.RevokedAccessTokens
                    .AnyAsync(rt => rt.TokenJti == tokenJti && rt.ExpiresAt > DateTime.UtcNow);

                if (isRevoked)
                {
                    _logger.LogInformation("Token {TokenJti} is revoked", tokenJti);
                }

                return isRevoked;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if token {TokenJti} is revoked", tokenJti);
                // Return true on error as a safety measure - fail closed
                return true;
            }
        }

        public async Task RevokeAllUserTokensAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
                }

                _logger.LogInformation("Revoking all tokens for user {UserId}", userId);

                // Get all active refresh tokens for this user
                var refreshTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                // Mark all refresh tokens as revoked
                foreach (var rt in refreshTokens)
                {
                    rt.RevokedAt = DateTime.UtcNow;

                    var revokedAccessToken = new RevokedAccessToken
                    {
                        Id = Guid.NewGuid(),
                        TokenJti = rt.JwtId,
                        UserId = userId,
                        Reason = "All tokens revoked",
                        RevokedAt = DateTime.UtcNow,
                        ExpiresAt = rt.ExpiresAt
                    };

                    _context.RevokedAccessTokens.Add(revokedAccessToken);

                    // Cache the revoked token in Redis
                    var ttl = rt.ExpiresAt - DateTime.UtcNow;
                    if (ttl > TimeSpan.Zero && !string.IsNullOrEmpty(rt.JwtId))
                    {
                        await _revocationValidationService.CacheRevokedTokenAsync(rt.JwtId, ttl);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully revoked {Count} tokens for user {UserId}", refreshTokens.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user {UserId}", userId);
                throw;
            }
        }

        public async Task CleanupExpiredTokensAsync()
        {
            try
            {
                _logger.LogInformation("Cleaning up expired revoked tokens");

                var expiredTokens = await _context.RevokedAccessTokens
                    .Where(rt => rt.ExpiresAt <= DateTime.UtcNow)
                    .ToListAsync();

                if (expiredTokens.Any())
                {
                    _context.RevokedAccessTokens.RemoveRange(expiredTokens);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {Count} expired revoked tokens", expiredTokens.Count);
                }
                else
                {
                    _logger.LogInformation("No expired revoked tokens to clean up");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired revoked tokens");
                throw;
            }
        }
    }
}