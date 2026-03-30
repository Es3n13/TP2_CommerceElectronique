using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Controllers
{
	public class RegisterRequest
	{
		public string Pseudo { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? PhoneNumber { get; set; }
	}

	public class LoginRequest
	{
		public string Email { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
	}

	public class AuthResponse
	{
		public string Token { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Role { get; set; } = string.Empty;
		public DateTime Expiration { get; set; }
		public User? User { get; set; }
	}

	[ApiController]
	[Route("api/auth")]
	public class AuthController : ControllerBase
	{
		private readonly UserDbContext _context;
		private readonly HttpClient _httpClient;

		public AuthController(UserDbContext context, IHttpClientFactory httpClientFactory)
		{
			_context = context;
			_httpClient = httpClientFactory.CreateClient();
		}

		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterRequest request)
		{
			if (string.IsNullOrWhiteSpace(request.Pseudo))
				return BadRequest(new { Message = "Pseudo is required." });

			if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
				return BadRequest(new { Message = "Valid email is required." });

			if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
				return BadRequest(new { Message = "Password must be at least 6 characters." });

			var existingUser = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == request.Email);

			if (existingUser != null)
				return Conflict(new { Message = $"User with email '{request.Email}' already exists." });

			var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

			var user = new User
			{
				Pseudo = request.Pseudo,
				Email = request.Email,
				FirstName = request.FirstName,
				LastName = request.LastName,
				PhoneNumber = request.PhoneNumber,
				PasswordHash = passwordHash,
				CreatedAt = DateTime.UtcNow,
				Role = "User"
			};

			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			var token = await GetTokenFromAuthService(user);

			return Ok(new AuthResponse
			{
				Token = token.Token,
				Email = user.Email,
				Name = user.Pseudo,
				Role = user.Role!,
				Expiration = token.Expiration,
				User = user
			});
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest request)
		{
			if (string.IsNullOrWhiteSpace(request.Email))
				return BadRequest(new { Message = "Email is required." });

			if (string.IsNullOrWhiteSpace(request.Password))
				return BadRequest(new { Message = "Password is required." });

			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == request.Email);

			if (user == null)
				return Unauthorized(new { Message = "Invalid email or password." });

			if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
				return Unauthorized(new { Message = "Invalid email or password." });

			var token = await GetTokenFromAuthService(user);

			return Ok(new AuthResponse
			{
				Token = token.Token,
				Email = user.Email,
				Name = user.Pseudo,
				Role = user.Role!,
				Expiration = token.Expiration,
				User = user
			});
		}

		private async Task<TokenResult> GetTokenFromAuthService(User user)
		{
			var authServiceUrl = "http://localhost:6000";
			var tokenRequest = new
			{
				UserId = user.Id,
				Email = user.Email,
				Name = user.Pseudo,
				Role = user.Role,
				FirstName = user.FirstName,
				LastName = user.LastName
			};

			var response = await _httpClient.PostAsJsonAsync($"{authServiceUrl}/api/auth/token", tokenRequest);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadFromJsonAsync<TokenResult>()!;
		}

		private class TokenResult
		{
			public string Token { get; set; } = string.Empty;
			public string Email { get; set; } = string.Empty;
			public string Name { get; set; } = string.Empty;
			public string Role { get; set; } = string.Empty;
			public DateTime Expiration { get; set; }
		}
	}
}