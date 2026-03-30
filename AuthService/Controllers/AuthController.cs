using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthService.Models;
using AuthService.Services;

namespace AuthService.Controllers
{
	public class UserInfo
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? PhoneNumber { get; set; }
		public string Role { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
	}

	[ApiController]
	[Route("api/auth")]
	public class AuthController : ControllerBase
	{
		private readonly TokenService _tokenService;
		private readonly HttpClient _httpClient;

		public AuthController(TokenService tokenService, IHttpClientFactory httpClientFactory)
		{
			_tokenService = tokenService;
			_httpClient = httpClientFactory.CreateClient();
		}

		// POST /api/auth/token - Generate JWT token
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

		// GET /api/auth/me - Get current user profile (requires auth)
		[Authorize]
		[HttpGet("me")]
		public async Task<IActionResult> GetCurrentUser()
		{
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
				return Unauthorized(new { Message = "Invalid token." });

			// Call UserService to get user profile
			var userServiceUrl = "http://localhost:5140";
			var response = await _httpClient.GetAsync($"{userServiceUrl}/api/users/{userId}");

			if (!response.IsSuccessStatusCode)
				return NotFound(new { Message = "User not found or unavailable." });

			var user = await response.Content.ReadFromJsonAsync<UserInfo>();

			return Ok(user);
		}
	}

	public class ValidateTokenRequest
	{
		public string Token { get; set; } = string.Empty;
	}
}