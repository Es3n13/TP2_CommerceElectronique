using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using AuthService.Models;

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

        public TokenResponse GenerateToken(TokenRequest user)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)
            );
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var expiration = DateTime.UtcNow.AddMinutes(30);

            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.Pseudo),
        new Claim(ClaimTypes.Role, user.Role ?? "User")
    };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return new TokenResponse
            {
                Token = tokenString,
                Email = user.Email,
                Pseudo = user.Pseudo,
                Role = user.Role ?? "User",
                Expiration = expiration
            };
        }
        public async Task<string> GenerateRefreshTokenAsync(int userId, string jwtId)
        {
            var refreshTokenBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(refreshTokenBytes);

            var plainRefreshToken = Convert.ToBase64String(refreshTokenBytes);

            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainRefreshToken));
            var hashedRefreshToken = Convert.ToBase64String(hashedBytes);

            var refreshTokenEntity = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId.ToString(),
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

        public ClaimsPrincipal? ValidateToken(string token)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),

                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],

                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                if (validatedToken is not JwtSecurityToken jwtToken)
                    return null;

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}