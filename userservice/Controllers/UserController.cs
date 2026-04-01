using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using UserService.Data;
using UserService.Models;

namespace UserService.Controllers
{
	[ApiController]
	[Route("api/users")]
	public class UserController : ControllerBase
	{
		private readonly UserDbContext _context;
		private readonly HttpClient _authServiceClient;

		public UserController(UserDbContext context, IHttpClientFactory httpClientFactory)
		{
			_context = context;
			_authServiceClient = httpClientFactory.CreateClient("AuthService");
		}

		// Modèles
		public class UserLoginRequest
		{
			public string Email { get; set; } = string.Empty;
			public string Password { get; set; } = string.Empty;
		}

		public class LoginResponse
		{
			public string Token { get; set; } = string.Empty;
			public User User { get; set; } = null!;
		}

		public class UserRegisterRequest
		{
			public string FirstName { get; set; } = string.Empty;
			public string LastName { get; set; } = string.Empty;
			public string Pseudo { get; set; } = string.Empty;
			public string Email { get; set; } = string.Empty;
			public string Password { get; set; } = string.Empty;
			public string? PhoneNumber { get; set; }
			public string? Role { get; set; } = "User";
		}

        // POST /api/users/register - Enregistrer un nouvel utilisateur
        [HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] UserRegisterRequest request)
		{
			if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
			{
				return BadRequest(new { Message = "Email and password are required." });
			}

			// Vérifier si l'email existe déjà
			var existingUser = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == request.Email);

			if (existingUser != null)
			{
				return Conflict(new { Message = "User with this email already exists." });
			}

			var user = new User
			{
				FirstName = request.FirstName,
				LastName = request.LastName,
				Pseudo = request.Pseudo,
				Email = request.Email,
				PasswordHash = HashPassword(request.Password),
				PhoneNumber = request.PhoneNumber,
				Role = request.Role ?? "User",
				CreatedAt = DateTime.UtcNow
			};

			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
		}

        // POST /api/users/login - Login utilisateur et obtenir un token JWT
        [HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] UserLoginRequest request)
		{
			if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
			{
				return BadRequest(new { Message = "Email and password are required." });
			}

			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == request.Email);

			if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
			{
				return Unauthorized(new { Message = "Invalid email or password." });
			}

            // Appel AuthService pour générer un token JWT
            var tokenRequest = new
			{
				UserId = user.Id,
				Email = user.Email,
				Pseudo = user.Pseudo,
				Role = user.Role
			};

			var response = await _authServiceClient.PostAsJsonAsync("api/auth/token", tokenRequest);

			if (!response.IsSuccessStatusCode)
			{
				return StatusCode((int)response.StatusCode, new { Message = "Failed to generate token." });
			}

			var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

			return Ok(new LoginResponse
			{
				Token = tokenResponse?.Token ?? string.Empty,
				User = user
			});
		}

        // GET /api/users - Get tout les utilisateurs
        [HttpGet]
		public async Task<IActionResult> GetAll()
		{
			var users = await _context.Users.ToListAsync();
			return Ok(users);
		}

		// GET /api/users/{id} - Get utilisateur par ID
		[HttpGet("{id}")]
		public async Task<IActionResult> GetById(int id)
		{
			var user = await _context.Users.FindAsync(id);

			if (user == null)
			{
				return NotFound(new { Message = "User not found." });
			}

			return Ok(user);
		}

		// GET /api/users/email/{email} - Get utilisateur par email
		[HttpGet("email/{email}")]
		public async Task<IActionResult> GetByEmail(string email)
		{
			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == email);

			if (user == null)
			{
				return NotFound(new { Message = "User not found." });
			}

			return Ok(user);
		}

		// PUT /api/users/{id} - Update utilisateur
		[HttpPut("{id}")]
		public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
		{
			var user = await _context.Users.FindAsync(id);

			if (user == null)
			{
				return NotFound(new { Message = "User not found." });
			}

			// Vérifier si l'email existe déjà pour un autre utilisateur
			if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
			{
				var existingUser = await _context.Users
					.FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != id);

				if (existingUser != null)
				{
					return Conflict(new { Message = "Email already in use." });
				}

				user.Email = request.Email;
			}

			user.FirstName = request.FirstName ?? user.FirstName;
			user.LastName = request.LastName ?? user.LastName;
			user.Pseudo = request.Pseudo ?? user.Pseudo;
			user.PhoneNumber = request.PhoneNumber;
			user.Role = request.Role ?? user.Role;

			_context.Users.Update(user);
			await _context.SaveChangesAsync();

			return Ok(user);
		}

        // DELETE /api/users/{id} - Delete utilisateur
        [HttpDelete("{id}")]
		public async Task<IActionResult> Delete(int id)
		{
			var user = await _context.Users.FindAsync(id);

			if (user == null)
			{
				return NotFound(new { Message = "User not found." });
			}

			_context.Users.Remove(user);
			await _context.SaveChangesAsync();

			return NoContent();
		}

        // Hash de mot de passe simple
        private string HashPassword(string password)
		{
			return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password + "_salt"));
		}

        // Vérification de mot de passe
        private bool VerifyPassword(string password, string hash)
		{
			return HashPassword(password) == hash;
		}
	}

	public class UpdateUserRequest
	{
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? Pseudo { get; set; }
		public string? Email { get; set; }
		public string? PhoneNumber { get; set; }
		public string? Role { get; set; }
	}

    // DTO pour la réponse du token JWT
    public class TokenResponse
	{
		public string Token { get; set; } = string.Empty;
	}
}