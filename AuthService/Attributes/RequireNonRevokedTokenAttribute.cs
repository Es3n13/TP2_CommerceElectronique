using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using AuthService.Services;

namespace AuthService.Attributes
{
    /// <summary>
    /// Attribute that can be applied to controllers or actions to explicitly enforce
    /// JWT token revocation checking. This is useful for sensitive endpoints that
    /// require stricter validation, or for testing purposes.
    ///
    /// Note: Automatic revocation checking is already enabled via JWT bearer events
    /// in Program.cs. This attribute provides an additional layer of control and
    /// can be used for opt-in scenarios.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireNonRevokedTokenAttribute : Attribute, IAuthorizationFilter, IAsyncAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Synchronous implementation for backward compatibility
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Defer to async implementation
            OnAuthorizationAsync(context).GetAwaiter().GetResult();
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Get required services from DI
            var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<RequireNonRevokedTokenAttribute>();

            try
            {
                // Check if user is authenticated
                var user = context.HttpContext.User;
                if (user == null || !user.Identity?.IsAuthenticated == true)
                {
                    logger.LogWarning("Authorization failed: User is not authenticated");
                    context.Result = new UnauthorizedResult();
                    return;
                }

                // Get revocation validation service
                var revocationValidationService = context.HttpContext.RequestServices
                    .GetRequiredService<IJwtRevocationValidationService>();

                // Extract token from Authorization header
                var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    logger.LogWarning("Authorization failed: Invalid or missing Authorization header");
                    context.Result = new UnauthorizedResult();
                    return;
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();

                // Validate token against revocation list
                var isValid = await revocationValidationService.ValidateTokenAsync(token, user);

                if (!isValid)
                {
                    logger.LogWarning("Authorization failed: Token has been revoked");
                    context.Result = new UnauthorizedObjectResult(new
                    {
                        error = "invalid_token",
                        error_description = "The provided access token has been revoked"
                    });
                    return;
                }

                logger.LogDebug("Token revocation check passed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during token revocation authorization check");
                // Fail closed - if we can't validate, reject the request
                context.Result = new StatusCodeResult(500);
            }
        }
    }
}