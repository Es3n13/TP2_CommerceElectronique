using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;

namespace UserService.Controllers
{
	public class CreateUserRequest
	{
		public string Name { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? PhoneNumber { get; set; }
	}

	public class UpdateUserRequest
	{
		public string? Name { get; set; }
		public string? Email { get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? PhoneNumber { get; set; }
	}

	[ApiController]
	[Route("api/users")]
	public class UsersController : ControllerBase
	{
		private readonly UserDbContext _context;

		public UsersController(UserDbContext context)
		{
			_context = context;
		}

		// GET https://localhost:PORT/api/users
		[HttpGet]
		public async Task<IActionResult> GetAll()
		{
			var users = await _context.Users
				.OrderByDescending(u => u.CreatedAt)
				.ToListAsync();

			return Ok(users);
		}

		// GET https://localhost:PORT/api/users/{id}
		[HttpGet("{id}")]
		public async Task<IActionResult> GetById(int id)
		{
			var user = await _context.Users.FindAsync(id);

			if (user == null)
			{
				return NotFound(new { Message = $"User with ID {id} not found." });
			}

			return Ok(user);
		}

		// POST https://localhost:PORT/api/users
		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
		{
			// Validation
			if (string.IsNullOrWhiteSpace(request.Name))
			{
				return BadRequest(new { Message = "Name is required." });
			}

			if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
			{
				return BadRequest(new { Message = "Valid email is required." });
			}

            // Vérifier si l'email existe déjà
			var existingUser = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == request.Email);

			if (existingUser != null)
			{
				return Conflict(new { Message = $"User with email '{request.Email}' already exists." });
			}

			var user = new User
			{
				Name = request.Name,
				Email = request.Email,
				FirstName = request.FirstName,
				LastName = request.LastName,
				PhoneNumber = request.PhoneNumber,
				CreatedAt = DateTime.UtcNow
			};

			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			return CreatedAtAction(
				nameof(GetById),
				new { id = user.Id },
				user
			);
		}

		// PUT https://localhost:PORT/api/users/{id}
		[HttpPut("{id}")]
		public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
		{
			var user = await _context.Users.FindAsync(id);

			if (user == null)
			{
				return NotFound(new { Message = $"User with ID {id} not found." });
			}

            // Vérifier si l'email existe déjà pour un autre utilisateur
			if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
			{
				var existingUser = await _context.Users
					.FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != id);

				if (existingUser != null)
				{
					return Conflict(new { Message = $"Email '{request.Email}' is already in use." });
				}
			}

            // Mettre � jour les champs si ils sont fournis dans la requ�te
            if (!string.IsNullOrEmpty(request.Name))
				user.Name = request.Name;
			if (!string.IsNullOrEmpty(request.Email))
				user.Email = request.Email;
			if (request.FirstName != null)
				user.FirstName = request.FirstName;
			if (request.LastName != null)
				user.LastName = request.LastName;
			if (request.PhoneNumber != null)
				user.PhoneNumber = request.PhoneNumber;

			await _context.SaveChangesAsync();

			return Ok(user);
		}

		// DELETE https://localhost:PORT/api/users/{id}
		[HttpDelete("{id}")]
		public async Task<IActionResult> Delete(int id)
		{
			var user = await _context.Users.FindAsync(id);

			if (user == null)
			{
				return NotFound(new { Message = $"User with ID {id} not found." });
			}

			_context.Users.Remove(user);
			await _context.SaveChangesAsync();

			return Ok(new { Message = $"User with ID {id} deleted successfully." });
		}
	}
}