# TP2 Commerce Électronique - E-Commerce Microservices Platform

**Course:** INF27523 - Technologies du commerce électronique  
**Semester:** Hiver 2026  
**Institution:** UQAR (Université du Québec à Rimouski)  
**Repository:** `Es3n13/TP2_CommerceElectronique`  
**Branch:** `V.Beta` (Stable/On Ice) | `V.Alpha` (Development) | `main` (Baseline)

---

## 🎯 Project Overview

A production-ready, **headless** e-commerce microservices platform built with ASP.NET Core. This project implements a backend-only architecture (Web APIs) designed to be consumed by external frontends or integrated via an API Gateway.

**Current Status:** ❄️ **ON ICE** (Stabilized and finalized for current phase).

**Key Features:**
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

## 🔥 Recent Updates (April 15, 2026)

### 🔧 Final Stabilizations
1. **Headless Architecture Verification ✅**
   - Confirmed the system as a pure Web API suite.
   - No frontend implementation; all interactions are via SwaggerUI or API calls.
2. **Notification Service Integration ✅**
   - Successfully implemented and verified the `NotificationService`.
   - Services now have the capability to send notifications (verified working).
3. **UserService Stability ✅**
   - Final verification of `UserService` stability and integration with Auth flow.
4. **Project Status: On Ice ❄️**
   - Project is now in a stabilized state, with all core and bonus features for the current phase implemented and verified.

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
| **NotificationService**| - | Variable | System notifications (Bonus) | ✅ Yes |
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
│  UserService  │  Resources    │ Reservations
│  (Port 5000)  │  (Port 5001)  │  (Port 5002)
│  JWT Auth ✅  │  JWT Auth ✅ │  JWT Auth ✅
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
   git checkout V.Beta
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Configure databases:**
   - Update connection strings in `appsettings.json` for each service
   - Default: `(localdb)\\mssqllocaldb`

4. **Build the solution:**
   ```bash
   dotnet build
   ```

### Running Services

**Option 1: Manual (7 terminals):**
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

# Terminal 6 - Notification Service
cd NotificationService && dotnet run

# Terminal 7 - API Gateway (Port 8080)
cd ApiGateway && dotnet run
```

---

## 📡 Access Points

### Gateway (Unified Entry Point)
- **Base URL:** `http://localhost:8080`
- **Swagger UI:** `http://localhost:8080/swagger`
- **Aggregated Swagger:** `http://localhost:8080/swagger/docs`

---

## 🔐 Authentication

### JWT Flow

1. **Register:** `POST /api/users/register`
2. **Login:** `POST /api/users/login` $\rightarrow$ Get Token
3. **Access:** Use `Authorization: Bearer {token}` header

---

## 🔒 Swagger UI Authorization Guide

### Using the Authorize Padlock

1. **Get Token** via `/api/users/login`
2. **Click 🔒 Authorize** in any Swagger UI
3. **Enter:** `Bearer eyJhbGci... `
4. **Test** protected endpoints.

---

## 🔃 JWT Token Revocation ✅

The platform includes a comprehensive JWT token revocation system using Redis for immediate invalidation.

**API Endpoints:**
- `POST /api/TokenRevocation/revoke` - Single token
- `GET /api/TokenRevocation/check/{tokenJti}` - Status check
- `POST /api/TokenRevocation/revoke-all/{userId}` - All user tokens
- `POST /api/TokenRevocation/cleanup` - Purge expired

---

## 💳 Payment Integration

### Stripe Implementation
- **Create Payment Intent:** `POST /api/payments/create`
- **Returns:** `paymentIntentId` and `clientSecret` for frontend integration.

---

## 🔄 Service Communication

- **Direct HTTP client calls** for internal communication.
- **Service-to-service** trust (no internal auth required).
- **API Gateway** handles all external JWT validation.

---

## 📊 API Documentation

- **Gateway Aggregated Swagger:** `http://localhost:8080/swagger`
- **Individual Service Swaggers:** Accessible at `/swagger` for each service port.

---

## 🧪 Testing

```bash
dotnet test
```

---

## 📁 Project Structure

```
TP2_CommerceElectronique/
├── AuthService/              # JWT authentication service
├── UserService/              # User management
├── ResourcesService/         # Products/resources
├── ReservationsService/      # Bookings/orders
├── PaymentService/           # Payment processing
├── NotificationService/      # System notifications (Bonus)
├── ApiGateway/               # Ocelot gateway
└── TP2_CommerceElectronique.sln
```

---

## 📈 Progress

### Phase 1: Core MVP ✅ COMPLETE
- [x] EF Core integration for all services
- [x] JWT authentication with refresh tokens
- [x] Payment service with Stripe
- [x] All CRUD operations
- [x] Service communication mesh

### Phase 2: Integration ✅ COMPLETE
- [x] Ocelot API gateway
- [x] Gateway-level JWT authentication
- [x] Service-to-service communication
- [x] Swagger aggregation (MMLib.SwaggerForOcelot)
- [x] CORS configuration resolved
- [x] Swagger security definitions (Authorize padlock)
- [x] Framework standardization (.NET 10.0)
- [x] Bonus: Notification service implemented and verified

### Phase 3: Deployment ⏳ NOT STARTED
- [ ] Azure deployment
- [ ] Production configuration

---

## 🐛 Known Issues

**✅ RESOLVED - CORS Issues**
- Fixed: Removed HTTPS redirection, updated CORS configuration.

**✅ RESOLVED - Swagger Authorization**
- Fixed: Added "Authorize" padlock to all services.

---

## 🛠️ Configuration

- **.NET Framework:** 10.0
- **Connection Strings:** SQL Server `(localdb)\mssqllocaldb`
- **Payment:** Stripe.net v47.3.0

---

## 👥 Authors

- **Snoop Frogg** (@snoopfrogg7085)
- Course: INF27523 - Technologies du commerce électronique
- Semester: Hiver 2026

---

*Last Updated: April 15, 2026 | Project Status: ❄️ On Ice*
