using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using userservice.Models;

namespace userservice.Services
{
	/// <summary>
	/// Service for JWT token generation
	/// </summary>
	public class AuthService
	{
		private readonly IConfiguration _configuration;

		public AuthService(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		/// <summary>
		/// Generate JWT token for a user
		/// </summary>
		public string GenerateToken(User user)
		{
			var secretKey = _configuration["Jwt:SecretKey"]!
				?? throw new InvalidOperationException("JWT SecretKey is not configured.");

			var issuer = _configuration["Jwt:Issuer"]!;
			var audience = _configuration["Jwt:Audience"]!;
			var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationInMinutes"] ?? "60");

			var claims = new List<Claim>
			{
				new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
				new Claim(JwtRegisteredClaimNames.Email, user.Email),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim(ClaimTypes.Name, user.Name),
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim(ClaimTypes.Role, user.Role ?? "User")
			};

			if (!string.IsNullOrEmpty(user.FirstName))
				claims.Add(new Claim("FirstName", user.FirstName));

			if (!string.IsNullOrEmpty(user.LastName))
				claims.Add(new Claim("LastName", user.LastName));

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
			var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var expiration = DateTime.UtcNow.AddMinutes(expirationMinutes);

			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: expiration,
				signingCredentials: credentials
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}