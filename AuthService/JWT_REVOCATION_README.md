# JWT Token Revocation System

## Overview

This implementation provides a production-ready JWT token revocation system that integrates with the existing authentication infrastructure. It uses a multi-layered approach with Redis caching for performance and database fallback for reliability.

## Architecture

### Components

1. **JwtRevocationValidationService** (`Services/JwtRevocationValidationService.cs`)
   - Core service for token validation against revocation list
   - Implements cache-first strategy with DB fallback
   - Provides caching methods for revoked tokens

2. **JwtRevocationBearerEvents** (`Middleware/JwtRevocationBearerEvents.cs`)
   - Custom JWT bearer authentication events
   - Automatically validates every authenticated request
   - Rejects revoked tokens before they reach controllers

3. **RequireNonRevokedTokenAttribute** (`Attributes/RequireNonRevokedTokenAttribute.cs`)
   - Optional attribute for explicit revocation checking
   - Can be applied to controllers or individual actions
   - Useful for sensitive endpoints or testing

4. **Enhanced RevokedAccessTokenService**
   - Integrated with Redis for cached revocation entries
   - Automatic TTL calculation based on token expiration
   - Graceful degradation when Redis is unavailable

5. **Enhanced TokenService**
   - Extracts JTI (JWT ID) from tokens
   - Extracts token expiration for TTL calculation
   - Required for revocation validation

## How It Works

### Token Generation

1. Generate token with JTI claim (already implemented in `TokenService`)
2. Store JTI in token claims for later identification
3. Return token to client

### Token Revocation

1. Extract JTI from the token to be revoked
2. Insert revocation record into database (`RevokedAccessTokens` table)
3. Cache revoked JTI in Redis with TTL = (token_expiration - now)
4. Both layers store the same information

### Token Validation (Automatic)

1. Client sends JWT in Authorization header
2. JWT bearer middleware validates standard claims (issuer, audience, signature, expiration)
3. **NEW**: `JwtRevocationBearerEvents.TokenValidated` triggers
4. Extract JTI from validated token claims
5. Check Redis cache first (fast path)
   - If found in cache → Token revoked → Reject request (401)
   - If not found → Continue to database check
6. Fallback to database check
   - If found in DB → Token revoked → Reject request (401)
   - If not found → Token valid → Allow request to continue
7. Request reaches controller with valid, non-revoked token

### Cache Strategy

**Redis Key Pattern**: `revoked:{jti}`
**Redis Value**: `"true"` (simple boolean flag)
**Redis TTL**: `token_expiration - current_time`

The TTL ensures:
- Cache entries automatically expire when token expires
- No manual cleanup required
- Memory-efficient storage

## Integration Guide

### Program.cs Setup

The following services are registered in `Program.cs`:

```csharp
// Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(...);

// Revocation validation service
builder.Services.AddScoped<IJwtRevocationValidationService, JwtRevocationValidationService>();

// JWT bearer with custom events
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.EventsType = typeof(JwtRevocationBearerEvents);
    });
```

### Controller Usage

**Automatic** (recommended for most cases):
```csharp
[ApiController]
[Route("api/[controller]")]
public class SensitiveController : ControllerBase
{
    // All endpoints automatically check for revocation
    // via JWtRevocationBearerEvents
}
```

**Explicit** (for sensitive endpoints or testing):
```csharp
[ApiController]
[Route("api/[controller]")]
[RequireNonRevokedToken]  // Double-check revocation
public class BankController : ControllerBase
{
    [HttpGet("balance")]
    public IActionResult GetBalance()
    {
        // Additional revocation check performed
        // on top of automatic check in middleware
    }
}
```

### Redis Configuration

Add to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AuthDbConnection": "Server=localhost;Database=AuthService;Integrated Security=True;",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "SecretKey": "your-secret-key",
    "Issuer": "AuthService",
    "Audience": "Services"
  }
}
```

**Graceful Degradation**:
- If Redis is unavailable, the system falls back to database-only validation
- Operations continue with minimal performance impact
- Warning logs indicate Redis connectivity issues

### Database Schema

**RevokedAccessTokens** table (already exists):

```sql
CREATE TABLE RevokedAccessTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId NVARCHAR(256) NOT NULL,
    TokenJti NVARCHAR(512) NOT NULL,
    Reason NVARCHAR(MAX) NULL,
    RevokedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NOT NULL
);

-- Indexes for fast lookups
CREATE INDEX IX_RevokedAccessTokens_TokenJti ON RevokedAccessTokens(TokenJti);
CREATE INDEX IX_RevokedAccessTokens_UserId ON RevokedAccessTokens(UserId);
```

## API Endpoints

### Revocation Endpoints (Already Existing)

**Revoke Single Token:**
```http
POST /api/TokenRevocation/revoke
Content-Type: application/json

{
  "tokenJti": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "userId": "12345",
  "reason": "User logged out"
}
```

**Check Token Status:**
```http
GET /api/TokenRevocation/check/{tokenJti}
```

Response:
```json
{
  "isRevoked": true
}
```

**Revoke All User Tokens:**
```http
POST /api/TokenRevocation/revoke-all/{userId}
```

**Cleanup Expired Tokens:**
```http
POST /api/TokenRevocation/cleanup
```

## Error Handling

### Fail-Closed Strategy

The system implements a **fail-closed** approach:
- If validation fails (exception, unknown state) → **Reject token** (401)
- If Redis is unavailable → **Fallback to DB only** (continues with warning)
- If DB check fails → **Reject token** (401)

### Logging

Comprehensive logging at various levels:

**Debug**: Successful validations, cache hits
**Info**: Token revocation actions, validation failures
**Warning**: Redis unavailability, cache misses
**Error**: Database errors, critical validation failures

## Performance Considerations

### Redis Caching Benefits

1. **Sub-millisecond lookups**: Redis provides O(1) complexity for key existence checks
2. **Reduced database load**: Most revocation checks hit cache instead of DB
3. **Automatic expiration**: TTL cleanup prevents memory bloat
4. **Scalability**: Redis handles high concurrency better than database queries

### Performance Metrics (Expected)

- **Cache hit rate**: 95%+ (assuming revoked tokens are checked repeatedly)
- **Average latency**: 1-2ms (Redis) vs 5-10ms (SQL Server)
- **Database queries reduced**: ~95% reduction in revocation checks

## Security Features

1. **Immediate revocation**: Revoked tokens are rejected immediately (no waiting for expiration)
2. **Multi-layer validation**: Both cache and DB checked; evasion requires compromising both
3. **Fail-closed**: Unknown states result in rejection, not approval
4. **Comprehensive logging**: All revocation actions and validation attempts logged
5. **TTL enforcement**: Cache entries expire with tokens, preventing stale entries

## Testing Strategy

### Manual Testing

1. **Generate token**:
   ```http
   POST /api/auth/token
   {
     "userId": 123,
     "email": "user@example.com",
     "pseudo": "testuser",
     "role": "User"
   }
   ```

2. **Extract JTI** from response token (decode JWT)

3. **Make authenticated request** (should succeed):
   ```http
   GET /api/protected/resource
   Authorization: Bearer {token}
   ```

4. **Revoke token**:
   ```http
   POST /api/TokenRevocation/revoke
   {
     "tokenJti": "{extracted_jti}",
     "userId": "123"
   }
   ```

5. **Make authenticated request again** (should fail with 401):
   ```http
   GET /api/protected/resource
   Authorization: Bearer {token}
   ```
   Response: `401 Unauthorized - "Token has been revoked"`

### Unit Testing

Test cases to cover:
- Valid token passes validation
- Revoked token rejected
- Cache hit scenario
- Cache miss with DB fallback
- Redis unavailable (graceful degradation)
- Invalid JTI format
- Missing JTI claim
- Database errors (fail-closed)

## Deployment Checklist

- [ ] Add `StackExchange.Redis` NuGet package (completed)
- [ ] Update `appsettings.json` with Redis connection string
- [ ] Ensure Redis server is running (optional, graceful degradation)
- [ ] Run database migrations (schema already exists)
- [ ] Test revocation flow end-to-end
- [ ] Monitor Redis connection health in logs
- [ ] Set up Redis monitoring/alerting
- [ ] Configure Redis backup/replication for production

## Troubleshooting

### "Redis connection failed" warnings

**Symptom**: Warning logs about Redis unavailability

**Solution**:
- Check Redis server is running: `redis-cli ping`
- Verify connection string in `appsettings.json`
- Ensure network connectivity to Redis host
- **Note**: System continues to function with DB fallback

### Tokens are rejected unexpectedly

**Symptoms**: Valid tokens being rejected as revoked

**Troubleshooting**:
1. Check `RevokedAccessTokens` table for false positives
2. Verify JTI extraction (decode token and check jti claim)
3. Check logs for validation errors
4. Test with `/api/TokenRevocation/check/{jti}` endpoint

### Performance degradation

**Symptoms**: Slower authentication after implementation

**Troubleshooting**:
1. Check Redis connection health
2. Monitor cache hit rate in logs
3. Verify database indexes exist on `TokenJti` column
4. Consider increasing Redis cache size if memory-constrained

## Maintenance

### Routine Tasks

**Daily** (optional):
- Monitor Redis memory usage
- Check for authentication errors in logs

**Weekly**:
- Review revocation patterns for abuse
- Clean up expired tokens (endpoint exists)

**Monthly**:
- Audit RevokedAccessTokens table size
- Review Redis performance metrics

### Automated Cleanup

Use the provided cleanup endpoint:

```http
POST /api/TokenRevocation/cleanup
Authorization: Bearer {admin_token}
```

Or schedule as a cron job:
```bash
0 2 * * * curl -X POST http://localhost:5000/api/TokenRevocation/cleanup -H "Authorization: Bearer {admin_token}"
```

## Future Enhancements

Potential improvements:
1. **Batch revocation**: Batch-mode for revoking multiple tokens
2. **Revocation reasons**: Enhanced categorization (logout, security, compliance)
3. **Dashboard**: Admin UI for monitoring revocations
4. **Metrics**: Prometheus/Grafana integration for monitoring
5. **Distributed cache**: Redis Cluster for high availability
6. **Token family tracking**: Track token families for selective revocation

## References

- JWT RFC: https://tools.ietf.org/html/rfc7519
- JWT Bearer Auth: https://tools.ietf.org/html/rfc6750
- Redis Documentation: https://redis.io/documentation
- ASP.NET Core Authentication: https://docs.microsoft.com/aspnet/core/security/authentication/