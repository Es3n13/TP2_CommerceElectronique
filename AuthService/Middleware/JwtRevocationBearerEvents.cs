using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using AuthService.Services;

namespace AuthService.Middleware
{
    public class JwtRevocationBearerEvents : JwtBearerEvents
    {
        private readonly IJwtRevocationValidationService _revocationValidationService;
        private readonly ILogger<JwtRevocationBearerEvents> _logger;

        public JwtRevocationBearerEvents(
            IJwtRevocationValidationService revocationValidationService,
            ILogger<JwtRevocationBearerEvents> logger)
        {
            _revocationValidationService = revocationValidationService;
            _logger = logger;
        }

        public override async Task TokenValidated(TokenValidatedContext context)
        {
            var token = context.SecurityToken as JwtSecurityToken;
            if (token == null)
            {
                _logger.LogWarning("Token validation failed: Security token is not a JWT");
                context.Fail("Invalid token format");
                return;
            }

            var userClaims = context.Principal;

            // Validate token against revocation list
            var isValid = await _revocationValidationService.ValidateTokenAsync(
                token.RawData,
                userClaims!
            );

            if (!isValid)
            {
                _logger.LogWarning(
                    "Token validation failed: Token {TokenId} has been revoked or is invalid",
                    token.Id
                );
                context.Fail("Token has been revoked");
                return;
            }

            _logger.LogDebug("Token {TokenId} validated successfully", token.Id);

            await base.TokenValidated(context);
        }

        public override Task AuthenticationFailed(AuthenticationFailedContext context)
        {
            _logger.LogError(
                context.Exception,
                "JWT authentication failed: {Message}",
                context.Exception.Message
            );
            return base.AuthenticationFailed(context);
        }

        public override Task Challenge(JwtBearerChallengeContext context)
        {
            _logger.LogDebug("JWT authentication challenge triggered");
            return base.Challenge(context);
        }
    }
}