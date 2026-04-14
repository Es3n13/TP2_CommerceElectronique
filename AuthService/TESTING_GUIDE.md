# JWT Revocation Testing Guide

## Test Scenarios

### Scenario 1: Valid Token - Should Pass

#### Request: Generate Token
```bash
curl -X POST http://localhost:5000/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 123,
    "email": "user@example.com",
    "pseudo": "testuser",
    "role": "User"
  }'
```

#### Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "user@example.com",
  "pseudo": "testuser",
  "role": "User",
  "expiration": "2026-04-05T10:30:00Z"
}
```

#### Action: Extract JTI
Use https://jwt.io/ or decode the token to get the JTI claim:
```
jti: "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
```

#### Request: Access Protected Endpoint (Should Succeed)
```bash
curl -X GET http://localhost:5000/api/protected/resource \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

#### Expected Result: `200 OK` ✓

---

### Scenario 2: Revoke Token - Should Reject

#### Request: Revoke the Token
```bash
curl -X POST http://localhost:5000/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -d '{
    "tokenJti": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "userId": "123",
    "reason": "User logged out"
  }'
```

#### Expected Result:
```json
{
  "message": "Token revoked successfully."
}
```

#### Action: Verify Revocation in Cache (Optional)
```bash
redis-cli GET "revoked:a1b2c3d4-e5f6-7890-abcd-ef1234567890"
# Expected: "true"
```

#### Request: Access Protected Endpoint (Should Fail)
```bash
curl -X GET http://localhost:5000/api/protected/resource \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

#### Expected Result:
```json
{
  "error": "invalid_token",
  "error_description": "Token has been revoked"
}
```
Status: `401 Unauthorized` ✓

---

### Scenario 3: Check Token Status Endpoint

#### Request: Check Token Status
```bash
curl -X GET http://localhost:5000/api/TokenRevocation/check/\
a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

#### Expected Response (Revoked):
```json
{
  "isRevoked": true
}
```

---

### Scenario 4: Revoke All User Tokens

#### Request: Revoke All Tokens for User
```bash
curl -X POST http://localhost:5000/api/TokenRevocation/revoke-all/123
```

#### Expected Result:
```json
{
  "message": "All tokens revoked successfully."
}
```

#### Notes:
- Revokes all refresh tokens for the user
- Updates `RevokedAccessTokens` table
- Caches all revoked JTIs in Redis

---

### Scenario 5: Graceful Degradation (Redis Down)

#### Setup: Stop Redis
```bash
redis-cli SHUTDOWN
# or
sudo systemctl stop redis
```

#### Request: Make Authenticated Request
```bash
curl -X GET http://localhost:5000/api/protected/resource \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

#### Expected Result:
- Valid, non-revoked tokens → `200 OK` (DB fallback works)
- Revoked tokens → `401 Unauthorized` (DB check still works)

#### Logs Should Show:
```
Warning: Redis cache unavailable, falling back to database for token {jti}
```

---

### Scenario 6: Invalid JTI Format

#### Request: Revoke Token with Invalid JTI
```bash
curl -X POST http://localhost:5000/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -d '{
    "tokenJti": "",
    "userId": "123"
  }'
```

#### Expected Result:
```json
{
  "message": "Token JTI is required."
}
```
Status: `400 Bad Request` ✓

---

### Scenario 7: Token Expiration After TTL

#### Request: Revoke Token Near Expiration
```bash
curl -X POST http://localhost:5000/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -d '{
    "tokenJti": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "userId": "123"
  }'
```

#### Action: Wait for Token to Expire
Wait for the token's expiration time (30 minutes for access tokens)

#### Request: Check Cache After Expiration
```bash
redis-cli GET "revoked:a1b2c3d4-e5f6-7890-abcd-ef1234567890"
# Expected: (nil) - TTL expired, key automatically removed
```

#### Notes:
- Redis automatically removes expired cache entries
- Database `RevokedAccessTokens` table still has the record
- Cleanup endpoint should be run periodically (see Scenario 8)

---

### Scenario 8: Cleanup Expired Tokens

#### Request: Cleanup Expired Revoked Tokens
```bash
curl -X POST http://localhost:5000/api/TokenRevocation/cleanup
```

#### Expected Result:
```json
{
  "message": "Expired tokens cleaned up successfully."
}
```

#### Notes:
- Removes expired entries from `RevokedAccessTokens` table
- Redis caches already auto-expired via TTL
- Should be scheduled as a periodic maintenance task

---

### Scenario 9: Explicit Validation Attribute

#### Create Test Controller
```csharp
[ApiController]
[Route("api/[controller]")]
[RequireNonRevokedToken]  // Explicit validation
public class BankController : ControllerBase
{
    [HttpGet("balance")]
    public IActionResult GetBalance()
    {
        return Ok(new { balance: 1000.00 });
    }
}
```

#### Request: Access Protected Endpoint
```bash
curl -X GET http://localhost:5000/api/Bank/balance \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

#### Notes:
- Token validated twice: middleware + attribute
- Useful for highly sensitive operations (banking, admin)
- Adds negligible overhead (second cache check is fast)

---

## Performance Testing

### Test Cache Performance
```bash
# Generate a token and revoke it
TOKEN_JTI="a1b2c3d4-e5f6-7890-abcd-ef1234567890"

# Benchmark cache lookups (1000 requests)
time for i in {1..1000}; do
  curl -s -X GET "http://localhost:5000/api/TokenRevocation/check/$TOKEN_JTI" > /dev/null
done
```

#### Expected Results:
- **With Redis**: ~1-2 seconds total (1-2ms per request)
- **Without Redis**: ~5-10 seconds total (5-10ms per request)

---

## Integration Testing

### Test End-to-End Flow
```bash
#!/bin/bash

# 1. Generate token
echo "Step 1: Generating token..."
TOKEN_RESPONSE=$(curl -s -X POST http://localhost:5000/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"userId":123,"email":"user@example.com","pseudo":"testuser","role":"User"}')

TOKEN=$(echo $TOKEN_RESPONSE | jq -r '.token')
TOKEN_JTI=$(echo -n $TOKEN | cut -d. -f2 | base64 -d | jq -r '.jti')

echo "Token generated. JTI: $TOKEN_JTI"

# 2. Make authenticated request (should succeed)
echo "Step 2: Testing valid token..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X GET http://localhost:5000/api/protected/resource \
  -H "Authorization: Bearer $TOKEN")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)

if [ "$HTTP_CODE" = "200" ]; then
  echo "✓ Valid token accepted"
else
  echo "✗ Expected 200, got $HTTP_CODE"
fi

# 3. Revoke token
echo "Step 3: Revoking token..."
curl -s -X POST http://localhost:5000/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -d "{\"tokenJti\":\"$TOKEN_JTI\",\"userId\":\"123\"}"
echo ""

# 4. Try to use revoked token (should fail)
echo "Step 4: Testing revoked token..."
RESPONSE=$(curl -s -w "\n%{http_code}" -X GET http://localhost:5000/api/protected/resource \
  -H "Authorization: Bearer $TOKEN")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)

if [ "$HTTP_CODE" = "401" ]; then
  echo "✓ Revoked token rejected"
else
  echo "✗ Expected 401, got $HTTP_CODE"
fi

echo "Test completed!"
```

---

## Troubleshooting Tests

### Issue: All Requests Rejected

**Check 1**: Verify Redis is running
```bash
redis-cli ping
# Expected: PONG
```

**Check 2**: Check logs for errors
```bash
# Look for "Redis connection failed" warnings
tail -f /path/to/AuthService/logs/production.log
```

**Check 3**: Verify JTI extraction
```bash
# Decode JWT and check jti claim exists
echo "eyJ..." | cut -d. -f2 | base64 -d | jq '.jti'
```

### Issue: Revoked Token Still Accepted

**Check 1**: Verify revocation in DB
```sql
SELECT * FROM RevokedAccessTokens WHERE TokenJti = '{jti}';
```

**Check 2**: Verify cache entry
```bash
redis-cli GET "revoked:{jti}"
```

**Check 3**: Check logs for validation errors
```bash
# Look for token validation logs
grep "Token.*validation" /path/to/AuthService/logs/production.log
```

### Issue: Performance Degradation

**Check 1**: Redis connectivity
```bash
redis-cli --latency
# Expected: < 1ms latency
```

**Check 2**: Cache hit rate
```bash
# Monitor logs for cache hits vs misses
grep "Found in revocation cache" /path/to/AuthService/logs/production.log | wc -l
grep "isRevoked in database" /path/to/AuthService/logs/production.log | wc -l
```

---

## Test Cleanup

### Reset Test Data
```bash
# Clear all revocation cache entries
redis-cli KEYS "revoked:*" | xargs redis-cli DEL

# Clear database (use with caution!)
# DELETE FROM RevokedAccessTokens;
```

---

## Summary

✅ **All scenarios test**:
- Token validation
- Token revocation
- Cache performance
- Graceful degradation
- Explicit validation mode
- Error handling
- Cleanup operations

**Next Steps**:
1. Run these tests locally
2. Document results
3. Set up automated tests
4. Monitor in production