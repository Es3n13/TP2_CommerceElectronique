using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using AuthService.Data;

namespace AuthService.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;
        private readonly AuthDbContext _context;

        public TokenService(IConfiguration config, AuthDbContext context)
        {
            _config = config;
            _context = context;
        }

        public string GenerateToken(UserService.Models.User user)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Pseudo),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<string> GenerateRefreshTokenAsync(UserService.Models.User user, string jwtId)
        {
            var refreshTokenBytes = new byte[64];
            RandomNumberGenerator.GetBytes(refreshTokenBytes);
            var plainRefreshToken = Convert.ToBase64String(refreshTokenBytes);

            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainRefreshToken));
            var hashedRefreshToken = Convert.ToBase64String(hashedBytes);

            var refreshTokenEntity = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = user.Id.ToString(),
                Token = hashedRefreshToken,
                JwtId = jwtId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = null,
                ReplacedByToken = null
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            return plainRefreshToken;
        }

        public async Task<bool> ValidateRefreshTokenAsync(string plainRefreshToken)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainRefreshToken));
            var hashedRefreshToken = Convert.ToBase64String(hashedBytes);

            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == hashedRefreshToken);

            if (storedToken == null)
                return false;

            return !storedToken.RevokedAt.HasValue && storedToken.ExpiresAt > DateTime.UtcNow;
        }

        public async Task RevokeRefreshTokenAsync(string plainRefreshToken, string? reason = null)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainRefreshToken));
            var hashedRefreshToken = Convert.ToBase64String(hashedBytes);

            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == hashedRefreshToken);

            if (storedToken != null)
            {
                storedToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}