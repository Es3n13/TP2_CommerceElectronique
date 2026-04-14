using AuthService.Data;
using AuthService.Services;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Tests
{
    /// <summary>
    /// Test fixture helper providing common setup data and utilities for integration tests
    /// </summary>
    public static class TestFixtures
    {
        /// <summary>
        /// Creates a fresh in-memory database context for isolated testing
        /// </summary>
        public static AuthDbContext CreateTestDbContext()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestAuthDb_{Guid.NewGuid()}")
                .Options;

            return new AuthDbContext(options);
        }

        /// <summary>
        /// Creates a mocked Redis connection for testing
        /// </summary>
        public static (Mock<IConnectionMultiplexer> RedisMock, Mock<IDatabase> DbMock) CreateMockRedis()
        {
            var redisDbMock = new Mock<IDatabase>();
            var redisMock = new Mock<IConnectionMultiplexer>();

            redisMock
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(redisDbMock.Object);

            // Setup default behaviors
            redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            redisDbMock
                .Setup(x => x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            return (redisMock, redisDbMock);
        }

        /// <summary>
        /// Creates a JwtRevocationValidationService with mocked dependencies
        /// </summary>
        public static JwtRevocationValidationService CreateRevocationValidationService(
            AuthDbContext context,
            Mock<IConnectionMultiplexer> redisMock,
            Mock<ILogger<JwtRevocationValidationService>>? loggerMock = null)
        {
            var logger = loggerMock ?? new Mock<ILogger<JwtRevocationValidationService>>();

            return new JwtRevocationValidationService(
                redisMock.Object,
                context,
                logger.Object
            );
        }

        /// <summary>
        /// Creates a RevokedAccessTokenService with mocked dependencies
        /// </summary>
        public static RevokedAccessTokenService CreateRevokedTokenService(
            AuthDbContext context,
            Mock<IConnectionMultiplexer> redisMock,
            Mock<IJwtRevocationValidationService>? validationMock = null,
            Mock<ILogger<RevokedAccessTokenService>>? loggerMock = null)
        {
            var logger = loggerMock ?? new Mock<ILogger<RevokedAccessTokenService>>();
            var validation = validationMock ?? new Mock<IJwtRevocationValidationService>();

            validation
                .Setup(x => x.CacheRevokedTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);

            return new RevokedAccessTokenService(
                context,
                logger.Object,
                redisMock.Object,
                validation.Object
            );
        }

        /// <summary>
        /// Creates a mock TokenService for testing
        /// </summary>
        public static TokenService CreateTokenService(AuthDbContext context)
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                {"Jwt:SecretKey", "ThisIsASecretKeyForTesting12345678901234567890"},
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            return new TokenService(configuration, context);
        }

        /// <summary>
        /// Generates a valid JWT token for testing
        /// </summary>
        public static string GenerateTestToken(Guid userId, string email, string pseudo, string role, out string tokenJti)
        {
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("ThisIsASecretKeyForTesting12345678901234567890")
            );

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            tokenJti = Guid.NewGuid().ToString();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, pseudo),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: "TestIssuer",
                audience: "TestAudience",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Creates a ClaimsPrincipal from a token string
        /// </summary>
        public static ClaimsPrincipal CreateClaimsPrincipalFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return new ClaimsPrincipal(new ClaimsIdentity(jwtToken.Claims));
        }

        /// <summary>
        /// Creates a test RevokedAccessToken entity
        /// </summary>
        public static RevokedAccessToken CreateRevokedToken(string tokenJti, string userId, string? reason = null, TimeSpan? expiresIn = null)
        {
            return new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = tokenJti,
                UserId = userId,
                Reason = reason ?? "Test token revocation",
                RevokedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(1))
            };
        }

        /// <summary>
        /// Creates a test RefreshToken entity
        /// </summary>
        public static RefreshToken CreateRefreshToken(string userId, string jwtId, bool isActive = true)
        {
            return new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId,
                Token = "test_refresh_token_" + Guid.NewGuid(),
                JwtId = jwtId,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = isActive ? null : DateTime.UtcNow
            };
        }

        /// <summary>
        /// Asserts that token JTI matches between claims and revoked token
        /// </summary>
        public static void AssertTokenRevoked(string tokenJti, RevokedAccessToken revokedToken)
        {
            revokedToken.Should().NotBeNull();
            revokedToken!.TokenJti.Should().Be(tokenJti);
            revokedToken.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            revokedToken.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        }

        /// <summary>
        /// Waits for asynchronous operations to complete (useful for timing-dependent tests)
        /// </summary>
        public static async Task WaitForDelay(int milliseconds = 100)
        {
            await Task.Delay(milliseconds);
        }
    }

    /// <summary>
    /// Test data constants used across integration tests
    /// </summary>
    public static class TestData
    {
        public static readonly string TestUserId = "test-user-123";
        public static readonly string TestEmail = "test@example.com";
        public static readonly string TestPseudo = "TestUser";
        public static readonly string TestRole = "User";
        public static readonly string TestTokenReason = "Password change";
        
        public static readonly Guid TestUserIdGuid = Guid.NewGuid();
        
        public static class Errors
        {
            public static readonly string TokenRevoked = "Token has been revoked";
            public static readonly string InvalidTokenFormat = "Invalid token format";
        }
    }
}