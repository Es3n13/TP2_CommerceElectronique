using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using userservice.Data;
using userservice.Models;
using userservice.Services;

namespace userservice.Controllers
{
	public class RegisterRequest
	{
		public string Name { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
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
	}

	[ApiController]
	[Route("api/auth")]
	public class AuthController : ControllerBase
	{
		private readonly UserDbContext _context;
		private readonly AuthService _authService;

		public AuthController(UserDbContext context, AuthService authService)
		{
			_context = context;
			_authService = authService;
		}

		// POST /api/auth/register
		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterRequest request)
		{
			// Validation
			if (string.IsNullOrWhiteSpace(request.Name))
				return BadRequest(new { Message = "Name is required." });

			if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
				return BadRequest(new { Message = "Valid email is required." });

			if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
				return BadRequest(new { Message = "Password must be at least 6 characters." });

			// Check if email already exists
			var existingUser = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == request.Email);

			if (existingUser != null)
				return Conflict(new { Message = $"User with email '{request.Email}' already exists." });

			// Hash password
			var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

			// Create user
			var user = new User
			{
				Name = request.Name,
				Email = request.Email,
				FirstName = request.FirstName,
				LastName = request.LastName,
				PasswordHash = passwordHash,
				CreatedAt = DateTime.UtcNow,
				Role = "User"
			};

			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			// Generate token
			var token = _authService.GenerateToken(user);

			return Ok(new AuthResponse
			{
				Token = token,
				Email = user.Email,
				Name = user.Name,
				Role = user.Role!
			});
		}

		// POST /api/auth/login
		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest request)
		{
			// Validation
			if (string.IsNullOrWhiteSpace(request.Email))
				return BadRequest(new { Message = "Email is required." });

			if (string.IsNullOrWhiteSpace(request.Password))
				return BadRequest(new { Message = "Password is required." });

			// Find user by email
			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == request.Email);

			if (user == null)
				return Unauthorized(new { Message = "Invalid email or password." });

			// Verify password
			if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
				return Unauthorized(new { Message = "Invalid email or password." });

			// Generate token
			var token = _authService.GenerateToken(user);

			return Ok(new AuthResponse
			{
				Token = token,
				Email = user.Email,
				Name = user.Name,
				Role = user.Role!
			});
		}

		// GET /api/auth/me (requires authentication)
		[Authorize]
		[HttpGet("me")]
		public IActionResult GetCurrentUser()
		{
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
				return Unauthorized(new { Message = "Invalid token." });

			var user = _context.Users.Find(userId);

			if (user == null)
				return NotFound(new { Message = "User not found." });

			return Ok(new
			{
				user.Id,
				user.Name,
				user.Email,
				user.FirstName,
				user.LastName,
				user.PhoneNumber,
				user.Role,
				user.CreatedAt
			});
		}
	}
}