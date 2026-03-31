namespace AuthService.Models
{
	public class TokenResponse
	{
		public string Token { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string Pseudo { get; set; } = string.Empty;
		public string Role { get; set; } = string.Empty;
		public DateTime Expiration { get; set; }
	}
}