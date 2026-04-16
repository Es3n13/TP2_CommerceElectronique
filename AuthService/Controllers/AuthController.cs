using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using AuthService.Models;
using AuthService.Services;

namespace AuthService.Controllers
{
	[ApiController]
	[Route("api/auth")]
	public class AuthController : ControllerBase
	{
		private readonly TokenService _tokenService;

		public AuthController(TokenService tokenService)
		{
			_tokenService = tokenService;
		}

        // POST /api/auth/token - Générer un JWT token
        [HttpPost("token")]
		public IActionResult GenerateToken([FromBody] TokenRequest request)
		{
			var token = _tokenService.GenerateToken(request);
			return Ok(token);
		}

        // POST /api/auth/validate - Valider un JWT token
        [HttpPost("validate")]
		public IActionResult ValidateToken([FromBody] ValidateTokenRequest request)
		{
			var claims = _tokenService.ValidateToken(request.Token);

			if (claims == null)
				return Unauthorized(new { Message = "Token invalide." });

			return Ok(new
			{
				IsValid = true,
				UserId = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value,
				Email = claims.FindFirst(ClaimTypes.Email)?.Value,
				Pseudo = claims.FindFirst(ClaimTypes.Name)?.Value,
				Role = claims.FindFirst(ClaimTypes.Role)?.Value,
				Expiration = claims.FindFirst(JwtRegisteredClaimNames.Exp)?.Value
			});
		}
	}

	public class ValidateTokenRequest
	{
		public string Token { get; set; } = string.Empty;
	}
}