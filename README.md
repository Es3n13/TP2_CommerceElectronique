# TP2 Commerce Électronique - E-Commerce Microservices Platform

**Course:** INF27523 - Technologies du commerce électronique  
**Semester:** Hiver 2026  
**Institution:** UQAR (Université du Québec à Rimouski)  
**Repository:** `Es3n13/TP2_CommerceElectronique`  
**Branch:** `V.Alpha` (Development) | `main` (Baseline)

---

## 🎯 Project Overview

A production-ready e-commerce microservices platform built with ASP.NET Core, featuring:

- ✅ 6 fully functional microservices
- ✅ JWT-based authentication across all services
- ✅ Ocelot API Gateway with unified authentication
- ✅ Stripe payment integration
- ✅ Enhanced Swagger UI with security (Authorize padlock)
- ✅ Complete service communication mesh
- ✅ SQL Server databases per service
- ✅ Swagger aggregation with MMLib.SwaggerForOcelot
- ✅ CORS configuration resolved
- ✅ Standardized .NET 10.0 across all services

---

## 🔥 Recent Updates (April 7, 2026)

### 🔧 Technical Improvements

1. **API Gateway CORS Fix ✅**
   - Resolved CORS issues between gateway and services
   - Removed HTTPS redirection that was causing blockage
   - Services now properly handle HTTP requests from gateway

2. **JWT Authentication Standardization ✅**
   - Added JWT authentication to all 6 production services
   - Each service validates JWT tokens independently
   - Gateway also validates tokens at entry point
   - Double-layer security (gateway + service-level)

3. **Swagger Security Integration ✅**
   - Added "Authorize" padlock button to all Swagger UIs
   - JWT bearer authentication now works seamlessly in Swagger
   - Security definitions configured in `Program.cs` for each service
   - Easy token-based API testing without manual header management

4. **Framework Standardization ✅**
   - All services standardized to .NET 10.0
   - Consistent SDK versions across the platform
   - Improved compatibility and performance

---

## 🏗️ Architecture

### Microservices

| Service | Database | Port | Purpose | JWT Auth |
|---------|----------|------|---------|----------|
| **AuthService** | AuthDb | 6001 | JWT token generation & validation | ✅ Yes |
| **UserService** | UserDb | 5000 | User management & authentication | ✅ Yes |
| **ResourcesService** | ResourceDb | 5001 | Products/resources management | ✅ Yes |
| **ReservationsService** | ReservationDb | 5002 | Booking/order management | ✅ Yes |
| **PaymentService** | PaymentDb | 5003 | Payment processing (Stripe) | ✅ Yes |
| **ApiGateway** | - | 8080 | Unified API entry point + JWT auth | ✅ Yes |

### Technology Stack

- **Framework:** .NET 10.0 (all services standardized)
- **Language:** C#
- **Databases:** SQL Server (one per service)
- **ORM:** Entity Framework Core
- **Authentication:** JWT (HS256)
- **API Gateway:** Ocelot 24.1.0
- **Payment:** Stripe.net v47.3.0
- **API Documentation:** Swashbuckle.AspNetCore v10.1.7
- **Swagger Aggregation:** MMLib.SwaggerForOcelot v10.0.0
- **OpenAPI:** Microsoft.OpenApi v2.4.1

### Security Layers

```
┌─────────────────────────────────────────────────────────────┐
│                    Ocelot API Gateway                        │
│  Port: 8080 | JWT Validation | CORS Configuration           │
└─────────────────────┬───────────────────────────────────────┘
                      │
      ┌───────────────┼───────────────┐
      │               │               │
┌─────▼─────┐   ┌─────▼─────┐   ┌─────▼─────┐
│  UserService    │  Resources   │ Reservations
│  (Port 5000)    │  (Port 5001) │  (Port 5002)
│  JWT Auth ✅    │  JWT Auth ✅ │  JWT Auth ✅
└─────┬─────┘   └─────┬─────┘   └─────┬─────┘
      │               │               │
      └───────────────┼───────────────┘
                      │
      ┌───────────────▼───────────────┐
      │  AuthService (Port 6001)      │
      │  JWT Token Issuer ✅          │
      └───────────────────────────────┘
                      │
      ┌───────────────▼───────────────┐
      │  PaymentService (Port 5003)   │
      │  Stripe Integration ✅        │
      │  JWT Auth ✅                  │
      └───────────────────────────────┘
```

---

## 🚀 Quick Start

### Prerequisites

- .NET 10.0 SDK
- SQL Server (or SQL Express)
- Node.js (for OpenClaw if applicable)

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/Es3n13/TP2_CommerceElectronique.git
   cd TP2_CommerceElectronique
   git checkout V.Alpha
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Configure databases:**
   - Update connection strings in `appsettings.json` for each service
   - Default: `(localdb)\\mssqllocaldb`
   - Or modify to your SQL Server instance

4. **Build the solution:**
   ```bash
   dotnet build
   ```

### Running Services

**Option 1: Manual (6 terminals):**
```bash
# Terminal 1 - Auth Service (Port 6001)
cd AuthService && dotnet run

# Terminal 2 - User Service (Port 5000)
cd UserService && dotnet run

# Terminal 3 - Resources Service (Port 5001)
cd ResourcesService && dotnet run

# Terminal 4 - Reservations Service (Port 5002)
cd ReservationsService && dotnet run

# Terminal 5 - Payment Service (Port 5003)
cd PaymentService && dotnet run

# Terminal 6 - API Gateway (Port 8080)
cd ApiGateway && dotnet run
```

**Option 2: Using Docker (if configured):**
```bash
docker-compose up
```

---

## 📡 Access Points

### Gateway (Unified Entry Point)
- **Base URL:** `http://localhost:8080`
- **Swagger UI:** `http://localhost:8080/swagger`
- **Aggregated Swagger:** `http://localhost:8080/swagger/docs`

**Routes:**
- `POST /api/users/register` - Register new user
- `POST /api/users/login` - User login
- `GET/POST/PUT/DELETE /api/resources/*` - Resource management
- `GET/POST/PUT/DELETE /api/reservations/*` - Reservation management
- `GET/POST/PUT/DELETE /api/payments/*` - Payment operations
- `GET/POST /api/auth/*` - Authentication endpoints

### Individual Services

| Service | Base URL | Swagger | JWT Required |
|---------|----------|---------|--------------|
| AuthService | `http://localhost:6001` | `/swagger` | Public endpoints |
| UserService | `http://localhost:5000` | `/swagger` | ✅ Yes (except register/login) |
| ResourcesService | `http://localhost:5001` | `/swagger` | ✅ Yes |
| ReservationsService | `http://localhost:5002` | `/swagger` | ✅ Yes |
| PaymentService | `http://localhost:5003` | `/swagger` | ✅ Yes |

---

## 🔐 Authentication

### JWT Flow

1. **Register:**
   ```http
   POST /api/users/register
   Content-Type: application/json

   {
     "pseudo": "john_doe",
     "email": "john@example.com",
     "password": "@Password123",
     "firstName": "John",
     "lastName": "Doe"
   }
   ```

2. **Login:**
   ```http
   POST /api/users/login
   Content-Type: application/json

   {
     "email": "john@example.com",
     "password": "@Password123"
   }

   Response:
   {
     "token": "eyJhbGciOiJIUzI1NiIs...",
     "userId": 1,
     "expiresAt": "2026-04-08T19:00:00Z"
   }
   ```

3. **Access Protected Routes:**
   ```http
   GET /api/resources
   Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
   ```

### JWT Configuration

- **Algorithm:** HS256
- **Secret:** Configured in `appsettings.json`
- **Expiration:** 30 minutes (default)
- **Issuer:** `https://localhost:6001`
- **Audience:** `TP2CommerceElectronique`

---

## 🔒 Swagger UI Authorization Guide

### Using the Authorize Padlock

All Swagger UIs now include an **"Authorize"** padlock button for JWT authentication.

**Step-by-Step:**

1. **Get Your JWT Token**
   - Login via `POST /api/users/login` endpoint
   - Copy the `token` value from the response

2. **Open Any Swagger UI**
   - Navigate to any service's Swagger (e.g., `http://localhost:8080/swagger`)
   - Click the **🔒 Authorize** button (top-right corner)

3. **Enter Token**
   - Click "Available authorizations"
   - In the popup, enter your token with `Bearer ` prefix:
     ```
     Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
     ```
   - Click **Authorize**
   - Click **Close**

4. **Test Protected Endpoints**
   - Try any endpoint that requires authentication
   - Swagger automatically adds the `Authorization: Bearer {token}` header
   - No manual header configuration needed!

**Note:** Registration and login endpoints do NOT require JWT authentication.

### Security Definitions

Each service has the following security definition configured:

```csharp
services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
```

---

## 🔃 JWT Token Revocation ✅

The platform now includes a comprehensive JWT token revocation system that enables immediate token invalidation.

**Key Features:**
- ✅ **Immediate revocation** - Tokens invalidated instantly, no waiting for expiration
- ✅ **Automatic validation** - Every JWT checked against revocation list automatically
- ✅ **High performance** - Redis caching provides sub-millisecond lookups
- ✅ **Graceful degradation** - System continues functioning if Redis is unavailable
- ✅ **Fail-closed security** - Unknown states result in token rejection

**Use Cases:**
- **Password changes** - Invalidate all user tokens after password reset
- **User logout** - Revoke specific token on explicit logout
- **Security incidents** - Immediately invalidate compromised tokens
- **Account deletion** - Revoke all tokens for deleted accounts

**API Endpoints:**
```http
# Revoke single token
POST /api/TokenRevocation/revoke
{
  "tokenJti": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "userId": "12345",
  "reason": "User logged out"
}

# Check token status
GET /api/TokenRevocation/check/{tokenJti}

# Revoke all user tokens
POST /api/TokenRevocation/revoke-all/{userId}

# Cleanup expired tokens
POST /api/TokenRevocation/cleanup
```

**Documentation:**
- Full feature guide: [JWT_REVOCATION_FEATURE_GUIDE.md](JWT_REVOCATION_FEATURE_GUIDE.md)
- Changelog: [CHANGELOG_JWT_REVOCATION.md](CHANGELOG_JWT_REVOCATION.md)

---

## 💳 Payment Integration

### Stripe Implementation

**Create Payment Intent:**
```http
POST /api/payments/create
Authorization: Bearer {token}
Content-Type: application/json

{
  "reservationId": 1,
  "amount": 10000,  // in cents ($100.00)
  "currency": "cad"
}
```

**Response:**
```json
{
  "paymentIntentId": "pi_3abc...",
  "clientSecret": "pi_3abc_secret_...",
  "status": "pending",
  "reservationId": 1
}
```

### Test Payment Methods

- `pm_card_visa` - Visa card
- `pm_card_mastercard` - Mastercard
- `pm_card_amex` - American Express

---

## 🔄 Service Communication

### Communication Patterns

**Payment → Reservations:**
- PaymentService updates reservation status after successful payment

**UserService → AuthService:**
- User login triggers JWT token generation

**ReservationsService → UserService:**
- Validates user exists before creating reservations

**ReservationsService → ResourcesService:**
- Checks resource availability before booking

**Communication Method:**
- Direct HTTP client calls
- Service-to-service (no auth required)
- Gateway validates tokens for external clients

---

## 📊 API Documentation

### Swagger UI (Individual Services)

Each service has its own Swagger UI with **Authorize padlock**:
- **AuthService:** `http://localhost:6001/swagger`
- **UserService:** `http://localhost:5000/swagger`
- **ResourcesService:** `http://localhost:5001/swagger`
- **ReservationsService:** `http://localhost:5002/swagger`
- **PaymentService:** `http://localhost:5003/swagger`

### Gateway Swagger Aggregation

- **URL:** `http://localhost:8080/swagger`
- **Status:** ✅ **Fully functional**
- **Features:**
  - Unified Swagger UI for all 5 services
  - Dropdown selector to switch between services
  - Gateway-level JWT validation
  - Authorize padlock for all protected routes
  - CORS configuration resolved
- **Configuration:**
  - MMLib.SwaggerForOcelot v10.0.0
  - Swashbuckle.AspNetCore v10.1.7
  - PathToSwaggerGenerator: `/swagger/docs`
  - Middleware order: Auth → UseSwaggerForOcelotUI → UseOcelot

---

## 🧪 Testing

### Running Tests

```bash
dotnet test
```

### Manual Testing Workflow

1. **Start all services** (6 terminals)
2. **Register a user** via Swagger UI (`POST /api/users/register`)
3. **Login** to get JWT token (`POST /api/users/login`)
4. **Copy the token** from the response
5. **Open any Swagger UI** and click **🔒 Authorize**
6. **Enter token** with `Bearer ` prefix
7. **Test protected endpoints** (JWT automatically added to headers)
8. **Create a resource** (`POST /api/resources`)
9. **Create a reservation** (`POST /api/reservations`)
10. **Process payment** (`POST /api/payments/create`)
11. **Verify reservation status update**

---

## 📁 Project Structure

```
TP2_CommerceElectronique/
├── AuthService/              # JWT authentication service
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Data/
│   └── appsettings.json
├── UserService/              # User management
│   ├── Controllers/
│   ├── Models/
│   ├── Data/
│   └── appsettings.json
├── ResourcesService/         # Products/resources
│   ├── Controllers/
│   ├── Models/
│   ├── Data/
│   └── appsettings.json
├── ReservationsService/      # Bookings/orders
│   ├── Controllers/
│   ├── Models/
│   ├── Data/
│   └── appsettings.json
├── PaymentService/           # Payment processing
│   ├── Controllers/
│   ├── Models/
│   ├── Data/
│   └── appsettings.json
├── ApiGateway/               # Ocelot gateway
│   ├── Program.cs
│   ├── ApiGateway.csproj
│   ├── ocelot.json
│   └── appsettings.json
└── TP2_CommerceElectronique.sln
```

---

## 📈 Progress

### Phase 1: Core MVP ✅ COMPLETE
- ✅ EF Core integration for all services
- ✅ JWT authentication with refresh tokens
- ✅ Payment service with Stripe
- ✅ All CRUD operations
- ✅ Service communication mesh

### Phase 2: Integration ✅ COMPLETE
- ✅ Ocelot API gateway
- ✅ Gateway-level JWT authentication
- ✅ Service-to-service communication
- ✅ Swagger aggregation (MMLib.SwaggerForOcelot)
- ✅ CORS configuration resolved
- ✅ Swagger security definitions (Authorize padlock)

### Phase 3: Deployment ⏳ NOT STARTED
- ⏳ Azure deployment
- ⏳ Production configuration
- ⏳ Bonus: Notification service

---

## 🐛 Known Issues

**✅ RESOLVED - CORS Issues**
- Previous: CORS errors between gateway and services
- Fixed: Removed HTTPS redirection, updated CORS configuration
- All services now properly handle HTTP requests from gateway

**✅ RESOLVED - Swagger Authorization**
- Previous: No way to test protected endpoints in Swagger UI
- Fixed: Added "Authorize" padlock to all services with JWT bearer authentication
- Token-based testing now works seamlessly

---

## 🛠️ Configuration

### Environment Variables

**.NET Framework:**
- `DOTNET_VERSION=10.0` (all services standardized)

**Connection Strings:**
- `DB_CONNECTION_STRING` (SQL Server connection string)

**JWT Configuration:**
- `JWT_SECRET_KEY` - HMAC secret for token signing
- `JWT_ISSUER` - Token issuer (default: `https://localhost:6001`)
- `JWT_AUDIENCE` - Token audience (default: `TP2CommerceElectronique`)

**Stripe Configuration:**
- `STRIPE_SECRET_KEY` - Your Stripe secret key
- `STRIPE_PUBLISHABLE_KEY` - Your Stripe publishable key

---

## 📝 Database Schema

### AuthDb
- **RefreshTokens:** TokenId, UserId, Token, ExpirationDate, CreatedAt, IsRevoked
- **TokenRevocations:** TokenJti, UserId, RevokedAt, Reason

### UserDb
- **Users:** Id, Pseudo, Email, PasswordHash, FirstName, LastName, PhoneNumber, Role, CreatedAt

### ResourceDb
- **Resources:** Id, Name, Description, Price, ImageUrl, StockQuantity, Category, CreatedAt

### ReservationDb
- **Reservations:** Id, UserId, ResourceId, Quantity, TotalPrice, Status

### PaymentDb
- **Payments:** Id, ReservationId, Amount, Currency, PaymentIntentId, Status

---

## 🤝 Contributing

This is an academic project. For contributions:

1. Create feature branch from `V.Alpha`
2. Make changes with clear commit messages
3. Test thoroughly
4. Submit pull request

---

## 📄 License

This project is created for educational purposes at UQAR (Université du Québec à Rimouski).

---

## 👥 Authors

- **Snoop Frogg** (@snoopfrogg7085)
- Course: INF27523 - Technologies du commerce électronique
- Semester: Hiver 2026

---

## 📞 Support

For issues or questions:
- Review the documentation in `docs/` directory
- Check individual service Swagger UIs
- Refer to the repository wiki (if available)

---

## 🗺️ Roadmap

### Completed ✅
- [x] All microservices implemented
- [x] JWT authentication (all services)
- [x] Payment integration (Stripe)
- [x] Service communication
- [x] API Gateway (Ocelot)
- [x] Individual Swagger documentation
- [x] Swagger aggregation (MMLib.SwaggerForOcelot)
- [x] CORS configuration
- [x] Swagger security definitions (Authorize padlock)
- [x] Framework standardization (.NET 10.0)

### Planned ⏳
- [ ] Azure deployment
- [ ] Production configuration
- [ ] Bonus: Notification service
- [ ] Role-based authorization
- [ ] Rate limiting
- [ ] Integration tests

---

**Last Updated:** April 7, 2026  
**Version:** V.Alpha  
**Status:** Development (95% Complete) ✅