using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserService.Models
{
	/// <summary>
	/// User entity for the userservice database
	/// </summary>
	[Table("Users")]
	public class User
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[Required]
		public string Pseudo { get; set; } = string.Empty;

		[Required]
		[EmailAddress]
		public string Email { get; set; } = string.Empty;

		// Password hash for authentication
		public string? PasswordHash { get; set; }

		public string? FirstName { get; set; }

		public string? LastName { get; set; }

		public string? PhoneNumber { get; set; }

		// Audit field
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public string? Role { get; set; } = "User"; // Default role
	}
}