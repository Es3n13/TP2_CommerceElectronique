# JWT Revocation Feature - Production Documentation

**Version:** 1.0.0  
**Release Date:** April 5, 2026  
**Component:** AuthService  
**Status:** Production Ready ✅

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Security Justification](#security-justification)
3. [Architecture](#architecture)
4. [Data Model](#data-model)
5. [Flow Diagrams](#flow-diagrams)
6. [Configuration Guide](#configuration-guide)
7. [API Documentation](#api-documentation)
8. [Cache Behavior & Performance](#cache-behavior--performance)
9. [Deployment Guide](#deployment-guide)
10. [Troubleshooting](#troubleshooting)
11. [Testing](#testing)
12. [Maintenance](#maintenance)

---

## Overview

The JWT Revocation Feature provides immediate token invalidation capabilities for the e-commerce platform's authentication system. It implements a **multi-layered revocation strategy** using Redis caching for performance and SQL Server for reliability, with automatic validation on every authenticated request.

### Key Capabilities

✅ **Immediate revocation** - Tokens are invalidated instantly, no waiting for expiration  
✅ **Automatic validation** - Every JWT is checked against the revocation list automatically  
✅ **High performance** - Redis caching provides sub-millisecond lookups  
✅ **Graceful degradation** - System continues functioning if Redis is unavailable  
✅ **Comprehensive logging** - All revocation actions and validations are logged  
✅ **Fail-closed security** - Unknown states result in token rejection  
✅ **Multiple revocation modes** - Revoke single token, all user tokens, or by reason

### Use Cases

- **Password changes** - Invalidate all user tokens after password reset
- **User logout** - Revoke specific token on explicit logout
- **Security incidents** - Immediately invalidate compromised tokens
- **Account deletion** - Revoke all tokens for deleted accounts
- **Role changes** - Force re-authentication when user roles change
- **Suspicious activity** - Revoke tokens from unusual locations/times

---

## Security Justification

### Why JWT Revocation is Critical

**Problem:** Standard JWTs are stateless and valid until expiration. Once issued, they cannot be invalided by the server.

**Impact Without Revocation:**
- Compromised tokens remain valid until expiration (default: 30 minutes)
- Password changes don't invalidate existing sessions
- Cannot respond to security incidents in real-time
- Forceful logout requires waiting for token expiration

### Security Benefits

#### 1. **Real-Time Threat Response**
- Compromised tokens are rejected immediately
- Security teams can revoke tokens during investigations
- Reduces attacker time window from minutes/hours to milliseconds

#### 2. **Compliance Requirements**
- **GDPR Article 32**: "Ability to ensure the ongoing confidentiality, integrity, availability and resilience of processing systems and services"
- **PCI DSS 3.2**: "Implement processes to immediately revoke access for terminated users"
- **OWASP A7**: "Identification and Authentication Failures" - Session revocation is a best practice

#### 3. **Defense in Depth**
- Multiple layers: Redis cache + Database validation + Application logic
- Fail-closed approach: Unknown states = rejection
- Comprehensive audit trail via logging

#### 4. **Operational Security**
- Enable/disable user access without server restart
- Force password resets with immediate token invalidation
- Respond to credential stuffing attacks in real-time

### Security Model

```
┌─────────────────────────────────────────────────────────────┐
│                   Security Layers                            │
├─────────────────────────────────────────────────────────────┤
│ Layer 1: JWT Signature Validation (existing)                 │
│ Layer 2: JWT Expiration Check (existing)                     │
│ Layer 3: Revocation Cache Check (Redis - NEW)                │
│ Layer 4: Revocation Database Check (SQL Server - NEW)        │
│ Layer 5: Application Authorization (existing)                │
└─────────────────────────────────────────────────────────────┘
```

---

## Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                        Client Application                       │
│                        (Browser/Mobile)                         │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             │ HTTP Request with JWT
                             │ Authorization: Bearer eyJhbGci...
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Api Gateway (Ocelot)                      │
│                        - Route Validation                       │
│                        - JWT Signature Check                    │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             │ Routed Request
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      AuthService                                │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │         JWT Bearer Authentication Middleware             │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │       JwtRevocationBearerEvents (Custom)           │  │  │
│  │  │  • TokenValidated event (automatic check)          │  │  │
│  │  │  • Calls RevocationValidationService                │  │  │
│  │  │  • Rejects if JTI found in revocation list          │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
│                          │                                       │
│                          ▼                                       │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │       JwtRevocationValidationService (Core Logic)         │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │         Redis Cache (Primary)                       │  │  │
│  │  │         Key: revoked:{jti}                          │  │  │
│  │  │         TTL: token_expiration - now                 │  │  │
│  │  │         Lookup: O(1) sub-millisecond                │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  │                              ↓                              │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │         SQL Server (Fallback)                       │  │  │
│  │  │         Table: RevokedAccessTokens                  │  │  │
│  │  │         Index: TokenJti (for fast lookup)           │  │  │
│  │  │         Query: SELECT WHERE TokenJti = ?            │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
│                          │                                       │
│                          ▼                                       │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │        Controller (Protected Endpoint)                   │  │
│  │        - Business logic executed only if token valid     │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Primary Files |
|-----------|---------------|---------------|
| **JwtRevocationBearerEvents** | JWT middleware integration, automatic validation on every request | `Middleware/JwtRevocationBearerEvents.cs` |
| **JwtRevocationValidationService** | Core validation logic, cache-first strategy | `Services/JwtRevocationValidationService.cs` |
| **RevokedAccessTokenService** | Token revocation, caching, database operations | `Services/RevokedAccessTokenService.cs` |
| **TokenRevocationController** | API endpoints for revocation operations | `Controllers/TokenRevocationController.cs` |
| **RevokedAccessToken** | Data model for revoked tokens | `Data/RevokedAccessToken.cs` |
| **Redis** | High-performance caching layer | External service |
| **SQL Server** | Persistent revocation storage | External database |

### Technology Stack

- **.NET 10.0** - Framework
- **ASP.NET Core Authentication** - JWT middleware
- **StackExchange.Redis** - Redis client library
- **Entity Framework Core** - Database ORM
- **SQL Server** - Persistent storage

---

## Data Model

### Database Schema

#### Table: RevokedAccessTokens

```sql
CREATE TABLE RevokedAccessTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId NVARCHAR(256) NOT NULL,
    TokenJti NVARCHAR(512) NOT NULL,
    Reason NVARCHAR(MAX) NULL,
    RevokedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NOT NULL,
    CONSTRAINT UQ_RevokedAccessTokens_TokenJti UNIQUE (TokenJti)
);

-- Performance Indexes
CREATE INDEX IX_RevokedAccessTokens_TokenJti ON RevokedAccessTokens(TokenJti);
CREATE INDEX IX_RevokedAccessTokens_UserId ON RevokedAccessTokens(UserId);
CREATE INDEX IX_RevokedAccessTokens_ExpiresAt ON RevokedAccessTokens(ExpiresAt);

-- Cleanup Index
CREATE INDEX IX_RevokedAccessTokens_RevokedAt ON RevokedAccessTokens(RevokedAt);
```

#### Entity Model

```csharp
public class RevokedAccessToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TokenJti { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime RevokedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

#### Fields Description

| Field | Type | Description | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | Primary key | Auto-generated |
| `UserId` | `string` (256) | User identifier | From JWT subject claim |
| `TokenJti` | `string` (512) | JWT ID claim | Unique identifier for token |
| `Reason` | `string?` | Revocation reason | Optional (logout, security, etc.) |
| `RevokedAt` | `DateTime` | Revocation timestamp | UTC timezone |
| `ExpiresAt` | `DateTime` | Original token expiration | Used for cleanup |

### Redis Data Structure

#### Key Pattern

```
revoked:{jti}
```

#### Value

```
"true"
```

#### TTL (Time To Live)

```
token_expiration - current_time
```

**Example:**
- Token expires at: `2026-04-05 15:30:00 UTC`
- Current time: `2026-04-05 15:00:00 UTC`
- TTL: `1800 seconds` (30 minutes)

#### Redis Commands

```
# Check if token is revoked
EXISTS revoked:a1b2c3d4-e5f6-7890-abcd-ef1234567890
# Returns: 1 (exists/revoked) or 0 (not revoked)

# Revoke token
SETEX revoked:a1b2c3d4-e5f6-7890-abcd-ef1234567890 1800 "true"
# Sets key with 1800 second TTL

# Automatic cleanup
# Redis automatically expires keys after TTL
```

---

## Flow Diagrams

### Password Change Flow (Revoke All User Tokens)

```
┌──────────┐
│   User   │
└─────┬────┘
      │
      │ 1. POST /api/users/change-password
      │    { oldPassword, newPassword }
      ▼
┌─────────────────────┐
│   UserService       │
│ - Validate old pwd  │
│ - Hash new pwd      │
│ - Update DB         │
└─────┬───────────────┘
      │
      │ 2. POST /api/TokenRevocation/revoke-all/{userId}
      ▼
┌─────────────────────────────────────┐
│  TokenRevocationController         │
│  - Calls RevokeAllUserTokensAsync  │
└─────┬───────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────┐
│     RevokedAccessTokenService       │
│                                     │
│  ┌───────────────────────────────┐  │
│  │ 1. Get all non-expired tokens  │  │
│  │    for user from DB           │  │
│  │ 2. Insert revocation records   │  │
│  │ 3. Cache each in Redis         │  │
│  │    with appropriate TTL       │  │
│  │ 4. Log revocation              │  │
│  └───────────────────────────────┘  │
└─────┬───────────────────────────────┘
      │
      │ Response: 200 OK
      │ { Message: "All tokens revoked" }
      ▼
┌──────────┐
│   User   │
└──────────┘

┌─────────────────────────────────────────────────────────────┐
│                     Subsequent Requests                       │
├─────────────────────────────────────────────────────────────┤
│  3. User makes authenticated request                        │
│     GET /api/reservations                                    │
│     Authorization: Bearer {old_token}                        │
│     ↓                                                        │
│  4. JWT middleware validates signature ✓                     │
│  5. JwtRevocationBearerEvents checks revocation              │
│     → Redis lookup: found in revocation list                 │
│     → Return 401 Unauthorized                                │
│     → Message: "Token has been revoked"                      │
│     ↓                                                        │
│  6. User must login again to get new token                   │
└─────────────────────────────────────────────────────────────┘
```

### Token Validation Flow (Every Authenticated Request)

```
┌──────────────┐
│    Client    │
│  (Browser)   │
└──────┬───────┘
       │
       │ HTTP Request
       │ Authorization: Bearer eyJhbGci...
       │
       ▼
┌────────────────────────────────────────────┐
│           Api Gateway (Ocelot)             │
│  - Route validation                        │
│  - JWT signature verification ✓            │
│  - JWT expiration check ✓                  │
└──────┬─────────────────────────────────────┘
       │ Token passed standard validation
       ▼
┌────────────────────────────────────────────┐
│       JwtRevocationBearerEvents            │
│  (Called on TokenValidated event)          │
└──────┬─────────────────────────────────────┘
       │
       ▼
┌────────────────────────────────────────────┐
│  JwtRevocationValidationService            │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │ Step 1: Extract JTI from token       │  │
│  │   jti = claims["jti"]                │  │
│  └──────────────┬───────────────────────┘  │
│                 │                          │
│                 ▼                          │
│  ┌──────────────────────────────────────┐  │
│  │ Step 2: Check Redis Cache            │  │
│  │   EXISTS revoked:{jti}               │  │
│  │   ↓                                  │  │
│  │   IF found → Return FALSE (revoked)  │  │
│  │   IF not found → Continue            │  │
│  └──────────────┬───────────────────────┘  │
│                 │                          │
│                 │ Cache miss/Redis down    │
│                 ▼                          │
│  ┌──────────────────────────────────────┐  │
│  │ Step 3: Fallback to Database         │  │
│  │   SELECT FROM RevokedAccessTokens    │  │
│  │   WHERE TokenJti = {jti}             │  │
│  │   AND ExpiresAt > NOW                │  │
│  │   ↓                                  │  │
│  │   IF found → Return FALSE (revoked)  │  │
│  │   IF not found → Return TRUE (valid) │  │
│  └──────────────┬───────────────────────┘  │
│                 │                          │
│                 ▼                          │
└─────────────────┼──────────────────────────┘
                  │
                  │ ValidationResult
                  ↓
        ┌─────────┴─────────┐
        │                   │
        FALSE (Revoked)     TRUE (Valid)
        │                   │
        ▼                   ▼
  ┌───────────┐     ┌──────────────────┐
  │  Return   │     │  Allow request   │
  │  401      │     │  to continue     │
  │  Unauthorized│                     │
  └───────────┘     └─────────┬────────┘
                             │
                             ▼
                    ┌──────────────────┐
                    │    Controller    │
                    │  Business Logic  │
                    └──────────────────┘
```

### Token Revocation Flow (Single Token)

```
┌──────────┐
│   User   │
└─────┬────┘
      │
      │ 1. POST /api/TokenRevocation/revoke
      │    { tokenJti, userId, reason }
      ▼
┌─────────────────────────────────────┐
│  TokenRevocationController         │
│  - Validates request parameters    │
└─────┬───────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────┐
│     RevokedAccessTokenService       │
│                                     │
│  ┌───────────────────────────────┐  │
│  │ Step 1: Decode JWT to extract │  │
│  │         expiresAt timestamp   │  │
│  │ Step 2: Calculate TTL        │  │
│  │         ttl = expiresAt - now │  │
│  │ Step 3: Insert into DB       │  │
│  │         RevokedAccessTokens  │  │
│  │ Step 4: Cache in Redis       │  │
│  │         SETEX revoked:{jti}  │  │
│  │              ttl "true"      │  │
│  │ Step 5: Log revocation       │  │
│  └───────────────────────────────┘  │
└─────┬───────────────────────────────┘
      │
      │ Response: 200 OK
      │ { Message: "Token revoked successfully" }
      ▼
┌──────────┐
│   User   │
└──────────┘
```

### Graceful Degradation Flow (Redis Unavailable)

```
┌────────────────────────────────────────────┐
│  Request arrives with JWT token            │
└──────┬─────────────────────────────────────┘
       │
       ▼
┌────────────────────────────────────────────┐
│ JwtRevocationValidationService             │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │ Step 1: Redis Cache Check           │  │
│  │   ↓                                 │  │
│  │   EXCEPTION: Redis connection failed│  │
│  │   ↓                                 │  │
│  │   LOG: Warning - Redis unavailable  │  │
│  │   ↓                                 │  │
│  │   RETURN: false (force DB fallback) │  │
│  └──────────────┬───────────────────────┘  │
│                 │                          │
│                 ▼                          │
│  ┌──────────────────────────────────────┐  │
│  │ Step 2: Database Fallback           │  │
│  │   SELECT FROM RevokedAccessTokens    │  │
│  │   WHERE TokenJti = {jti}             │  │
│  │   AND ExpiresAt > NOW                │  │
│  │   ↓                                 │  │
│  │   Returns result normally            │  │
│  │                                      │  │
│  │   LOG: Info - Validation via DB only│  │
│  └──────────────┬───────────────────────┘  │
└─────────────────┼──────────────────────────┘
                  │
                  │ Token validation continues
                  │ with DB-only strategy
                  ▼
          ┌───────────────┐
          │ Normal flow   │
          │ continues     │
          └───────────────┘
```

**Result:** System continues to validate tokens with minimal performance impact (5-10ms DB lookup vs 1-2ms Redis).

---

## Configuration Guide

### 1. Add NuGet Package

```bash
cd AuthService
dotnet add package StackExchange.Redis
```

### 2. Update appsettings.json

Add Redis connection string to `/AuthService/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AuthDbConnection": "Server=localhost;Database=AuthService;Integrated Security=True;TrustServerCertificate=True",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-at-least-256-bits-long",
    "Issuer": "https://localhost:6001",
    "Audience": "TP2CommerceElectronique",
    "ExpirationMinutes": 30
  },
  "Redis": {
    "EnableRetryOnConnect": true,
    "ConnectRetry": 3,
    "ConnectTimeout": 5000,
    "SyncTimeout": 5000
  }
}
```

### 3. Update Program.cs

Register services in `/AuthService/Program.cs`:

```csharp
using AuthService.Middleware;
using AuthService.Services;
using StackExchange.Redis;

// Add Redis connection (optional - graceful degradation if unavailable)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis");
    
    // Return null if not configured - system will use DB fallback
    if (string.IsNullOrEmpty(configuration))
    {
        return null!;
    }
    
    try
    {
        return ConnectionMultiplexer.Connect(configuration);
    }
    catch (Exception ex)
    {
        var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
        logger.LogWarning(ex, "Failed to connect to Redis. System will use database-only validation.");
        return null!;
    }
});

// Register revocation validation service
builder.Services.AddScoped<IJwtRevocationValidationService, JwtRevocationValidationService>();

// Configure JWT Bearer authentication with custom events
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]))
    };

    // Use custom events for automatic revocation validation
    options.EventsType = typeof(JwtRevocationBearerEvents);
});

// Register existing services
builder.Services.AddScoped<IRevokedAccessTokenService, RevokedAccessTokenService>();
```

### 4. Database Migration

Run migrations to create the `RevokedAccessTokens` table:

```bash
cd AuthService
dotnet ef migrations add AddRevokedAccessTokensTable
dotnet ef database update
```

Or manually execute SQL (see [Data Model](#data-model)).

### 5. Redis Installation (Optional)

Redis is optional - the system gracefully degrades to database-only validation.

#### Linux (Ubuntu/Debian)
```bash
sudo apt-get update
sudo apt-get install redis-server
sudo systemctl start redis
sudo systemctl enable redis
```

#### Windows (Docker)
```bash
docker run -d -p 6379:6379 redis:latest
```

#### macOS
```bash
brew install redis
brew services start redis
```

#### Test Connection
```bash
redis-cli ping
# Should return: PONG
```

### 6. Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Redis` | string | null | Redis connection string (host:port) |
| `EnableRetryOnConnect` | bool | true | Retry connection on failure |
| `ConnectRetry` | int | 3 | Number of connection retry attempts |
| `ConnectTimeout` | int | 5000 | Connection timeout in ms |
| `SyncTimeout` | int | 5000 | Synchronous operation timeout in ms |
| `Jwt:ExpirationMinutes` | int | 30 | Token lifetime (affects cache TTL) |

### 7. Environment-Specific Configuration

#### Development (appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

#### Production (appsettings.Production.json)
```json
{
  "ConnectionStrings": {
    "Redis": "redis-production.example.com:6380,ssl=true,password={password},abortConnect=false"
  }
}
```

#### Azure Cache for Redis
```json
{
  "ConnectionStrings": {
    "Redis": "{cache-name}.redis.cache.windows.net:6380,password={access-key},ssl=True,abortConnect=False"
  }
}
```

---

## API Documentation

### Authentication

All revocation endpoints require **admin privileges** or **user matching userId**.

### Endpoints

#### 1. Revoke Single Token

Revokes a specific JWT token by its JTI.

```http
POST /api/TokenRevocation/revoke
Content-Type: application/json
Authorization: Bearer {admin_token}

{
  "tokenJti": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "userId": "12345",
  "reason": "User logged out"
}
```

**Response:**
```json
{
  "message": "Token revoked successfully."
}
```

**Status Codes:**
- `200 OK` - Token revoked successfully
- `400 Bad Request` - Invalid parameters (missing JTI or userId)
- `401 Unauthorized` - Authentication failed
- `403 Forbidden` - Insufficient permissions
- `500 Internal Server Error` - Server error

**Use Cases:**
- User explicit logout
- Revoke suspicious token
- Specific session termination

---

#### 2. Check Token Status

Check if a specific token is revoked.

```http
GET /api/TokenRevocation/check/a1b2c3d4-e5f6-7890-abcd-ef1234567890
Authorization: Bearer {admin_token}
```

**Response (Revoked):**
```json
{
  "isRevoked": true
}
```

**Response (Not Revoked):**
```json
{
  "isRevoked": false
}
```

**Status Codes:**
- `200 OK` - Check completed
- `400 Bad Request` - Invalid JTI format
- `401 Unauthorized` - Authentication failed
- `500 Internal Server Error` - Server error

**Use Cases:**
- Debugging authentication issues
- Token validation testing
- Audit verification

---

#### 3. Revoke All User Tokens

Revokes all active tokens for a specific user.

```http
POST /api/TokenRevocation/revoke-all/12345
Authorization: Bearer {admin_token}
```

**Response:**
```json
{
  "message": "All tokens revoked successfully."
}
```

**Status Codes:**
- `200 OK` - All tokens revoked
- `400 Bad Request` - Invalid userId
- `401 Unauthorized` - Authentication failed
- `403 Forbidden` - Insufficient permissions
- `500 Internal Server Error` - Server error

**Use Cases:**
- Password change
- Account compromise response
- User suspension/deletion
- Role change enforcement

---

#### 4. Cleanup Expired Tokens

Remove expired revocation records from database. Useful for maintenance.

```http
POST /api/TokenRevocation/cleanup
Authorization: Bearer {admin_token}
```

**Response:**
```json
{
  "message": "Expired tokens cleaned up successfully."
}
```

**Status Codes:**
- `200 OK` - Cleanup completed
- `401 Unauthorized` - Authentication failed
- `403 Forbidden` - Insufficient permissions
- `500 Internal Server Error` - Server error

**Use Cases:**
- Scheduled maintenance (cron job)
- Database optimization
- Cleanup after high-volume revocation events

---

### Example Workflows

#### Scenario 1: Password Change Flow

```bash
# 1. User changes password (UserService endpoint)
curl -X POST http://localhost:8080/api/users/change-password \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {user_token}" \
  -d '{
    "oldPassword": "@OldPassword123",
    "newPassword": "@NewPassword456"
  }'

# 2. Revoke all user tokens (automatic via API Gateway or service call)
curl -X POST http://localhost:8080/api/TokenRevocation/revoke-all/12345 \
  -H "Authorization: Bearer {admin_token}"

# 3. User must login again to get new token
curl -X POST http://localhost:8080/api/users/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "@NewPassword456"
  }'
```

#### Scenario 2: Security Incident Response

```bash
# 1. Identify suspicious user ID (from logs/monitoring)
USER_ID="12345"

# 2. Immediately revoke all tokens
curl -X POST http://localhost:8080/api/TokenRevocation/revoke-all/$USER_ID \
  -H "Authorization: Bearer {admin_token}" \
  -d '{
    "reason": "Security incident - suspicious activity detected"
  }'

# 3. Verify tokens are revoked (check specific JTI)
JTI="a1b2c3d4-e5f6-7890-abcd-ef1234567890"
curl -X GET http://localhost:8080/api/TokenRevocation/check/$JTI \
  -H "Authorization: Bearer {admin_token}"

# 4. Optional: Reset user password
curl -X POST http://localhost:8080/api/users/reset-password/$USER_ID \
  -H "Authorization: Bearer {admin_token}"
```

#### Scenario 3: User Logout with Token Revocation

```bash
# 1. Extract JTI from current token (decode JWT)
JTI=$(decode_jwt_token {user_token} | jq -r '.jti')

# 2. Revoke specific token
curl -X POST http://localhost:8080/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {user_token}" \
  -d "{
    \"tokenJti\": \"$JTI\",
    \"userId\": \"12345\",
    \"reason\": \"User logout\"
  }"

# 3. Subsequent requests with this token will fail (401)
curl -X GET http://localhost:8080/api/reservations \
  -H "Authorization: Bearer {user_token}"
# Response: 401 Unauthorized - "Token has been revoked"
```

---

## Cache Behavior & Performance

### Caching Strategy

The implementation uses a **cache-first** strategy with **automatic fallback**:

```
┌─────────────────────────────────────────────────────┐
│                  Cache Strategy                      │
├─────────────────────────────────────────────────────┤
│  1. Primary: Redis Cache (sub-millisecond)          │
│     - Key: revoked:{jti}                            │
│     - TTL: token_expiration - now                   │
│     - Complexity: O(1)                              │
│                                                      │
│  2. Secondary: SQL Server (fallback)                │
│     - Table: RevokedAccessTokens                    │
│     - Index: TokenJti (B-tree)                      │
│     - Complexity: O(log n)                          │
│                                                      │
│  3. Graceful Degradation:                           │
│     - Redis unreachable → DB only (logged)          │
│     - DB unreachable → Reject token (fail-closed)   │
└─────────────────────────────────────────────────────┘
```

### Performance Characteristics

| Operation | Average Latency | P99 Latency | Notes |
|-----------|-----------------|-------------|-------|
| **Redis Cache Hit** | 1-2 ms | 5 ms | Sub-millisecond with local Redis |
| **Redis Cache Miss** | 5-10 ms | 20 ms | Includes DB lookup |
| **DB-only (Redis down)** | 5-10 ms | 20 ms | Graceful degradation |
| **Token Revocation** | 10-20 ms | 50 ms | DB write + Redis cache |
| **Cleanup Expired** | 100-500 ms | 2 s | Depends on record count |

### Cache Hit Rate Analysis

**Expected Cache Hit Rate: 95%+**

**Why so high?**
- Revoked tokens are typically checked multiple times before expiration
- User may attempt 3-5 requests before realizing they need to re-login
- Redis TTL ensures entries remain valid for token lifetime
- Most revocation checks happen shortly after revocation event

**Example Scenario:**
- User's token revoked at 10:00 AM
- Token expires at 10:30 AM
- User attempts 5 requests between 10:00-10:05 AM
- All 5 requests hit Redis cache (0 database queries)
- Cache hit rate: 100% for this event

### Memory Usage Analysis

#### Redis Memory Estimation

```
Per Entry:
- Key: "revoked:{jti}" = ~40 bytes (avg JTI length)
- Value: "true" = 4 bytes
- Redis overhead: ~64 bytes (metadata)
- Total per entry: ~108 bytes

Examples:
- 1,000 revoked tokens: ~108 KB
- 10,000 revoked tokens: ~1.08 MB
- 100,000 revoked tokens: ~10.8 MB
- 1,000,000 revoked tokens: ~108 MB
```

**Note:** TTL ensures automatic cleanup, so memory usage follows token revocation rate.

### Performance Optimization Tips

#### 1. Index Optimization

Ensure database indexes exist:
```sql
-- Critical for performance
CREATE INDEX IX_RevokedAccessTokens_TokenJti 
ON RevokedAccessTokens(TokenJti);

-- Useful for user-specific queries
CREATE INDEX IX_RevokedAccessTokens_UserId 
ON RevokedAccessTokens(UserId);

-- Useful for cleanup operations
CREATE INDEX IX_RevokedAccessTokens_ExpiresAt 
ON RevokedAccessTokens(ExpiresAt);
```

#### 2. Redis Configuration

For production, tune Redis settings:

```redis
# redis.conf

# Max memory (adjust based on expected load)
maxmemory 256mb

# Eviction policy (allkeys-lru = evict least recently used keys)
maxmemory-policy allkeys-lru

# Persistence (optional - for caching only, can disable)
save ""  # Disable RDB snapshots
appendonly no  # Disable AOF

# Memory optimization
hash-max-ziplist-entries 512
hash-max-ziplist-value 64
```

#### 3. Connection Pooling

StackExchange.Redis provides built-in connection pooling. Ensure configuration includes:

```csharp
var config = ConfigurationOptions.Parse(connectionString);
config.AbortOnConnectFail = false;  // Don't abort on initial failure
config.ConnectRetry = 3;
config.ConnectTimeout = 5000;
config.SyncTimeout = 5000;
config.DefaultDatabase = 0;
```

#### 4. Batch Operations

For bulk revocation (e.g., revoke all user tokens), use batch Redis operations:

```csharp
// In RevokedAccessTokenService
var db = _redis.GetDatabase();
var tasks = new List<Task>();

foreach (var token in userTokens)
{
    var ttl = token.ExpiresAt - DateTime.UtcNow;
    tasks.Add(db.StringSetAsync($"revoked:{token.TokenJti}", "true", ttl));
}

await Task.WhenAll(tasks);
```

### Monitoring Metrics

Track these metrics for performance monitoring:

| Metric | Tool | Threshold |
|--------|------|-----------|
| **Cache Hit Rate** | Redis INFO | > 90% |
| **Cache Latency** | Redis LATENCY | < 5ms |
| **DB Query Time** | SQL Server Profiler | < 20ms |
| **Redis Memory Usage** | Redis INFO | < 80% maxmemory |
| **Revocation Failures** | Application Logs | 0 (critical) |
| **Redis Connection Errors** | Application Logs | < 1% of requests |

---

## Deployment Guide

### Deployment Checklist

#### Prerequisites

- [ ] .NET 10.0 Runtime installed on target server
- [ ] SQL Server instance running and accessible
- [ ] Redis server running (optional, but recommended)
- [ ] Appropriate network connectivity (firewall rules)
- [ ] Connection strings configured correctly

#### Pre-Deployment

- [ ] Test revocation flow in development environment
- [ ] Run unit tests: `dotnet test AuthService.Tests`
- [ ] Run integration tests: `dotnet test AuthService.Tests --filter "FullyQualifiedName~Integration"`
- [ ] Verify Redis connectivity: `redis-cli ping`
- [ ] Verify database connectivity: Test connection string
- [ ] Review logs for any warnings or errors
- [ ] Create database backup (before schema changes)

#### Deployment Steps

**Step 1: Deploy Application Code**
```bash
# Build for production
cd AuthService
dotnet publish -c Release -o /var/www/authservice

# Copy to server (example)
scp -r /var/www/authservice user@server:/var/www/authservice
```

**Step 2: Update Database Schema**
```bash
# Run migrations
dotnet ef database update --connection "ProductionConnectionString"

# Or run SQL script manually
sqlcmd -S server -d AuthDb -i migrations/AddRevokedAccessTokensTable.sql
```

**Step 3: Configure Environment**
```bash
# Update appsettings.Production.json
{
  "ConnectionStrings": {
    "AuthDbConnection": "Server=prod-db;Database=AuthService;...",
    "Redis": "prod-redis.example.com:6379,ssl=true,password={password}"
  }
}
```

**Step 4: Configure Redis (if needed)**
```bash
# Install Redis (if not already installed)
sudo apt-get install redis-server

# Configure redis.conf
sudo nano /etc/redis/redis.conf

# Restart Redis
sudo systemctl restart redis

# Verify
redis-cli ping
```

**Step 5: Deploy Service**
```bash
# Restart service (systemd example)
sudo systemctl restart authservice

# Check logs
sudo journalctl -u authservice -f
```

**Step 6: Health Check**
```bash
# Check service is running
curl http://localhost:6001/health

# Test revocation endpoint
curl -X GET http://localhost:6001/api/TokenRevocation/check/test-jti \
  -H "Authorization: Bearer {admin_token}"

# Expected: {"isRevoked": false}
```

#### Post-Deployment

- [ ] Verify authentication flow works (login → access protected endpoint)
- [ ] Test token revocation (revoke → verify 401 response)
- [ ] Monitor logs for warnings or errors
- [ ] Check Redis connection status
- [ ] Verify database indexes are used (query plan)
- [ ] Update monitoring dashboards
- [ ] Document deployment in CHANGELOG

### Deployment Strategies

#### Rolling Deployment (Recommended)

Deploy service instance by instance without downtime:

```bash
# Deploy to instance 1
ssh instance1
systemctl stop authservice
# Deploy code
systemctl start authservice
# Verify
curl http://localhost:6001/health

# Deploy to instance 2
ssh instance2
systemctl stop authservice
# Deploy code
systemctl start authservice
# Verify
curl http://localhost:6001/health
```

#### Blue-Green Deployment

Maintain two identical production environments:

```bash
# Deploy to Blue (canary)
ssh blue-server
# Deploy code
systemctl restart authservice

# Verify Blue
curl http://blue.example.com/health
curl http://blue.example.com/api/TokenRevocation/check/test

# Switch traffic (load balancer)
# Update DNS or load balancer config

# Retire Green (previous version)
```

### Rollback Procedure

If issues occur after deployment:

#### 1. Immediate Rollback

```bash
# Stop current version
ssh server
systemctl stop authservice

# Restore previous version
cd /var/www
mv authservice authservice.failed
mv authservice.backup authservice

# Restart with previous version
systemctl start authservice

# Verify
systemctl status authservice
curl http://localhost:6001/health
```

#### 2. Database Rollback (if schema changed)

```bash
# Only necessary if schema changed
dotnet ef database update previous-migration \
  --connection "ProductionConnectionString"

# Or restore from backup
sqlcmd -S server -d AuthDb -i backup/AuthDb_backup.sql
```

#### 3. Configuration Rollback

```bash
# Restore previous appsettings
cp appsettings.Production.json.backup appsettings.Production.json

# Restart service
systemctl restart authservice
```

### Azure Deployment

#### Deploy to Azure App Service

```bash
# Create resource group
az group create --name RG-AuthService --location eastus

# Create App Service
az webapp create --resource-group RG-AuthService \
  --name authservice-prod --plan ASP-AuthService --runtime "DOTNET|10.0"

# Configure connection strings
az webapp config connection-string set --resource-group RG-AuthService \
  --name authservice-prod \
  --settings AuthDbConnection="Server=tcp:prod-db.database.windows.net,1433;..." \
  --custom-name AuthDbConnection --type SQLAzure

az webapp config connection-string set --resource-group RG-AuthService \
  --name authservice-prod \
  --settings Redis="prod-redis.redis.cache.windows.net:6380,ssl=true,..." \
  --custom-name Redis --type Custom

# Deploy code
cd AuthService
az webapp up --resource-group RG-AuthService --name authservice-prod

# Configure health check
az webapp config set --resource-group RG-AuthService \
  --name authservice-prod --health-check-path /health

# Enable managed identity
az webapp identity assign --resource-group RG-AuthService --name authservice-prod
```

#### Azure Cache for Redis

```bash
# Create Azure Cache for Redis
az redis create --resource-group RG-AuthService \
  --name prod-redis --location eastus --sku Basic --vm-size c0

# Get connection string
az redis list-keys --name prod-redis --resource-group RG-AuthService

# Update appsettings.json with connection string
{
  "ConnectionStrings": {
    "Redis": "{cache-name}.redis.cache.windows.net:6380,password={primary-key},ssl=True,abortConnect=False"
  }
}
```

### Docker Deployment

#### Dockerfile (AuthService)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 6001

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AuthService.csproj", "./"]
RUN dotnet restore "AuthService.csproj"
COPY . .
RUN dotnet publish "AuthService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AuthService.dll"]
```

#### docker-compose.yml

```yaml
version: '3.8'

services:
  authservice:
    build: ./AuthService
    ports:
      - "6001:6001"
    environment:
      - ConnectionStrings__AuthDbConnection=Server=db;Database=AuthService;...
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      - redis
      - db
    restart: unless-stopped

  redis:
    image: redis:latest
    ports:
      - "6379:6379"
    restart: unless-stopped
    command: redis-server --appendonly yes

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports:
      - "1433:1433"
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Password
    volumes:
      - sqldata:/var/opt/mssql

volumes:
  sqldata:
```

#### Deploy to Kubernetes

```yaml
# authservice-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: authservice
spec:
  replicas: 3
  selector:
    matchLabels:
      app: authservice
  template:
    metadata:
      labels:
        app: authservice
    spec:
      containers:
      - name: authservice
        image: your-registry/authservice:latest
        ports:
        - containerPort: 6001
        env:
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: redis-secret
              key: connection-string
        - name: ConnectionStrings__AuthDbConnection
          valueFrom:
            secretKeyRef:
              name: db-secret
              key: connection-string
        livenessProbe:
          httpGet:
            path: /health
            port: 6001
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 6001
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: authservice
spec:
  selector:
    app: authservice
  ports:
  - protocol: TCP
    port: 80
    targetPort: 6001
  type: LoadBalancer
```

---

## Troubleshooting

### Common Issues and Solutions

#### 1. "Redis connection failed" warnings

**Symptoms:**
- Warning logs: `Failed to connect to Redis. System will use database-only validation.`
- Slower authentication responses (5-10ms vs 1-2ms)
- High database load

**Diagnosis:**
```bash
# Check Redis connectivity
redis-cli ping
# Expected: PONG

# Check Redis logs
sudo journalctl -u redis -f

# Check network connectivity
telnet redis-host 6379

# Test from application context
# In Program.cs, add diagnostic logging
var redis = sp.GetRequiredService<IConnectionMultiplexer>();
var db = redis.GetDatabase();
var pong = await db.PingAsync();
logger.LogInformation("Redis ping: {Pong}", pong);
```

**Solutions:**

**Solution A: Fix Redis connectivity**
```bash
# Start Redis if not running
sudo systemctl start redis

# Check Redis is listening
sudo netstat -tlnp | grep 6379

# Fix firewall (if needed)
sudo ufw allow 6379/tcp

# Check Redis configuration
sudo nano /etc/redis/redis.conf
# Verify: bind 0.0.0.0 (if remote access needed)
# Verify: port 6379
```

**Solution B: Update connection string**
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=secret,connectRetry=3,connectTimeout=5000"
  }
}
```

**Solution C: Accept degraded performance**
- If Redis is optional for your use case, warnings are acceptable
- Monitor database load and query performance
- Consider implementing Redis health alerts

---

#### 2. Tokens are rejected unexpectedly

**Symptoms:**
- Users report being logged out unexpectedly
- HTTP 401 responses: "Token has been revoked"
- Valid tokens being rejected

**Diagnosis:**
```bash
# Extract JTI from token (decode JWT)
# Online: https://jwt.io/
# Or using CLI:
echo "YOUR_TOKEN" | cut -d. -f2 | base64 -d | jq -r '.jti'

# Check database
sqlcmd -S server -d AuthDb -Q "
SELECT TokenJti, Reason, RevokedAt, ExpiresAt
FROM RevokedAccessTokens
WHERE TokenJti = 'YOUR_JTI'
"

# Check Redis
redis-cli GET "revoked:YOUR_JTI"
# If returns "true", token is cached as revoked

# Check logs
# Look for: "Token {TokenJti} is revoked"
# Search logs for the JTI or user ID
```

**Solutions:**

**Solution A: False positive revocation**
```sql
-- Check if token was incorrectly revoked
SELECT * FROM RevokedAccessTokens
WHERE TokenJti = 'YOUR_JTI'
AND RevokedAt > DATEADD(minute, -5, GETUTCDATE())

-- If recent, check logs for revocation event
-- Look for: "Revoked token {TokenJti} for user {UserId}"

-- If incorrect, remove from database
DELETE FROM RevokedAccessTokens
WHERE TokenJti = 'YOUR_JTI'

-- Remove from Redis
redis-cli DEL "revoked:YOUR_JTI"
```

**Solution B: Token expiration confusion**
```sql
-- Check token expiration time
SELECT ExpiresAt, DATEDIFF(second, GETUTCDATE(), ExpiresAt) AS SecondsRemaining
FROM RevokedAccessTokens
WHERE TokenJti = 'YOUR_JTI'

-- If ExpiresAt < GETUTCDATE(), token naturally expired
-- This is not a revocation issue
```

**Solution C: Token reuse after logout**
```bash
# If user explicitly logged out, token should be revoked
# This is expected behavior
# User must login again to get new token
```

---

#### 3. Performance degradation after deployment

**Symptoms:**
- Slower authentication responses
- Increased database load
- Timeouts on revocation checks

**Diagnosis:**
```bash
# Check Redis connection health
redis-cli INFO stats
# Look for: total_connections_received and rejected_connections

# Check Redis memory usage
redis-cli INFO memory
# Look for: used_memory and used_memory_peak

# Check database query performance
sqlcmd -S server -d AuthDb -Q "
SELECT @@VERSION
"

-- Check query plan for revocation check
SET SHOWPLAN_TEXT ON;
GO
SELECT * FROM RevokedAccessTokens WHERE TokenJti = 'test-jti';
GO

-- Check index usage
SELECT 
  OBJECT_NAME(i.object_id) AS TableName,
  i.name AS IndexName,
  i.type_desc AS IndexType,
  s.user_seeks,
  s.user_scans,
  s.user_lookups,
  s.user_updates
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats s ON i.object_id = s.object_id AND i.index_id = s.index_id
WHERE OBJECT_NAME(i.object_id) = 'RevokedAccessTokens'
```

**Solutions:**

**Solution A: Missing database indexes**
```sql
-- Create critical indexes
CREATE INDEX IX_RevokedAccessTokens_TokenJti 
ON RevokedAccessTokens(TokenJti);

CREATE INDEX IX_RevokedAccessTokens_UserId 
ON RevokedAccessTokens(UserId);

CREATE INDEX IX_RevokedAccessTokens_ExpiresAt 
ON RevokedAccessTokens(ExpiresAt);
```

**Solution B: Redis memory pressure**
```bash
# Configure Redis memory limit
sudo nano /etc/redis/redis.conf

# Add/modify:
maxmemory 256mb
maxmemory-policy allkeys-lru

# Restart Redis
sudo systemctl restart redis
```

**Solution C: High revocation rate**
```bash
# Check number of revoked tokens
sqlcmd -S server -d AuthDb -Q "
SELECT COUNT(*) AS TotalRevoked,
       COUNT(CASE WHEN ExpiresAt > GETUTCDATE() THEN 1 END) AS ActiveRevoked
FROM RevokedAccessTokens
"

# If count is very high (>100,000), consider:
# 1. More aggressive cleanup (run cleanup job more frequently)
# 2. Review revocation patterns (are users being logged out too often?)
# 3. Increase Redis memory
```

---

#### 4. Database errors during validation

**Symptoms:**
- Error logs: "Error checking token revocation status in database"
- HTTP 500 responses
- All tokens rejected (fail-closed behavior)

**Diagnosis:**
```bash
# Check database connectivity
sqlcmd -S server -d AuthDb -Q "SELECT @@VERSION"

# Check database logs
# SQL Server Management Studio → Management → SQL Server Logs

# Check application logs
# Look for: "Error checking token revocation status in database"

# Test query directly
sqlcmd -S server -d AuthDb -Q "
SELECT * FROM RevokedAccessTokens WHERE TokenJti = 'test-jti'
"
```

**Solutions:**

**Solution A: Database connection issues**
```json
// Update connection string with retry logic
{
  "ConnectionStrings": {
    "AuthDbConnection": "Server=server;Database=AuthService;Connect Timeout=30;Command Timeout=60;Integrated Security=True;"
  }
}
```

**Solution B: Database table missing**
```sql
-- Check if table exists
SELECT * FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME = 'RevokedAccessTokens'

-- If missing, run migration
dotnet ef database update

-- Or create manually
CREATE TABLE RevokedAccessTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId NVARCHAR(256) NOT NULL,
    TokenJti NVARCHAR(512) NOT NULL,
    Reason NVARCHAR(MAX) NULL,
    RevokedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NOT NULL,
    CONSTRAINT UQ_RevokedAccessTokens_TokenJti UNIQUE (TokenJti)
);
```

**Solution C: Database permissions**
```sql
-- Grant necessary permissions
USE AuthService;
GO
GRANT SELECT, INSERT, DELETE ON RevokedAccessTokens TO authService_user;
GO
```

---

#### 5. TTL calculation issues

**Symptoms:**
- Tokens remain in cache after expiration
- Tokens expired from cache too early
- Redis memory bloat

**Diagnosis:**
```bash
# Check Redis TTL for specific key
redis-cli TTL "revoked:SOME-JTI"
# Returns: seconds remaining
# Returns: -1 (no expiration set)
# Returns: -2 (key doesn't exist)

# Check token expiration from JWT
# Decode JWT and check 'exp' claim
```

**Solutions:**

**Solution A: TTL not set correctly**
```csharp
// In RevokedAccessTokenService, ensure TTL is calculated correctly
public async Task RevokedTokenAsync(string tokenJti, string userId, string? reason)
{
    // Decode token to get expiration
    var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
    var expiresAt = token.ValidTo;
    var ttl = expiresAt - DateTime.UtcNow;

    // Ensure TTL is positive
    if (ttl < TimeSpan.Zero)
    {
        _logger.LogWarning("Token {TokenJti} already expired, skipping cache", tokenJti);
        ttl = TimeSpan.FromSeconds(1); // Cache briefly for consistency
    }

    // Cache with TTL
    await CacheRevokedTokenAsync(tokenJti, ttl);
}
```

**Solution B: Manual TTL correction**
```bash
# If key has no TTL, set it manually
redis-cli EXPIRE "revoked:SOME-JTI" 1800  # 30 minutes

# Batch fix (if many keys)
redis-cli --scan --pattern "revoked:*" | xargs -I {} redis-cli EXPIRE {} 1800
```

---

### Diagnostic Commands

#### Redis Diagnostics

```bash
# Check Redis status
redis-cli ping

# Get Redis info
redis-cli INFO

# Check specific stats
redis-cli INFO stats
redis-cli INFO memory
redis-cli INFO clients

# Monitor commands (in production, use with caution)
redis-cli MONITOR

# Check revocation cache size
redis-cli --scan --pattern "revoked:*" | wc -l

# Get TTL for specific key
redis-cli TTL "revoked:JTI"

# Slow log
redis-cli SLOWLOG GET 10

# Client list
redis-cli CLIENT LIST
```

#### Database Diagnostics

```sql
-- Check table size
SELECT 
  COUNT(*) AS TotalRecords,
  COUNT(CASE WHEN ExpiresAt > GETUTCDATE() THEN 1 END) AS ActiveRecords,
  COUNT(CASE WHEN ExpiresAt <= GETUTCDATE() THEN 1 END) AS ExpiredRecords
FROM RevokedAccessTokens;

-- Check recent revocations
SELECT TOP 100
  TokenJti,
  UserId,
  Reason,
  RevokedAt,
  ExpiresAt
FROM RevokedAccessTokens
ORDER BY RevokedAt DESC;

-- Check index fragmentation
SELECT 
  OBJECT_NAME(ips.object_id) AS TableName,
  i.name AS IndexName,
  ips.avg_fragmentation_in_percent,
  ips.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, NULL) ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE OBJECT_NAME(ips.object_id) = 'RevokedAccessTokens';

-- Check query performance
SELECT TOP 10
  qs.execution_count,
  qs.total_elapsed_time / qs.execution_count AS avg_elapsed_time,
  qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
  SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
    ((CASE qs.statement_end_offset
      WHEN -1 THEN DATALENGTH(st.text)
      ELSE qs.statement_end_offset
    END - qs.statement_start_offset)/2) + 1) AS query_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE st.text LIKE '%RevokedAccessTokens%'
ORDER BY qs.total_elapsed_time DESC;
```

#### Application Diagnostics

```bash
# Check application logs
# Systemd
journalctl -u authservice -f

# Or check file-based logs
tail -f /var/log/authservice/authservice.log

# Search for specific errors
journalctl -u authservice | grep -i "error"
journalctl -u authservice | grep -i "revocation"
journalctl -u authservice | grep -i "redis"

# Check process status
systemctl status authservice

# Check health endpoint
curl http://localhost:6001/health

# Test revocation endpoint
curl -X GET http://localhost:6001/api/TokenRevocation/check/test-jti \
  -H "Authorization: Bearer {admin_token}"

# Check memory usage
ps aux | grep AuthService
```

---

## Testing

### Unit Tests

Test file: `AuthService.Tests/Services/JwtRevocationValidationServiceTests.cs`

```bash
# Run all tests
dotnet test AuthService.Tests

# Run specific test class
dotnet test AuthService.Tests --filter "FullyQualifiedName~JwtRevocationValidationServiceTests"

# Run with verbose output
dotnet test AuthService.Tests --logger "console;verbosity=detailed"
```

#### Test Coverage

| Test Case | Description |
|-----------|-------------|
| **Valid token passes validation** | Token not revoked should pass validation |
| **Revoked token in cache** | Token found in Redis cache should be rejected |
| **Revoked token in database** | Token found in DB should be rejected (cache miss) |
| **Redis unavailable** | System should gracefully degrade to DB-only |
| **Database error** | System should reject token (fail-closed) |
| **Missing JTI claim** | Should return validation failure |
| **Invalid JTI format** | Should return validation failure |
| **Concurrent validation** | Multiple threads validating tokens simultaneously |
| **TTL calculation** | Cache TTL matches token expiration |

### Integration Tests

Test file: `AuthService.Tests/Integration/JwtMiddlewareIntegrationTests.cs`

```bash
# Run integration tests
dotnet test AuthService.Tests --filter "FullyQualifiedName~Integration"

# Run with web server
dotnet test AuthService.Tests --filter "FullyQualifiedName~Integration" --logger "console;verbosity=detailed"
```

#### Integration Test Scenarios

1. **End-to-end revocation flow**: Login → revoke token → authenticate → verify 401
2. **Password change flow**: Change password → revoke all tokens → verify re-authentication required
3. **Multiple services**: Revoke token → verify rejection from all microservices
4. **Redis failure scenario**: Stop Redis → verify DB fallback works
5. **High load**: 1000 concurrent requests with mixed valid/revoked tokens

### Manual Testing

#### Test Suite 1: Basic Revocation

```bash
# 1. Login to get token
TOKEN=$(curl -s -X POST http://localhost:8080/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com", "password":"@Password123"}' \
  | jq -r '.token')

echo "Token: $TOKEN"

# 2. Use token to access protected endpoint (should succeed)
curl -X GET http://localhost:8080/api/resources \
  -H "Authorization: Bearer $TOKEN"
# Expected: 200 OK

# 3. Extract JTI from token
JTI=$(echo $TOKEN | cut -d. -f2 | base64 -d 2>/dev/null | jq -r '.jti')
echo "JTI: $JTI"

# 4. Revoke token
curl -X POST http://localhost:8080/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"tokenJti\":\"$JTI\",\"userId\":\"12345\",\"reason\":\"Test revocation\"}"
# Expected: 200 OK

# 5. Try to use revoked token (should fail)
curl -X GET http://localhost:8080/api/resources \
  -H "Authorization: Bearer $TOKEN"
# Expected: 401 Unauthorized - "Token has been revoked"
```

#### Test Suite 2: Revoke All User Tokens

```bash
# 1. Login multiple times to get multiple tokens
TOKEN1=$(curl -s -X POST http://localhost:8080/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com", "password":"@Password123"}' \
  | jq -r '.token')

TOKEN2=$(curl -s -X POST http://localhost:8080/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com", "password":"@Password123"}' \
  | jq -r '.token')

# 2. Verify both tokens work
curl -X GET http://localhost:8080/api/resources -H "Authorization: Bearer $TOKEN1" | jq '.status'
curl -X GET http://localhost:8080/api/resources -H "Authorization: Bearer $TOKEN2" | jq '.status'
# Expected: 200 for both

# 3. Revoke all user tokens
curl -X POST http://localhost:8080/api/TokenRevocation/revoke-all/12345 \
  -H "Authorization: Bearer {admin_token}"
# Expected: 200 OK

# 4. Verify both tokens now fail
curl -X GET http://localhost:8080/api/resources -H "Authorization: Bearer $TOKEN1" | jq '.status'
curl -X GET http://localhost:8080/api/resources -H "Authorization: Bearer $TOKEN2" | jq '.status'
# Expected: 401 for both
```

#### Test Suite 3: Redis Failure Scenario

```bash
# 1. Stop Redis
sudo systemctl stop redis

# 2. Verify Redis is down
redis-cli ping
# Expected: Connection refused

# 3. Revoke a token
JTI="test-jti-123"
curl -X POST http://localhost:8080/api/TokenRevocation/revoke \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {admin_token}" \
  -d "{\"tokenJti\":\"$JTI\",\"userId\":\"12345\",\"reason\":\"Test\"}"
# Expected: 200 OK (should succeed even without Redis)

# 4. Check if token is revoked (uses DB only)
curl -X GET http://localhost:8080/api/TokenRevocation/check/$JTI \
  -H "Authorization: Bearer {admin_token}"
# Expected: {"isRevoked": true}

# 5. Check logs for warnings
sudo journalctl -u authservice | grep -i "redis.*unavailable"
# Expected: Warning about Redis unavailability

# 6. Restart Redis
sudo systemctl start redis

# 7. Verify Redis is back
redis-cli ping
# Expected: PONG

# 8. Check logs for recovery
sudo journalctl -u authservice | tail -20
# Should show normal operation restored
```

---

## Maintenance

### Routine Maintenance Tasks

#### Daily (Optional)

- **Check Redis health**
  ```bash
  redis-cli INFO memory
  redis-cli INFO stats
  ```

- **Review recent revocation events**
  ```sql
  SELECT COUNT(*) AS RevocationsLast24h
  FROM RevokedAccessTokens
  WHERE RevokedAt >= DATEADD(hour, -24, GETUTCDATE());
  ```

- **Monitor error rates in logs**
  ```bash
  sudo journalctl -u authservice --since "24 hours ago" | grep -i "error" | wc -l
  ```

#### Weekly

- **Check database size and cleanup**
  ```sql
  SELECT 
    COUNT(*) AS TotalRecords,
    COUNT(CASE WHEN ExpiresAt > GETUTCDATE() THEN 1 END) AS ActiveRecords,
    COUNT(CASE WHEN ExpiresAt <= GETUTCDATE() THEN 1 END) AS ExpiredRecords
  FROM RevokedAccessTokens;
  ```

- **Review security incidents**
  - Check for unusual revocation patterns
  - Review reasons for revocations (security logs)

- **Performance metrics**
  - Cache hit rate
  - Average latency
  - Database query performance

#### Monthly

- **Full database backup verification**
  ```bash
  # SQL Server backup verification
  sqlcmd -S server -Q "BACKUP DATABASE AuthService TO DISK='NUL' WITH STATS"
  ```

- **Index maintenance**
  ```sql
  -- Rebuild fragmented indexes
  ALTER INDEX IX_RevokedAccessTokens_TokenJti 
  ON RevokedAccessTokens REBUILD;
  
  UPDATE STATISTICS RevokedAccessTokens;
  ```

- **Review and update documentation**
  - Update this guide with lessons learned
  - Add new troubleshooting scenarios

- **Capacity planning**
  - Review Redis memory usage trends
  - Review RevokedAccessTokens table growth
  - Plan for scale-up if needed

### Automated Maintenance

#### Daily Cleanup Job (Cron)

```bash
# Add to crontab
crontab -e

# Run cleanup at 2 AM daily
0 2 * * * curl -X POST http://localhost:6001/api/TokenRevocation/cleanup \
  -H "Authorization: Bearer {admin_token}" \
  >> /var/log/authservice/cleanup.log 2>&1
```

#### Health Monitoring (Prometheus + Grafana)

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'authservice'
    metrics_path: '/metrics'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:6001']
```

**Key metrics to monitor:**
- `authservice_revocation_checks_total` - Total validation checks
- `authservice_revocation_cache_hits` - Cache hit count
- `authservice_revocation_cache_misses` - Cache miss count
- `authservice_revocation_failures` - Validation failures
- `authservice_redis_connection_errors` - Redis connection errors

### Log Retention Policy

**Recommended retention:**

| Log Type | Retention | Storage |
|----------|-----------|---------|
| **Application logs** | 30 days | /var/log/authservice/ |
| **Redis logs** | 7 days | /var/log/redis/ |
| **Database logs** | 90 days | /var/opt/mssql/log/ |
| **Cleanup logs** | 1 year | /var/log/authservice/cleanup/ |

**Log rotation:**

```bash
# /etc/logrotate.d/authservice
/var/log/authservice/*.log {
  daily
  rotate 30
  compress
  delaycompress
  notifempty
  create 0640 authservice authservice
  sharedscripts
  postrotate
    systemctl reload authservice > /dev/null 2>&1 || true
  endscript
}
```

### Disaster Recovery

#### Redis Backup Strategy

```bash
# Enable Redis persistence (if needed for disaster recovery)
sudo nano /etc/redis/redis.conf

# Configure RDB snapshots
save 900 1      # Save after 900 seconds if at least 1 key changed
save 300 10     # Save after 300 seconds if at least 10 keys changed
save 60 10000   # Save after 60 seconds if at least 10000 keys changed

# Backup directory
dir /var/lib/redis

# Restart Redis
sudo systemctl restart redis

# Regular backup script
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
cp /var/lib/redis/dump.rdb /backup/redis/dump_$DATE.rdb
find /backup/redis/ -name "dump_*.rdb" -mtime +7 -delete
```

#### Database Backup Strategy

```sql
-- Full backup (daily)
BACKUP DATABASE AuthService 
TO DISK = '/backup/AuthDb/AuthDb_Full_20260405.bak'
WITH COMPRESSION, STATS;

-- Differential backup (hourly)
BACKUP DATABASE AuthService 
TO DISK = '/backup/AuthDb/AuthDb_Diff_20260405_1400.bak'
WITH DIFFERENTIAL, COMPRESSION, STATS;

-- Transaction log backup (every 15 minutes)
BACKUP LOG AuthService 
TO DISK = '/backup/AuthDb/AuthDb_Log_20260405_1415.trn';
```

### Security Best Practices

1. **Rotate Redis passwords**
   ```bash
   # Every 90 days
   redis-cli CONFIG set requirepass new-password
   # Update connection strings
   ```

2. **Audit revocation actions**
   - Log all revocation API calls
   - Send security alerts for suspicious patterns
   - Regular audit of revocation logs

3. **Principle of least privilege**
   - Service accounts only need SELECT/INSERT/DELETE on RevokedAccessTokens
   - Admin users can revoke tokens for all users
   - Regular users can only revoke their own tokens

4. **Network security**
   - Use TLS for Redis connections in production
   - Restrict Redis to private network
   - Implement firewall rules

---

## Additional Resources

### Documentation

- **ASP.NET Core Authentication**: [docs.microsoft.com/aspnet/core/security/authentication/](https://docs.microsoft.com/aspnet/core/security/authentication/)
- **JWT Specification**: [tools.ietf.org/html/rfc7519](https://tools.ietf.org/html/rfc7519)
- **Redis Documentation**: [redis.io/documentation](https://redis.io/documentation)
- **StackExchange.Redis**: [stackexchange.github.io/StackExchange.Redis/](https://stackexchange.github.io/StackExchange.Redis/)

### Code References

- **JwtRevocationBearerEvents.cs** - JWT middleware integration
- **JwtRevocationValidationService.cs** - Core validation logic
- **RevokedAccessTokenService.cs** - Revocation service implementation
- **TokenRevocationController.cs** - API endpoints

### Support

- **Internal**: Contact DevOps team for deployment issues
- **Issues**: Track bugs in project issue tracker
- **Emergency**: Follow incident response procedures

---

**Document Version:** 1.0.0  
**Last Updated:** April 5, 2026  
**Maintained by:** DevOps Team  
**Next Review:** July 5, 2026 (quarterly)