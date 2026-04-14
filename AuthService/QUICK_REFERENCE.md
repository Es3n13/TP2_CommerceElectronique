# JWT Revocation Quick Reference

## Quick Start

### 1. Add Redis Connection String
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### 2. Build and Run
```bash
cd AuthService
dotnet restore
dotnet build
dotnet run
```

### 3. Test Revocation
```bash
# Generate token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"userId":123,"email":"user@example.com","pseudo":"testuser","role":"User"}' \
  | jq -r '.token')

# Extract JTI
TOKEN_JTI=$(echo -n $TOKEN | cut -d. -f2 | base64 -d | jq -r '.jti')

# Revoke token
curl -X POST http://localhost:5000/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -d "{\"tokenJti\":\"$TOKEN_JTI\",\"userId\":\"123\"}"

# Try to use revoked token (will fail)
curl -X GET http://localhost:5000/api/protected/resource \
  -H "Authorization: Bearer $TOKEN"
```

## Key Files

| File | Purpose |
|------|---------|
| `Services/JwtRevocationValidationService.cs` | Core validation service |
| `Middleware/JwtRevocationBearerEvents.cs` | Automatic validation on all requests |
| `Attributes/RequireNonRevokedTokenAttribute.cs` | Optional explicit validation |
| `Program.cs` | Service registration (lines 35-60) |
| `Services/TokenService.cs` | JTI extraction methods |
| `Services/RevokedAccessTokenService.cs` | Revocation with Redis caching |

## API Endpoints

### Revoke Token
```http
POST /api/TokenRevocation/revoke
Content-Type: application/json

{
  "tokenJti": "uuid",
  "userId": "123",
  "reason": "User logged out"
}
```

### Check Token Status
```http
GET /api/TokenRevocation/check/{tokenJti}
```

### Revoke All User Tokens
```http
POST /api/TokenRevocation/revoke-all/{userId}
```

### Cleanup Expired Tokens
```http
POST /api/TokenRevocation/cleanup
```

## Redis Keys

**Pattern**: `revoked:{jti}`
**Value**: `"true"`
**TTL**: `token_expiration - current_time`

## Check Redis

```bash
# Check if token is revoked
redis-cli GET "revoked:{jti}"

# Get TTL
redis-cli TTL "revoked:{jti}"

# List all revoked tokens
redis-cli KEYS "revoked:*"

# Clear all revoked tokens
redis-cli KEYS "revoked:*" | xargs redis-cli DEL
```

## Usage Examples

### Automatic Validation (Default)
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProtectedController : ControllerBase
{
    // All endpoints automatically validated
}
```

### Explicit Validation (Optional)
```csharp
[ApiController]
[Route("api/[controller]")]
[RequireNonRevokedToken]  // Double-check
public class SensitiveController : ControllerBase
{
    [HttpGet("balance")]
    public IActionResult GetBalance()
    {
        // Extra security for sensitive operations
        return Ok(new { balance = 1000.00 });
    }
}
```

## Troubleshooting

### All requests rejected
```bash
# Check Redis status
redis-cli ping

# Check logs
tail -f logs/production.log | grep "Token.*validation"
```

### Performance degraded
```bash
# Check Redis latency
redis-cli --latency

# Check cache hit rate in logs
grep "Found in revocation cache" logs/production.log | wc -l
```

### Revoked token still accepted
```bash
# Verify in database
sqlcmd -Q "SELECT * FROM RevokedAccessTokens WHERE TokenJti = '{jti}'"

# Verify in cache
redis-cli GET "revoked:{jti}"
```

## Logs

### Debug Level
```
Validating token {jti}
Token {jti} is valid
Token {jti} found in revocation cache
```

### Info Level
```
Token {jti} is revoked
Successfully revoked token {jti}
Redis connection established at {connection_string}
```

### Warning Level
```
Redis cache unavailable, falling back to database
Failed to cache revoked token {jti} in Redis
```

### Error Level
```
Error validating token
Error checking token revocation status in database
```

## Performance Benchmarks

| Scenario | Latency |
|----------|---------|
| Cache hit | 1-2ms |
| Cache miss + DB | 5-10ms |
| Redis unavailable (DB only) | 5-10ms |
| Both unavailable (reject) | < 1ms |

## Security Checklist

- [ ] Redis connection string is secure
- [ ] TLS enabled for Redis in production
- [ ] JWT secret keys are rotated regularly
- [ ] Revocation logs are monitored
- [ ] Cleanup job scheduled for expired tokens
- [ ] Redis backup configured
- [ ] Database indexed on TokenJti column

## Documentation Links

- [Full Documentation](./JWT_REVOCATION_README.md)
- [Implementation Details](./IMPLEMENTATION_SUMMARY.md)
- [Architecture Diagram](./ARCHITECTURE_DIAGRAM.md)
- [Testing Guide](./TESTING_GUIDE.md)
- [Execution Summary](./EXECUTION_SUMMARY.md)

## Support

**Logs**: `/path/to/AuthService/logs/production.log`
**Redis**: `localhost:6379`
**Database**: `RevokedAccessTokens` table
**Cache Pattern**: `revoked:{jti}`

---

*Version: 1.0 | Last Updated: 2026-04-05*