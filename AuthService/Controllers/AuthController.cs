using System.Security.Claims;
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

		// POST /api/auth/token - Generate JWT token for a user
		[HttpPost("token")]
		public IActionResult GenerateToken([FromBody] TokenRequest request)
		{
			var token = _tokenService.GenerateToken(request);
			return Ok(token);
		}

		// POST /api/auth/validate - Validate JWT token
		[HttpPost("validate")]
		public IActionResult ValidateToken([FromBody] ValidateTokenRequest request)
		{
			var claims = _tokenService.ValidateToken(request.Token);

			if (claims == null)
				return Unauthorized(new { Message = "Invalid token." });

			return Ok(new
			{
				IsValid = true,
				UserId = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value,
				Email = claims.FindFirst(ClaimIdentifiers.ClaimTypes.Email)?.Value,
				Name = claims.FindFirst(ClaimTypes.Name)?.Value,
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