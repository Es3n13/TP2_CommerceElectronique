namespace AuthService.Models
{
	public class TokenRequest
	{
		public int UserId { get; set; }
		public string Email { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string? Role { get; set; } = "User";
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
	}
}