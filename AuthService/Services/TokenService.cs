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

        // Injecte la configuration JWT et le contexte EF Core
        public TokenService(IConfiguration config, AuthDbContext context)
        {
            _config = config;
            _context = context;
        }

        // Génère un access token JWT pour l'utilisateur
        public TokenResponse GenerateToken(TokenRequest user)
        {
            var jwtSettings = _config.GetSection("Jwt");

            // Construit la clé de signature à partir du secret
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)
            );

            // Définit l'algorithme de signature du token
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Définit une durée de vie de 30 minutes pour le JWT
            var expiration = DateTime.UtcNow.AddMinutes(30);

            // Définit les claims embarqués dans le token
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Pseudo),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };

            // Crée le JWT
            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: expiration,
                signingCredentials: credentials
            );

            // Sérialise le token en chaîne de caractères
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Retourne le token et les métadonnées utiles au client
            return new TokenResponse
            {
                Token = tokenString,
                Email = user.Email,
                Pseudo = user.Pseudo,
                Role = user.Role ?? "User",
                Expiration = expiration
            };
        }

        // Génère et stock un refresh token sécurisé
        public async Task<string> GenerateRefreshTokenAsync(int userId, string jwtId)
        {
            var refreshTokenBytes = new byte[64];

            // Génère des octets aléatoires
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(refreshTokenBytes);

            // Convertit le token brut en Base64 pour l'envoyer au client
            var plainRefreshToken = Convert.ToBase64String(refreshTokenBytes);

            // Hash le refresh token avant stockage en base
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainRefreshToken));
            var hashedRefreshToken = Convert.ToBase64String(hashedBytes);

            // Prépare l'entité persistée du refresh token
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

            // Enregistre le refresh token hashé en base
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            // Retourne uniquement le token brut au client
            return plainRefreshToken;
        }

        // Vérifie qu'un refresh token existe, n'est pas révoqué et n'est pas expiré
        public async Task<bool> ValidateRefreshTokenAsync(string plainRefreshToken)
        {
            using var sha256 = SHA256.Create();

            // Recalcule le hash pour comparer avec la base
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainRefreshToken));
            var hashedRefreshToken = Convert.ToBase64String(hashedBytes);

            // Recherche le token correspondant en base
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == hashedRefreshToken);

            if (storedToken == null)
                return false;

            // Le token doit être actif et non expiré
            return !storedToken.RevokedAt.HasValue && storedToken.ExpiresAt > DateTime.UtcNow;
        }

        // Révoque un refresh token existant
        public async Task RevokeRefreshTokenAsync(string plainRefreshToken, string? reason = null)
        {
            using var sha256 = SHA256.Create();

            // Recalcule le hash pour retrouver le token stocké
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainRefreshToken));
            var hashedRefreshToken = Convert.ToBase64String(hashedBytes);

            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == hashedRefreshToken);

            if (storedToken != null)
            {
                // Marque le token comme révoqué
                storedToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        // Valide un JWT et retourne ses claims si valide
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

                // Vérifie que le token validé est bien un JWT
                if (validatedToken is not JwtSecurityToken jwtToken)
                    return null;

                return principal;
            }
            catch
            {
                // Retourne null si le token est invalide
                return null;
            }
        }
    }
}