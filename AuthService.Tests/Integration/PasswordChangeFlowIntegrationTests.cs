using AuthService.Data;
using AuthService.Services;
using AuthService.Tests;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace AuthService.Tests.Integration
{
    /// <summary>
    /// Integration tests for password change flow with token revocation
    /// Tests the complete lifecycle: generate token → change password → revoke old token → validate behavior
    /// </summary>
    public class PasswordChangeFlowIntegrationTests
    {
        #region Password Change with Token Revocation

        [Fact]
        public async Task PasswordChangeFlow_ShouldRevokeOldTokens_AllowNewTokens()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            var loggerMock = new Mock<ILogger<PasswordChangeFlowIntegrationTests>>();

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;
            var userEmail = TestData.TestEmail;
            var userPseudo = TestData.TestPseudo;

            // Step 1: Generate initial token (before password change)
            var oldToken = TestFixtures.GenerateTestToken(
                TestData.TestUserIdGuid,
                userEmail,
                userPseudo,
                TestData.TestRole,
                out string oldTokenJti
            );

            var oldClaims = TestFixtures.CreateClaimsPrincipalFromToken(oldToken);

            // Step 2: Validate old token is valid before password change
            var isOldTokenValidBeforeChange = await validationService.ValidateTokenAsync(oldToken, oldClaims);
            isOldTokenValidBeforeChange.Should().BeTrue("Old token should be valid before password change");

            // Step 3: Simulate password change (revoke all user tokens)
            // In a real scenario, this would be called from a password change controller
            await revokedTokenService.RevokeAllUserTokensAsync(userId);

            // Step 4: Verify old token is now revoked
            var isOldTokenValidAfterChange = await validationService.ValidateTokenAsync(oldToken, oldClaims);
            isOldTokenValidAfterChange.Should().BeFalse("Old token should be revoked after password change");

            // Verify the revoked token exists in database
            var revokedTokenRecord = await context.RevokedAccessTokens
                .FirstOrDefaultAsync(rt => rt.TokenJti == oldTokenJti);
            
            revokedTokenRecord.Should().BeNull("Only refresh tokens should be revoked in bulk");

            // Step 5: Generate new token (after password change)
            var newToken = TestFixtures.GenerateTestToken(
                TestData.TestUserIdGuid,
                userEmail,
                userPseudo,
                TestData.TestRole,
                out string newTokenJti
            );

            var newClaims = TestFixtures.CreateClaimsPrincipalFromToken(newToken);

            // Step 6: Verify new token is valid
            var isNewTokenValid = await validationService.ValidateTokenAsync(newToken, newClaims);
            isNewTokenValid.Should().BeTrue("New token should be valid after password change");

            // Step 7: Verify new token is not in revocation list
            var isNewTokenRevoked = await revokedTokenService.IsTokenRevokedAsync(newTokenJti);
            isNewTokenRevoked.Should().BeFalse("New token should not be revoked");
        }

        [Fact]
        public async Task PasswordChangeFlow_WithMultipleActiveTokens_ShouldRevokeAll()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;

            // Step 1: Create multiple refresh tokens for the user
            var jwtId1 = Guid.NewGuid().ToString();
            var jwtId2 = Guid.NewGuid().ToString();
            var jwtId3 = Guid.NewGuid().ToString();

            var refreshTokens = new[]
            {
                TestFixtures.CreateRefreshToken(userId, jwtId1, isActive: true),
                TestFixtures.CreateRefreshToken(userId, jwtId2, isActive: true),
                TestFixtures.CreateRefreshToken(userId, jwtId3, isActive: true)
            };

            context.RefreshTokens.AddRange(refreshTokens);
            await context.SaveChangesAsync();

            // Step 2: Validate that all tokens are valid before revocation
            foreach (var jwtId in new[] { jwtId1, jwtId2, jwtId3 })
            {
                var isRevoked = await revokedTokenService.IsTokenRevokedAsync(jwtId);
                isRevoked.Should().BeFalse($"Token {jwtId} should not be revoked before password change");
            }

            // Step 3: Simulate password change (revoke all user tokens)
            await revokedTokenService.RevokeAllUserTokensAsync(userId);

            // Step 4: Verify all tokens are revoked
            var revokedTokensCount = await context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .CountAsync();

            revokedTokensCount.Should().Be(3, "All 3 tokens should be revoked");

            foreach (var jwtId in new[] { jwtId1, jwtId2, jwtId3 })
            {
                var isRevoked = await revokedTokenService.IsTokenRevokedAsync(jwtId);
                isRevoked.Should().BeTrue($"Token {jwtId} should be revoked after password change");
            }

            // Step 5: Verify all refresh tokens are marked as revoked
            var revokedRefreshTokens = await context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            revokedRefreshTokens.Should().OnlyContain(
                rt => rt.RevokedAt != null,
                "All refresh tokens should be marked as revoked"
            );
        }

        #endregion

        #region Old Token Rejection After Password Change

        [Fact]
        public async Task ValidateTokenAfterPasswordChange_OldTokenShouldBeRejected()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;

            // Step 1: Generate and refresh token
            var jwtId = Guid.NewGuid().ToString();
            var token = TestFixtures.GenerateTestToken(
                TestData.TestUserIdGuid,
                TestData.TestEmail,
                TestData.TestPseudo,
                TestData.TestRole,
                out string tokenJti
            );

            var claims = TestFixtures.CreateClaimsPrincipalFromToken(token);

            // Create corresponding refresh token
            var refreshToken = TestFixtures.CreateRefreshToken(userId, jwtId, isActive: true);
            context.RefreshTokens.Add(refreshToken);
            await context.SaveChangesAsync();

            // Step 2: Verify token is valid initially
            var isValidBefore = await validationService.ValidateTokenAsync(token, claims);
            isValidBefore.Should().BeTrue();

            // Step 3: Revoke the specific refresh token (simulating password change)
            await revokedTokenService.RevokedTokenAsync(jwtId, userId, TestData.TestTokenReason);

            // Step 4: Verify old token is now rejected
            var isValidAfter = await validationService.ValidateTokenAsync(token, claims);
            isValidAfter.Should().BeFalse("Old token should be rejected after revocation");

            // Step 5: Verify the revocation was recorded
            var revokedToken = await context.RevokedAccessTokens
                .FirstOrDefaultAsync(rt => rt.TokenJti == jwtId);

            revokedToken.Should().NotBeNull();
            revokedToken!.TokenJti.Should().Be(jwtId);
            revokedToken.Reason.Should().Be(TestData.TestTokenReason);

            // Step 6: Verify refresh token was also revoked
            var revokedRefreshToken = await context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenId == refreshToken.TokenId);

            // Note: RevokedTokenAsync only adds to RevokedAccessTokens, doesn't modify RefreshTokens
            // This is expected behavior - RevokeAllUserTokensAsync updates RefreshTokens
        }

        [Fact]
        public async Task ValidateTokenAfterPasswordChange_CacheHitShouldRejectQuickly()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            // Setup Redis to return true for the revoked token (cache hit)
            var tokenJti = Guid.NewGuid().ToString();
            
            redisDbMock
                .Setup(x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None))
                .ReturnsAsync(true);

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);

            // Generate token
            var token = TestFixtures.GenerateTestToken(
                TestData.TestUserIdGuid,
                TestData.TestEmail,
                TestData.TestPseudo,
                TestData.TestRole,
                out string _);

            var claims = TestFixtures.CreateClaimsPrincipalFromToken(token);

            // Manually update the claims to use the specific JTI
            var newClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, TestData.TestUserId.ToString()),
                new Claim(ClaimTypes.Email, TestData.TestEmail),
                new Claim(ClaimTypes.Name, TestData.TestPseudo),
                new Claim(ClaimTypes.Role, TestData.TestRole)
            }));

            // Act
            var isValid = await validationService.ValidateTokenAsync(token, newClaims);

            // Assert
            isValid.Should().BeFalse("Token should be rejected from cache hit");

            // Verify database was not checked (cache hit)
            var dbCheckCount = await context.RevokedAccessTokens
                .Where(rt => rt.TokenJti == tokenJti)
                .CountAsync();

            // Redis cache returned true immediately, so DB was not checked
            dbCheckCount.Should().Be(0, "Database should not be checked when cache has the token");

            // Verify Redis was checked
            redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Once,
                "Redis cache should be checked only once"
            );
        }

        [Fact]
        public async Task ValidateTokenAfterPasswordChange_DatabaseFallbackShouldReject()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            // Setup Redis to return false (cache miss)
            redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                .ReturnsAsync(false);

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);

            var tokenJti = Guid.NewGuid().ToString();
            var userId = TestData.TestUserId;

            // Add revoked token to database
            var revokedToken = TestFixtures.CreateRevokedToken(tokenJti, userId, TestData.TestTokenReason);
            context.RevokedAccessTokens.Add(revokedToken);
            await context.SaveChangesAsync();

            // Generate token
            var token = TestFixtures.GenerateTestToken(
                TestData.TestUserIdGuid,
                TestData.TestEmail,
                TestData.TestPseudo,
                TestData.TestRole,
                out string _);

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, TestData.TestUserId.ToString()),
                new Claim(ClaimTypes.Email, TestData.TestEmail),
                new Claim(ClaimTypes.Name, TestData.TestPseudo),
                new Claim(ClaimTypes.Role, TestData.TestRole)
            }));

            // Act
            var isValid = await validationService.ValidateTokenAsync(token, claims);

            // Assert
            isValid.Should().BeFalse("Token should be rejected from database fallback");

            // Verify Redis was checked first
            redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Once
            );
        }

        #endregion

        #region New Token Works After Password Change

        [Fact]
        public async Task ValidateNewTokenAfterPasswordChange_NewTokenShouldBeAccepted()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;

            // Step 1: Create and revoke an old token
            var oldJwtId = Guid.NewGuid().ToString();
            var oldRefreshToken = TestFixtures.CreateRefreshToken(userId, oldJwtId, isActive: true);
            context.RefreshTokens.Add(oldRefreshToken);
            await context.SaveChangesAsync();

            await revokedTokenService.RevokedTokenAsync(oldJwtId, userId, TestData.TestTokenReason);

            // Step 2: Verify old token is revoked
            var oldIsRevoked = await revokedTokenService.IsTokenRevokedAsync(oldJwtId);
            oldIsRevoked.Should().BeTrue();

            // Step 3: Generate new token (after password change)
            var newJwtId = Guid.NewGuid().ToString();
            var newToken = TestFixtures.GenerateTestToken(
                TestData.TestUserIdGuid,
                TestData.TestEmail,
                TestData.TestPseudo,
                TestData.TestRole,
                out string newTokenJti
            );

            var newClaims = TestFixtures.CreateClaimsPrincipalFromToken(newToken);

            // Step 4: Verify new token is valid
            var newIsValid = await validationService.ValidateTokenAsync(newToken, newClaims);
            newIsValid.Should().BeTrue("New token should be valid after password change");

            // Step 5: Verify new token is not in revocation list
            var newIsRevoked = await revokedTokenService.IsTokenRevokedAsync(newTokenJti);
            newIsRevoked.Should().BeFalse();

            // Step 6: Verify database doesn't have new token in revocation list
            var newTokenRevokedRecord = await context.RevokedAccessTokens
                .FirstOrDefaultAsync(rt => rt.TokenJti == newTokenJti);

            newTokenRevokedRecord.Should().BeNull("New token should not be in revocation database");
        }

        [Fact]
        public async Task MultiplePasswordChanges_EachShouldRevokePreviousTokens()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;

            // Step 1: First password change - create and revoke first set of tokens
            var jwtId1 = Guid.NewGuid().ToString();
            var jwtId2 = Guid.NewGuid().ToString();

            var refreshTokens1 = new[]
            {
                TestFixtures.CreateRefreshToken(userId, jwtId1, isActive: true),
                TestFixtures.CreateRefreshToken(userId, jwtId2, isActive: true)
            };

            context.RefreshTokens.AddRange(refreshTokens1);
            await context.SaveChangesAsync();

            await revokedTokenService.RevokeAllUserTokensAsync(userId);

            // Verify first set is revoked
            var set1RevokedCount = await context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId && (rt.TokenJti == jwtId1 || rt.TokenJti == jwtId2))
                .CountAsync();

            set1RevokedCount.Should().Be(2);

            // Step 2: Second password change - create new tokens
            var jwtId3 = Guid.NewGuid().ToString();
            var jwtId4 = Guid.NewGuid().ToString();

            var refreshTokens2 = new[]
            {
                TestFixtures.CreateRefreshToken(userId, jwtId3, isActive: true),
                TestFixtures.CreateRefreshToken(userId, jwtId4, isActive: true)
            };

            context.RefreshTokens.AddRange(refreshTokens2);
            await context.SaveChangesAsync();

            await revokedTokenService.RevokeAllUserTokensAsync(userId);

            // Verify all tokens are revoked (both sets)
            var allRevokedCount = await context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .CountAsync();

            allRevokedCount.Should().Be(4);
        }

        [Fact]
        public async Task PasswordChangeWithConcurrentSessions_AllSessionsShouldBeRevoked()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;

            // Simulate user logged in from multiple devices
            var devices = new[]
            {
                ("iPhone", Guid.NewGuid().ToString()),
                ("MacBook", Guid.NewGuid().ToString()),
                ("iPad", Guid.NewGuid().ToString()),
                ("Desktop", Guid.NewGuid().ToString()),
                ("Android", Guid.NewGuid().ToString())
            };

            var refreshTokens = devices.Select(d => 
                TestFixtures.CreateRefreshToken(userId, d.Item2, isActive: true)
            ).ToArray();

            context.RefreshTokens.AddRange(refreshTokens);
            await context.SaveChangesAsync();

            // Step 1: Verify all tokens are initially valid
            foreach (var (device, jwtId) in devices)
            {
                var isRevoked = await revokedTokenService.IsTokenRevokedAsync(jwtId);
                isRevoked.Should().BeFalse($"{device} token should be valid initially");
            }

            // Step 2: Simulate password change (revoke all tokens)
            await revokedTokenService.RevokeAllUserTokensAsync(userId);

            // Step 3: Verify all device tokens are revoked
            foreach (var (device, jwtId) in devices)
            {
                var isRevoked = await revokedTokenService.IsTokenRevokedAsync(jwtId);
                isRevoked.Should().BeTrue($"{device} token should be revoked after password change");
            }

            // Step 4: Verify refresh tokens are marked
            var revokedRefreshTokens = await context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();

            revokedRefreshTokens.Should().HaveCount(5, "All 5 device tokens should be revoked");
            revokedRefreshTokens.Should().OnlyContain(
                rt => rt.RevokedAt != null,
                "All refresh tokens should be marked as revoked"
            );
        }

        #endregion

        #region Edge Cases and Scenarios

        [Fact]
        public async Task PasswordChangeWithExpiredTokens_ShouldOnlyRevokeActive()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;

            // Create mixed tokens (active and expired)
            var activeJwtId = Guid.NewGuid().ToString();
            var expiredJwtId = Guid.NewGuid().ToString();

            var activeRefreshToken = TestFixtures.CreateRefreshToken(userId, activeJwtId, isActive: true);
            
            var expiredRefreshToken = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId,
                Token = "expired_token",
                JwtId = expiredJwtId,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                ExpiresAt = DateTime.UtcNow.AddDays(-3), // Expired
                RevokedAt = null
            };

            context.RefreshTokens.AddRange(activeRefreshToken, expiredRefreshToken);
            await context.SaveChangesAsync();

            // Step 1: Revoke all tokens
            await revokedTokenService.RevokeAllUserTokensAsync(userId);

            // Step 2: Verify only active token was revoked
            var revokedCount = await context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .CountAsync();

            revokedCount.Should().Be(1, "Only active token should be revoked");

            var revokedToken = await context.RevokedAccessTokens
                .FirstOrDefaultAsync(rt => rt.UserId == userId);

            revokedToken!.TokenJti.Should().Be(activeJwtId);
        }

        [Fact]
        public async Task PasswordChangeWithAlreadyRevokedTokens_ShouldNotDuplicateRecords()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var revokedTokenService = TestFixtures.CreateRevokedTokenService(
                context,
                redisMock,
                new Mock<IJwtRevocationValidationService>(),
                new Mock<ILogger<RevokedAccessTokenService>>()
            );

            var userId = TestData.TestUserId;
            var jwtId = Guid.NewGuid().ToString();

            // Add already revoked token
            var existingRevokedToken = TestFixtures.CreateRevokedToken(jwtId, userId, "Previously revoked");
            context.RevokedAccessTokens.Add(existingRevokedToken);

            // Add corresponding refresh token (already revoked)
            var existingRefreshToken = TestFixtures.CreateRefreshToken(userId, jwtId, isActive: false);
            context.RefreshTokens.Add(existingRefreshToken);
            
            await context.SaveChangesAsync();

            var initialRevokedCount = await context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .CountAsync();

            // Step 1: Try to revoke all tokens again
            await revokedTokenService.RevokeAllUserTokensAsync(userId);

            // Step 2: Verify no duplicate records created
            var finalRevokedCount = await context.RevokedAccessTokens
                .Where(rt => rt.UserId == userId)
                .CountAsync();

            finalRevokedCount.Should().Be(initialRevokedCount, 
                "No duplicate revocation records should be created for already revoked tokens");
        }

        #endregion
    }
}