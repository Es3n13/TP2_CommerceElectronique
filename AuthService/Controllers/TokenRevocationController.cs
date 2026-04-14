using Microsoft.AspNetCore.Mvc;
using AuthService.Services;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TokenRevocationController : ControllerBase
    {
        private readonly IRevokedAccessTokenService _revokedTokenService;
        private readonly ILogger<TokenRevocationController> _logger;

        public TokenRevocationController(
            IRevokedAccessTokenService revokedTokenService,
            ILogger<TokenRevocationController> logger)
        {
            _revokedTokenService = revokedTokenService;
            _logger = logger;
        }

        /// <summary>
        /// Revoke a specific access token by JTI
        /// </summary>
        [HttpPost("revoke")]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TokenJti))
                {
                    return BadRequest(new { Message = "Token JTI is required." });
                }

                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest(new { Message = "User ID is required." });
                }

                await _revokedTokenService.RevokedTokenAsync(request.TokenJti, request.UserId, request.Reason);

                return Ok(new { Message = "Token revoked successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return StatusCode(500, new { Message = "An error occurred while revoking the token." });
            }
        }

        /// <summary>
        /// Check if a token is revoked
        /// </summary>
        [HttpGet("check/{tokenJti}")]
        public async Task<IActionResult> CheckTokenRevoked(string tokenJti)
        {
            try
            {
                if (string.IsNullOrEmpty(tokenJti))
                {
                    return BadRequest(new { Message = "Token JTI is required." });
                }

                var isRevoked = await _revokedTokenService.IsTokenRevokedAsync(tokenJti);

                return Ok(new { IsRevoked = isRevoked });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token revocation status");
                return StatusCode(500, new { Message = "An error occurred while checking token status." });
            }
        }

        /// <summary>
        /// Revoke all tokens for a user
        /// </summary>
        [HttpPost("revoke-all/{userId}")]
        public async Task<IActionResult> RevokeAllUserTokens(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { Message = "User ID is required." });
                }

                await _revokedTokenService.RevokeAllUserTokensAsync(userId);

                return Ok(new { Message = "All tokens revoked successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all user tokens");
                return StatusCode(500, new { Message = "An error occurred while revoking user tokens." });
            }
        }

        /// <summary>
        /// Cleanup expired revoked tokens (maintenance endpoint)
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupExpiredTokens()
        {
            try
            {
                await _revokedTokenService.CleanupExpiredTokensAsync();

                return Ok(new { Message = "Expired tokens cleaned up successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
                return StatusCode(500, new { Message = "An error occurred while cleaning up expired tokens." });
            }
        }
    }

    public class RevokeTokenRequest
    {
        public string TokenJti { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}