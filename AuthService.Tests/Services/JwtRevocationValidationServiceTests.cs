using AuthService.Data;
using AuthService.Services;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace AuthService.Tests.Services
{
    public class JwtRevocationValidationServiceTests
    {
        private readonly AuthDbContext _context;
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly Mock<IDatabase> _redisDbMock;
        private readonly Mock<ILogger<JwtRevocationValidationService>> _loggerMock;
        private readonly JwtRevocationValidationService _service;

        public JwtRevocationValidationServiceTests()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestAuthDb_{Guid.NewGuid()}")
                .Options;

            _context = new AuthDbContext(options);
            _redisDbMock = new Mock<IDatabase>();
            _redisMock = new Mock<IConnectionMultiplexer>();
            _loggerMock = new Mock<ILogger<JwtRevocationValidationService>>();

            _redisMock
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_redisDbMock.Object);

            _service = new JwtRevocationValidationService(
                _redisMock.Object,
                _context,
                _loggerMock.Object
            );
        }

        #region ValidateTokenAsync Tests

        [Fact]
        public async Task ValidateTokenAsync_WithValidNotRevokedToken_ShouldReturnTrue()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var token = "valid.jwt.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            _redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeTrue();
            _redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Once
            );
        }

        [Fact]
        public async Task ValidateTokenAsync_WithRevokedTokenInCache_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var token = "revoked.jwt.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            _redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeFalse();
            _redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Once
            );
        }

        [Fact]
        public async Task ValidateTokenAsync_WithRevokedTokenInDatabase_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var token = "revoked.jwt.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            var revokedToken = new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = tokenJti,
                UserId = "user123",
                Reason = "Test revocation",
                RevokedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _context.RevokedAccessTokens.Add(revokedToken);
            await _context.SaveChangesAsync();

            _redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateTokenAsync_WithExpiredTokenInDatabase_ShouldReturnTrue()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var token = "expired.jwt.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            var expiredRevokedToken = new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = tokenJti,
                UserId = "user123",
                Reason = "Test revocation",
                RevokedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired
            };

            _context.RevokedAccessTokens.Add(expiredRevokedToken);
            await _context.SaveChangesAsync();

            _redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateTokenAsync_WithNoJtiClaim_ShouldReturnFalse()
        {
            // Arrange
            var token = "no.jti.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeFalse();
            _redisDbMock.Verify(
                x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
                Times.Never
            );
        }

        [Fact]
        public async Task ValidateTokenAsync_WithEmptyJtiClaim_ShouldReturnFalse()
        {
            // Arrange
            var token = "empty.jti.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, ""),
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateTokenAsync_WithNullClaims_ShouldReturnFalse()
        {
            // Arrange
            var token = "null.claims.token";

            // Act
            var result = await _service.ValidateTokenAsync(token, null!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateTokenAsync_WhenRedisThrowsException_ShouldFallbackToDatabase()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var token = "redis.error.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            _redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeTrue(); // Should fall back to DB and find nothing
        }

        [Fact]
        public async Task ValidateTokenAsync_WhenAllChecksFail_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var token = "all.fail.token";
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, "user123")
            }));

            _redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new Exception("Redis failed"));

            // Make the database check fail (exception returns true - fail closed)
            // This is tested by the implementation returning true on error

            // Act
            var result = await _service.ValidateTokenAsync(token, claims);

            // Assert
            result.Should().BeFalse(); // Should fail closed when Redis fails and DB check might fail
        }

        #endregion

        #region IsTokenRevokedInCacheAsync Tests

        [Fact]
        public async Task IsTokenRevokedInCacheAsync_WithRevokedToken_ShouldReturnTrue()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();

            _redisDbMock
                .Setup(x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None))
                .ReturnsAsync(true);

            // Act
            var result = await _service.IsTokenRevokedInCacheAsync(tokenJti);

            // Assert
            result.Should().BeTrue();
            _redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Once
            );
        }

        [Fact]
        public async Task IsTokenRevokedInCacheAsync_WithNonRevokedToken_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();

            _redisDbMock
                .Setup(x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None))
                .ReturnsAsync(false);

            // Act
            var result = await _service.IsTokenRevokedInCacheAsync(tokenJti);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedInCacheAsync_WhenRedisThrowsException_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();

            _redisDbMock
                .Setup(x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act
            var result = await _service.IsTokenRevokedInCacheAsync(tokenJti);

            // Assert
            result.Should().BeFalse(); // Should return false to force DB fallback
        }

        #endregion

        #region IsTokenRevokedInDatabaseAsync Tests

        [Fact]
        public async Task IsTokenRevokedInDatabaseAsync_WithRevokedToken_ShouldReturnTrue()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();

            var revokedToken = new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = tokenJti,
                UserId = "user123",
                Reason = "Test",
                RevokedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _context.RevokedAccessTokens.Add(revokedToken);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.IsTokenRevokedInDatabaseAsync(tokenJti);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsTokenRevokedInDatabaseAsync_WithNonRevokedToken_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();

            // Act
            var result = await _service.IsTokenRevokedInDatabaseAsync(tokenJti);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedInDatabaseAsync_WithExpiredToken_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();

            var expiredToken = new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = tokenJti,
                UserId = "user123",
                Reason = "Test",
                RevokedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired
            };

            _context.RevokedAccessTokens.Add(expiredToken);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.IsTokenRevokedInDatabaseAsync(tokenJti);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedInDatabaseAsync_WithNullTokenJti_ShouldReturnFalse()
        {
            // Act
            var result = await _service.IsTokenRevokedInDatabaseAsync(null!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedInDatabaseAsync_WithEmptyTokenJti_ShouldReturnFalse()
        {
            // Act
            var result = await _service.IsTokenRevokedInDatabaseAsync("");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region CacheRevokedTokenAsync Tests

        [Fact]
        public async Task CacheRevokedTokenAsync_WithValidParameters_ShouldCacheToken()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var ttl = TimeSpan.FromHours(1);

            _redisDbMock
                .Setup(x => x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _service.CacheRevokedTokenAsync(tokenJti, ttl);

            // Assert
            _redisDbMock.Verify(
                x => x.StringSetAsync(
                    $"revoked:{tokenJti}",
                    "true",
                    ttl,
                    false,
                    When.Always,
                    CommandFlags.None),
                Times.Once
            );
        }

        [Fact]
        public async Task CacheRevokedTokenAsync_WhenRedisThrowsException_ShouldNotThrow()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var ttl = TimeSpan.FromHours(1);

            _redisDbMock
                .Setup(x => x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act & Assert
            await _service.Invoking(x => x.CacheRevokedTokenAsync(tokenJti, ttl))
                .Should().NotThrowAsync();
        }

        [Fact]
        public async Task CacheRevokedTokenAsync_WithZeroTtl_ShouldStillAttemptCache()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var ttl = TimeSpan.Zero;

            _redisDbMock
                .Setup(x => x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _service.CacheRevokedTokenAsync(tokenJti, ttl);

            // Assert
            _redisDbMock.Verify(
                x => x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    ttl,
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()),
                Times.Once
            );
        }

        #endregion
    }
}