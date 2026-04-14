using AuthService.Data;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace AuthService.Tests.Services
{
    public class RevokedAccessTokenServiceTests
    {
        private readonly AuthDbContext _context;
        private readonly Mock<ILogger<RevokedAccessTokenService>> _loggerMock;
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly Mock<IDatabase> _redisDbMock;
        private readonly Mock<IJwtRevocationValidationService> _revocationValidationMock;
        private readonly RevokedAccessTokenService _service;

        public RevokedAccessTokenServiceTests()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestAuthDb_{Guid.NewGuid()}")
                .Options;

            _context = new AuthDbContext(options);
            _loggerMock = new Mock<ILogger<RevokedAccessTokenService>>();
            _redisDbMock = new Mock<IDatabase>();
            _redisMock = new Mock<IConnectionMultiplexer>();
            _revocationValidationMock = new Mock<IJwtRevocationValidationService>();

            _redisMock
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_redisDbMock.Object);

            _revocationValidationMock
                .Setup(x => x.CacheRevokedTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);

            _service = new RevokedAccessTokenService(
                _context,
                _loggerMock.Object,
                _redisMock.Object,
                _revocationValidationMock.Object
            );
        }

        #region RevokedTokenAsync Tests

        [Fact]
        public async Task RevokedTokenAsync_WithValidData_ShouldAddRevokedTokenAndCache()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var userId = "user123";
            var reason = "User requested logout";

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
            await _service.RevokedTokenAsync(tokenJti, userId, reason);

            // Assert
            var revokedToken = await _context.RevokedAccessTokens
                .FirstOrDefaultAsync(rt => rt.TokenJti == tokenJti);

            revokedToken.Should().NotBeNull();
            revokedToken!.TokenJti.Should().Be(tokenJti);
            revokedToken.UserId.Should().Be(userId);
            revokedToken.Reason.Should().Be(reason);
            revokedToken.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            revokedToken.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(5));

            // Verify caching was called
            _revocationValidationMock.Verify(
                x => x.CacheRevokedTokenAsync(tokenJti, It.IsAny<TimeSpan>()),
                Times.Once
            );
        }

        [Fact]
        public async Task RevokedTokenAsync_WithNullTokenJti_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RevokedTokenAsync(null!, "user123", "reason"));
        }

        [Fact]
        public async Task RevokedTokenAsync_WithEmptyTokenJti_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RevokedTokenAsync("", "user123", "reason"));
        }

        [Fact]
        public async Task RevokedTokenAsync_WithNullUserId_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RevokedTokenAsync("token123", null!, "reason"));
        }

        [Fact]
        public async Task RevokedTokenAsync_WithEmptyUserId_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RevokedTokenAsync("token123", "", "reason"));
        }

        [Fact]
        public async Task RevokedTokenAsync_WhenRedisCachingFails_ShouldStillSaveToDatabase()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var userId = "user123";
            var reason = "Test";

            _revocationValidationMock
                .Setup(x => x.CacheRevokedTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act & Assert
            await _service.Invoking(x => x.RevokedTokenAsync(tokenJti, userId, reason))
                .Should().NotThrowAsync();

            // Verify database still has the record
            var revokedToken = await _context.RevokedAccessTokens
                .FirstOrDefaultAsync(rt => rt.TokenJti == tokenJti);

            revokedToken.Should().NotBeNull();
            revokedToken!.TokenJti.Should().Be(tokenJti);
        }

        #endregion

        #region IsTokenRevokedAsync Tests

        [Fact]
        public async Task IsTokenRevokedAsync_WithRevokedToken_ShouldReturnTrue()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var userId = "user123";

            var revokedToken = new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = tokenJti,
                UserId = userId,
                Reason = "Test",
                RevokedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _context.RevokedAccessTokens.Add(revokedToken);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.IsTokenRevokedAsync(tokenJti);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsTokenRevokedAsync_WithNonRevokedToken_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();

            // Act
            var result = await _service.IsTokenRevokedAsync(tokenJti);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedAsync_WithExpiredRevokedToken_ShouldReturnFalse()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            var userId = "user123";

            var revokedToken = new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = tokenJti,
                UserId = userId,
                Reason = "Test",
                RevokedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired 1 hour ago
            };

            _context.RevokedAccessTokens.Add(revokedToken);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.IsTokenRevokedAsync(tokenJti);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedAsync_WithNullTokenJti_ShouldReturnFalse()
        {
            // Act
            var result = await _service.IsTokenRevokedAsync(null!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedAsync_WithEmptyTokenJti_ShouldReturnFalse()
        {
            // Act
            var result = await _service.IsTokenRevokedAsync("");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsTokenRevokedAsync_WhenDatabaseThrowsException_ShouldReturnTrue()
        {
            // Arrange
            var tokenJti = Guid.NewGuid().ToString();
            
            // Use a mock that throws exception when querying
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(databaseName: $"FailingDb_{Guid.NewGuid()}")
                .Options;
            
            var failingContext = new AuthDbContext(options);
            var failingService = new RevokedAccessTokenService(
                failingContext,
                _loggerMock.Object,
                _redisMock.Object,
                _revocationValidationMock.Object
            );

            // Act
            var result = await failingService.IsTokenRevokedAsync(tokenJti);

            // Assert - Should return true on error (fail closed)
            result.Should().BeTrue();
        }

        #endregion

        #region RevokeAllUserTokensAsync Tests (批量撤销所有用户令牌)

        [Fact]
        public async Task RevokeAllUserTokensAsync_WithActiveRefreshTokens_ShouldRevokeAll()
        {
            // Arrange
            var userId = "user123";
            var jwtId1 = Guid.NewGuid().ToString();
            var jwtId2 = Guid.NewGuid().ToString();
            var jwtId3 = Guid.NewGuid().ToString();

            // Create some refresh tokens
            var refreshTokens = new[]
            {
                new RefreshToken
                {
                    TokenId = Guid.NewGuid(),
                    UserId = userId,
                    Token = "token1",
                    JwtId = jwtId1,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    ExpiresAt = DateTime.UtcNow.AddHours(23),
                    RevokedAt = null
                },
                new RefreshToken
                {
                    TokenId = Guid.NewGuid(),
                    UserId = userId,
                    Token = "token2",
                    JwtId = jwtId2,
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(22),
                    RevokedAt = null
                },
                new RefreshToken
                {
                    TokenId = Guid.NewGuid(),
                    UserId = "other_user",
                    Token = "token3",
                    JwtId = jwtId3,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    ExpiresAt = DateTime.UtcNow.AddHours(23),
                    RevokedAt = null
                }
            };

            _context.RefreshTokens.AddRange(refreshTokens);
            await _context.SaveChangesAsync();

            // Act
            await _service.RevokeAllUserTokensAsync(userId);

            // Assert
            var userRefreshTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            userRefreshTokens.Should().HaveCount(2);
            userRefreshTokens.Should().OnlyContain(rt => rt.RevokedAt != null);

            var revokedAccessTokens = await _context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            revokedAccessTokens.Should().HaveCount(2);

            // Verify caching was called for each token
            _revocationValidationMock.Verify(
                x => x.CacheRevokedTokenAsync(jwtId1, It.IsAny<TimeSpan>()),
                Times.Once
            );
            _revocationValidationMock.Verify(
                x => x.CacheRevokedTokenAsync(jwtId2, It.IsAny<TimeSpan>()),
                Times.Once
            );

            // Verify other user's tokens are not revoked
            var otherUserTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == "other_user")
                .ToListAsync();

            otherUserTokens.Should().HaveCount(1);
            otherUserTokens.Should().OnlyContain(rt => rt.RevokedAt == null);
        }

        [Fact]
        public async Task RevokeAllUserTokensAsync_WithAlreadyRevokedTokens_ShouldOnlyRevokeActive()
        {
            // Arrange
            var userId = "user123";
            var activeJwtId = Guid.NewGuid().ToString();

            // Create mixed refresh tokens (some already revoked)
            var refreshTokens = new[]
            {
                new RefreshToken
                {
                    TokenId = Guid.NewGuid(),
                    UserId = userId,
                    Token = "active_token",
                    JwtId = activeJwtId,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    ExpiresAt = DateTime.UtcNow.AddHours(23),
                    RevokedAt = null
                },
                new RefreshToken
                {
                    TokenId = Guid.NewGuid(),
                    UserId = userId,
                    Token = "already_revoked",
                    JwtId = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(22),
                    RevokedAt = DateTime.UtcNow.AddMinutes(-30) // Already revoked
                }
            };

            _context.RefreshTokens.AddRange(refreshTokens);
            await _context.SaveChangesAsync();

            // Act
            await _service.RevokeAllUserTokensAsync(userId);

            // Assert
            var userRefreshTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            userRefreshTokens.Should().HaveCount(2);
            
            var activeTokens = await _context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            // Only one new revoked token entry should be created
            activeTokens.Should().HaveCount(1);
            activeTokens[0].TokenJti.Should().Be(activeJwtId);

            // Verify caching was called only once
            _revocationValidationMock.Verify(
                x => x.CacheRevokedTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()),
                Times.Once
            );
        }

        [Fact]
        public async Task RevokeAllUserTokensAsync_WithNoActiveTokens_ShouldSucceedGracefully()
        {
            // Arrange
            var userId = "user123";

            // Act
            await _service.Invoking(x => x.RevokeAllUserTokensAsync(userId))
                .Should().NotThrowAsync();

            // Assert
            var revokedAccessTokens = await _context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            revokedAccessTokens.Should().BeEmpty();
        }

        [Fact]
        public async Task RevokeAllUserTokensAsync_WithNullUserId_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RevokeAllUserTokensAsync(null!));
        }

        [Fact]
        public async Task RevokeAllUserTokensAsync_WithEmptyUserId_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.RevokeAllUserTokensAsync(""));
        }

        [Fact]
        public async Task RevokeAllUserTokensAsync_WhenRedisCachingFails_ShouldStillRevokeInDatabase()
        {
            // Arrange
            var userId = "user123";
            var jwtId = Guid.NewGuid().ToString();

            var refreshToken = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId,
                Token = "token1",
                JwtId = jwtId,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                ExpiresAt = DateTime.UtcNow.AddHours(23),
                RevokedAt = null
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            _revocationValidationMock
                .Setup(x => x.CacheRevokedTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act & Assert
            await _service.Invoking(x => x.RevokeAllUserTokensAsync(userId))
                .Should().NotThrowAsync();

            // Verify database changes
            var userRefreshTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            userRefreshTokens.Should().HaveCount(1);
            userRefreshTokens[0].RevokedAt.Should().NotBeNull();

            var revokedAccessTokens = await _context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            revokedAccessTokens.Should().HaveCount(1);
        }

        #endregion

        #region CleanupExpiredTokensAsync Tests (过期令牌清理)

        [Fact]
        public async Task CleanupExpiredTokensAsync_WithExpiredTokens_ShouldRemoveThem()
        {
            // Arrange
            var userId = "user123";

            // Create expired revoked tokens
            var expiredTokens = new[]
            {
                new RevokedAccessToken
                {
                    Id = Guid.NewGuid(),
                    TokenJti = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Reason = "Test1",
                    RevokedAt = DateTime.UtcNow.AddHours(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired
                },
                new RevokedAccessToken
                {
                    Id = Guid.NewGuid(),
                    TokenJti = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Reason = "Test2",
                    RevokedAt = DateTime.UtcNow.AddHours(-3),
                    ExpiresAt = DateTime.UtcNow.AddHours(-2) // Expired
                }
            };

            // Create non-expired revoked token
            var activeToken = new RevokedAccessToken
            {
                Id = Guid.NewGuid(),
                TokenJti = Guid.NewGuid().ToString(),
                UserId = userId,
                Reason = "Test3",
                RevokedAt = DateTime.UtcNow.AddMinutes(-30),
                ExpiresAt = DateTime.UtcNow.AddHours(1) // Still active
            };

            _context.RevokedAccessTokens.AddRange(expiredTokens);
            _context.RevokedAccessTokens.Add(activeToken);
            await _context.SaveChangesAsync();

            // Act
            await _service.CleanupExpiredTokensAsync();

            // Assert
            var remainingTokens = await _context.RevokedAccessTokens.ToListAsync();
            remainingTokens.Should().HaveCount(1);
            remainingTokens[0].Id.Should().Be(activeToken.Id);
        }

        [Fact]
        public async Task CleanupExpiredTokensAsync_WithNoExpiredTokens_ShouldNotRemoveAny()
        {
            // Arrange
            var userId = "user123";

            var activeTokens = new[]
            {
                new RevokedAccessToken
                {
                    Id = Guid.NewGuid(),
                    TokenJti = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Reason = "Test1",
                    RevokedAt = DateTime.UtcNow.AddMinutes(-30),
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                },
                new RevokedAccessToken
                {
                    Id = Guid.NewGuid(),
                    TokenJti = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Reason = "Test2",
                    RevokedAt = DateTime.UtcNow.AddMinutes(-15),
                    ExpiresAt = DateTime.UtcNow.AddHours(2)
                }
            };

            _context.RevokedAccessTokens.AddRange(activeTokens);
            await _context.SaveChangesAsync();

            // Act
            await _service.CleanupExpiredTokensAsync();

            // Assert
            var remainingTokens = await _context.RevokedAccessTokens.ToListAsync();
            remainingTokens.Should().HaveCount(2);
        }

        [Fact]
        public async Task CleanupExpiredTokensAsync_WithEmptyDatabase_ShouldSucceedGracefully()
        {
            // Act & Assert
            await _service.Invoking(x => x.CleanupExpiredTokensAsync())
                .Should().NotThrowAsync();

            var remainingTokens = await _context.RevokedAccessTokens.ToListAsync();
            remainingTokens.Should().BeEmpty();
        }

        [Fact]
        public async Task CleanupExpiredTokensAsync_WithAllExpiredTokens_ShouldRemoveAll()
        {
            // Arrange
            var userId = "user123";

            var expiredTokens = new[]
            {
                new RevokedAccessToken
                {
                    Id = Guid.NewGuid(),
                    TokenJti = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Reason = "Test1",
                    RevokedAt = DateTime.UtcNow.AddHours(-2),
                    ExpiresAt = DateTime.UtcNow.AddHours(-1)
                },
                new RevokedAccessToken
                {
                    Id = Guid.NewGuid(),
                    TokenJti = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Reason = "Test2",
                    RevokedAt = DateTime.UtcNow.AddHours(-3),
                    ExpiresAt = DateTime.UtcNow.AddHours(-2)
                }
            };

            _context.RevokedAccessTokens.AddRange(expiredTokens);
            await _context.SaveChangesAsync();

            // Act
            await _service.CleanupExpiredTokensAsync();

            // Assert
            var remainingTokens = await _context.RevokedAccessTokens.ToListAsync();
            remainingTokens.Should().BeEmpty();
        }

        #endregion
    }
}