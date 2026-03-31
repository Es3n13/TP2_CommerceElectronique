using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using AuthService.Models;

namespace AuthService.Services
{
	public class TokenService
	{
		private readonly IConfiguration _configuration;

		public TokenService(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		public TokenResponse GenerateToken(TokenRequest request)
		{
			var secretKey = _configuration["Jwt:SecretKey"]!
				?? throw new InvalidOperationException("JWT SecretKey is not configured.");

			var issuer = _configuration["Jwt:Issuer"]!;
			var audience = _configuration["Jwt:Audience"]!;
			var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationInMinutes"] ?? "60");

			var claims = new List<Claim>
			{
				new Claim(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
				new Claim(JwtRegisteredClaimNames.Email, request.Email),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim(ClaimTypes.Name, request.Pseudo),
				new Claim(ClaimTypes.NameIdentifier, request.UserId.ToString()),
				new Claim(ClaimTypes.Role, request.Role ?? "User")
			};

			if (!string.IsNullOrEmpty(request.FirstName))
				claims.Add(new Claim("FirstName", request.FirstName));

			if (!string.IsNullOrEmpty(request.LastName))
				claims.Add(new Claim("LastName", request.LastName));

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

			return new TokenResponse
			{
				Token = new JwtSecurityTokenHandler().WriteToken(token),
				Email = request.Email,
				Pseudo = request.Pseudo,
				Role = request.Role ?? "User",
				Expiration = expiration
			};
		}

		public ClaimsPrincipal? ValidateToken(string token)
		{
			var secretKey = _configuration["Jwt:SecretKey"]!
				?? throw new InvalidOperationException("JWT SecretKey is not configured.");

			var issuer = _configuration["Jwt:Issuer"]!;
			var audience = _configuration["Jwt:Audience"]!;

			var tokenHandler = new JwtSecurityTokenHandler();
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

			try
			{
				var validationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = issuer,
					ValidAudience = audience,
					IssuerSigningKey = key,
					ClockSkew = TimeSpan.Zero
				};

				return tokenHandler.ValidateToken(token, validationParameters, out _);
			}
			catch
			{
				return null;
			}
		}
	}
}