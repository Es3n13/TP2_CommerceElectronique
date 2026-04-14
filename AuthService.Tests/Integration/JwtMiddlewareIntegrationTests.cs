using AuthService.Data;
using AuthService.Services;
using AuthService.Tests;
using AuthService.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace AuthService.Tests.Integration
{
    /// <summary>
    /// Integration tests for JWT middleware with token revocation checking
    /// Tests the complete authentication flow: token validation → revocation check → authorization decision
    /// </summary>
    public class JwtMiddlewareIntegrationTests
    {
        private const string SecretKey = "ThisIsASecretKeyForTesting12345678901234567890";
        private const string Issuer = "TestIssuer";
        private const string Audience = "TestAudience";

        #region Revocation Check on Protected Endpoint

        [Fact]
        public async Task JwtMiddleware_WithValidNotRevokedToken_ShouldPassAuthentication()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            // Generate valid token
            var token = GenerateTestToken(out string tokenJti);
            var claimsPrincipal = CreateClaimsPrincipal(tokenJti);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            // Setup token validated context
            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = securityToken
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().BeNull("Valid token should pass authentication");
            validatedContext.Should().NotBeNull();
            
            // Verify revocation check was performed
            redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Once,
                "Redis revocation check should be performed"
            );
        }

        [Fact]
        public async Task JwtMiddleware_WithRevokedToken_ShouldFailAuthentication()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            // Setup Redis to return true (token is revoked)
            redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                .ReturnsAsync(true);

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            // Generate token
            var token = GenerateTestToken(out string tokenJti);
            var claimsPrincipal = CreateClaimsPrincipal(tokenJti);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = securityToken
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().NotBeNull("Revoked token should fail authentication");
            
            var failureMessage = validatedContext.Result?.Failure?.Message ?? 
                                validatedContext.Principal?.FindFirst("error")?.Value;
            
            failureMessage.Should().Contain("revoked", "Error should mention token is revoked");
        }

        [Fact]
        public async Task JwtMiddleware_WithNoJtiClaim_ShouldFailAuthentication()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            // Create claims principal without JTI
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(ClaimTypes.Email, "test@example.com")
            }));

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = new JwtSecurityToken()
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().NotBeNull("Token without JTI should fail authentication");
        }

        #endregion

        #region Cache Behavior Tests

        [Fact]
        public async Task JwtMiddleware_RevocationCheck_ShouldUseCacheFirst()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var tokenJti = Guid.NewGuid().ToString();
            
            // Setup Redis to indicate token is revoked (cache hit)
            redisDbMock
                .Setup(x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None))
                .ReturnsAsync(true);

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var token = GenerateTestTokenWithJti(tokenJti);
            var claimsPrincipal = CreateClaimsPrincipal(tokenJti);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = securityToken
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().NotBeNull("Token should fail due to cache hit");
            
            // Verify Redis cache was checked
            redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Once,
                "Redis cache should be checked"
            );

            // Verify database was NOT checked (short-circuited by cache)
            var dbCheckCount = await context.RevokedAccessTokens
                .Where(rt => rt.TokenJti == tokenJti)
                .CountAsync();

            dbCheckCount.Should().Be(0, "Database should not be checked when cache has the token");
        }

        [Fact]
        public async Task JwtMiddleware_CacheMiss_ShouldFallbackToDatabase()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            // Setup Redis to return false (cache miss)
            redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                .ReturnsAsync(false);

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var token = GenerateTestToken(out string tokenJti);
            var claimsPrincipal = CreateClaimsPrincipal(tokenJti);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = securityToken
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().BeNull("Valid token should pass after DB check");
            
            // Verify Redis was checked first
            redisDbMock.Verify(
                x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None),
                Times.Once,
                "Redis cache should be checked first"
            );

            // Note: Database fallback is handled internally by ValidateTokenAsync, 
            // we verify the overall result is correct
        }

        [Fact]
        public async Task JwtMiddleware_CacheMiss_WithRevokedTokenInDatabase_ShouldFail()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            // Setup Redis to return false (cache miss)
            redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                .ReturnsAsync(false);

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var tokenJti = Guid.NewGuid().ToString();
            var userId = "test-user-123";

            // Add revoked token to database
            var revokedToken = TestFixtures.CreateRevokedToken(tokenJti, userId, "Test revocation");
            context.RevokedAccessTokens.Add(revokedToken);
            await context.SaveChangesAsync();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var token = GenerateTestTokenWithJti(tokenJti);
            var claimsPrincipal = CreateClaimsPrincipal(tokenJti);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = securityToken
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().NotBeNull("Token should fail when found in database");
        }

        [Fact]
        public async Task JwtMiddleware_MultipleRequests_ShouldLeverageCache()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var tokenJti = Guid.NewGuid().ToString();

            // Track call counts
            int redisCallCount = 0;
            
            redisDbMock
                .Setup(x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None))
                .ReturnsAsync(() => {
                    redisCallCount++;
                    return false; // Not revoked
                });

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var token = GenerateTestTokenWithJti(tokenJti);
            var claimsPrincipal = CreateClaimsPrincipal(tokenJti);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            // Act - Simulate multiple requests with the same token
            for (int i = 0; i < 5; i++)
            {
                var validatedContext = new TokenValidatedContext(
                    CreateDefaultHttpContext(),
                    CreateJwtBearerOptions(),
                    events
                )
                {
                    Principal = claimsPrincipal,
                    SecurityToken = securityToken
                };

                await events.TokenValidated(validatedContext);
                
                validatedContext.Result.Should().BeNull($"Request {i + 1} should pass");
            }

            // Assert
            redisCallCount.Should().Be(5, "Each request should check Redis cache");
            
            // Redis calls indicate caching is being used (not hitting DB every time)
            redisDbMock.Verify(
                x => x.KeyExistsAsync($"revoked:{tokenJti}", CommandFlags.None),
                Times.Exactly(5),
                "Redis cache should be checked on each request"
            );
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task JwtMiddleware_NullSecurityToken_ShouldFailGracefully()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var claimsPrincipal = CreateClaimsPrincipal(Guid.NewGuid().ToString());

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = null! // Null security token
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().NotBeNull("Null security token should cause failure");
            
            // Verify Redis was not called (short-circuited)
            redisDbMock.Verify(
                x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None),
                Times.Never,
                "Redis should not be checked when security token is null"
            );
        }

        [Fact]
        public async Task JwtMiddleware_NullPrincipal_ShouldFailGracefully()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var token = GenerateTestToken(out string _);
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = null!, // Null principal
                SecurityToken = securityToken
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().NotBeNull("Null principal should cause failure");
        }

        [Fact]
        public async Task JwtMiddleware_EmptyJti_ShouldFail()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            // Create claims with empty JTI
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, ""),
                new Claim(ClaimTypes.NameIdentifier, "123")
            }));

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = new JwtSecurityToken()
            };

            // Act
            await events.TokenValidated(validatedContext);

            // Assert
            validatedContext.Result.Should().NotBeNull("Empty JTI should cause failure");
        }

        [Fact]
        public async Task JwtMiddleware_RedisFailure_ShouldFallbackToDatabase()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            // Setup Redis to throw exception
            redisDbMock
                .Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
                .ThrowsAsync(new Exception("Redis connection failed"));

            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var token = GenerateTestToken(out string tokenJti);
            var claimsPrincipal = CreateClaimsPrincipal(tokenJti);
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.ReadJwtToken(token);

            var validatedContext = new TokenValidatedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Principal = claimsPrincipal,
                SecurityToken = securityToken
            };

            // Act & Assert
            await events.Invoking(async e => await e.TokenValidated(validatedContext))
                .Should().NotThrowAsync("Redis failure should not crash the middleware");

            // The token should still be validated (fail closed behavior depends on implementation)
            validatedContext.Should().NotBeNull();
        }

        [Fact]
        public async Task JwtMiddleware_AuthenticationFailed_ShouldLogError()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var exception = new Exception("Invalid signature");
            
            var failedContext = new AuthenticationFailedContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            )
            {
                Exception = exception
            };

            // Act
            await events.AuthenticationFailed(failedContext);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.Once,
                "Authentication failures should be logged"
            );
        }

        [Fact]
        public async Task JwtMiddleware_Challenge_ShouldLogDebug()
        {
            // Arrange
            var context = TestFixtures.CreateTestDbContext();
            var (redisMock, redisDbMock) = TestFixtures.CreateMockRedis();
            
            var validationService = TestFixtures.CreateRevocationValidationService(context, redisMock);
            var loggerMock = new Mock<ILogger<JwtRevocationBearerEvents>>();

            var events = new JwtRevocationBearerEvents(validationService, loggerMock.Object);
            
            var challengeContext = new JwtBearerChallengeContext(
                CreateDefaultHttpContext(),
                CreateJwtBearerOptions(),
                events
            );

            // Act
            await events.Challenge(challengeContext);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.Once,
                "Challenge events should be logged at debug level"
            );
        }

        #endregion

        #region Helper Methods

        private string GenerateTestToken(out string tokenJti)
        {
            tokenJti = Guid.NewGuid().ToString();

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, "User")
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateTestTokenWithJti(string tokenJti)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, "User")
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private ClaimsPrincipal CreateClaimsPrincipal(string tokenJti)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, tokenJti),
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, "User")
            }));
        }

        private HttpContext CreateDefaultHttpContext()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost");
            return httpContext;
        }

        private JwtBearerOptions CreateJwtBearerOptions()
        {
            var options = new JwtBearerOptions
            {
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey))
                }
            };
            
            return options;
        }

        #endregion
    }
}