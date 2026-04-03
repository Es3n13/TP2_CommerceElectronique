# TP2 Commerce Électronique - MVP-First Roadmap

**Course:** INF27523 - Technologies du commerce électronique
**Semester:** Hiver 2026
**Repository:** `Es3n13/TP2_CommerceElectronique`

---

## 🎯 Phase 1: Core MVP (Completed ✅)

### Goal: Working microservices with real databases + auth

#### 1.1 Add Entity Framework Core to all services ✅
- [x] Install Microsoft.EntityFrameworkCore.SqlServer
- [x] Create DbContext for each service
- [x] Migrate in-memory data to SQL Server
- [x] Each service gets its own database

**Status:** ✅ Complete
- ✅ UserService (V.Alpha - d86eb1f)
- ✅ ResourcesService (V.Alpha - 5c47624)
- ✅ ReservationsService (V.Alpha - 5c47624)
- ✅ AuthService (V.Alpha - a2e7f72)

#### 1.2 Build JWT Authentication ✅
- [x] Create AuthService microservice
- [x] Implement token generation endpoints
- [x] Generate JWT tokens with proper claims
- [x] Validate JWT tokens
- [x] Add refresh token support
- [x] Database storage for refresh tokens

**Status:** ✅ Complete (April 1-2, 2026)
- ✅ AuthController with token generation (V.Alpha)
- ✅ RefreshToken entity with indexes
- ✅ AuthDbContext with SQL Server integration
- ✅ TokenService with refresh token management
- ✅ Integration: UserService ↔ AuthService via HttpClient

#### 1.3 Create Payment Service with Stripe ✅
- [x] New microservice: PaymentService (port 5003)
- [x] Integrate Stripe.net (v47.3.0)
- [x] Implement payment intent creation
- [x] Add payment confirmation
- [x] Implement refunds
- [x] Add payment history tracking
- [x] PaymentDbContext with SQL Server
- [x] Service communication: PaymentService ↔ ReservationsService

**Status:** ✅ Complete (April 2-3, 2026)
- ✅ PaymentService created and tested
- ✅ Stripe test integration working (pm_card_visa, pm_card_mastercard)
- ✅ Full payment flow verified: create → pay → confirm reservation status
- ✅ PaymentDb with proper indexes and constraints
- ✅ Environment variable configuration for Stripe keys
- ✅ ReservationsService status updates on payment success

---

## 🎨 Phase 2: Polish & Integration ⏳

### Goal: Unified API gateway + complete service communication

#### 2.1 Configure Swagger for Ocelot ⏳
- [ ] Add MMLib.SwaggerForOcelot
- [ ] Aggregate all service Swagger docs in gateway

**Status:** ⏳ Not Started
- Ocelot gateway not created yet
- Swagger aggregation not configured

#### 2.2 Implement Complete Inter-service Communication 🚧 (50% complete)
- [x] PaymentService → ReservationsService (status updates) ✅
- [x] UserService → AuthService (token generation/validation) ✅
- [ ] ReservationsService → UserService (user validation)
- [ ] ReservationsService → ResourcesService (availability checking)
- [ ] ResourcesService → UserService (user filtering)
- [ ] Services call each other (full mesh) ❌

**Status:** 🚧 In Progress (50%)
- Payment service communication complete ✅
- Auth service communication complete ✅
- Need resource/user validation and availability checking ❌

#### 2.3 Implement Authentication Integration ⏳
- [ ] Add [Authorize] to ResourcesService
- [ ] Add [Authorize] to ReservationsService
- [ ] Add [Authorize] to PaymentService
- [ ] Implement JWT validation middleware for all services
- [ ] Add token refresh flow to clients

**Status:** ⏳ Not Started
- Services have no authentication protection yet
- Need to configure JWT validation per service

---

## 🚀 Phase 3: Deployment & Bonus ⏳

### Goal: Production-ready + extra credit

#### 3.1 Deploy to Azure ⏳
- [ ] Create Azure SQL databases
- [ ] Deploy Web Apps
- [ ] Configure connection strings in Azure

**Status:** ⏳ Not Started
- Local development only
- No deployment configuration

#### 3.2 Notifications Service (Bonus 5%) ⏳
- [ ] Email/SMS confirmations
- [ ] Can be mock implementation

**Status:** ⏳ Not Started
- Service does not exist yet

---

## 📊 Overall Progress

| Phase | Description | Status | Progress |
|-------|-------------|--------|----------|
| **Phase 1** | Core MVP | ✅ Complete | 100% |
| **Phase 2** | Polish & Integration | ⏳ In Progress | 25% |
| **Phase 3** | Deployment & Bonus | ⏳ Not Started | 0% |

### Detailed Phase 1 Progress

| Task | Status | Notes |
|------|--------|-------|
| EF Core - UserService | ✅ Complete | Commit: d86eb1f |
| EF Core - ResourcesService | ✅ Complete | Commit: 5c47624 |
| EF Core - ReservationsService | ✅ Complete | Commit: 5c47624 |
| EF Core - AuthService | ✅ Complete | Commit: a2e7f72 |
| EF Core - PaymentService | ✅ Complete | Commit: ff12b1d |
| JWT Authentication | ✅ Complete | With refresh tokens (April 1-2) |
| Payment Service with Stripe | ✅ Complete | With Stripe.net (April 2-3) |
| CRUD Operations | ✅ Complete | All 5 services tested |
| Service Communication | 🚧 Partial | Payment↔Reservations & User↔Auth working |

---

## 🎯 Next Steps (Priority Order)

### HIGH PRIORITY (Complete Phase 2)
1. **Implement Authentication Integration**
   - Add [Authorize] attributes to ResourcesService
   - Add [Authorize] attributes to ReservationsService
   - Add [Authorize] attributes to PaymentService
   - Configure JWT validation middleware
   - Test authenticated requests across all services

2. **Complete Service Communication**
   - ReservationsService → UserService (validate user existence)
   - ReservationsService → ResourcesService (check availability)
   - ResourcesService → UserService (get user-owned resources)
   - Implement error handling for service downtime

3. **Create Ocelot Gateway**
   - Set up ApiGateway
   - Configure routes to all 5 microservices
   - Aggregate Swagger documentation
   - Add gateway-level authentication

### MEDIUM PRIORITY (Testing & Polish)
4. **End-to-End Integration Tests**
   - Test full flow: register → login → jwt → reserve → pay
   - Test authentication failure scenarios
   - Test payment failure handling

5. **Documentation Updates**
   - Update API documentation with authentication requirements
   - Document service communication patterns
   - Add integration testing examples

### LOW PRIORITY (Phase 3 - Bonus)
6. **Prepare for Deployment**
   - Configure Azure SQL connection strings
   - Set up production environment variables
   - Create deployment scripts
   - Document deployment process

7. **(Optional) Notification Service**
   - Create notification microservice
   - Implement email/SMS triggers
   - Mock notification delivery

---

## 💡 Technical Considerations

### Service Discovery
Options for inter-service communication:
- **Fixed URLs** (simplest, currently implemented): Hardcode localhost ports in development
- **Ocelot Gateway** (recommended): Single entry point, routes to services
- **Service Registry** (advanced): Dynamic service discovery

### Authentication Pattern (Chosen)
- **Current Approach:** Each service calls AuthService for token generation/validation
- **Target Approach:** Ocelot Gateway validates JWT and passes user context
- **Backup Approach:** Each service validates JWT independently (shared secret)

### Database Migration Strategy
- **Development:** Auto-migration (currently implemented)
- **Production:** Explicit migrations with versioning
- **Separation:** Each service has its own database (microservices pattern)

### Payment Flow (Implemented)
- Stripe PaymentIntent creation with PaymentMethodId
- Auto-confirmation when payment method provided
- Status tracking: Pending → Succeeded/Failed/Canceled/Refunded
- Service integration: PaymentService calls ReservationsService for status updates

---

## 📝 Service Summary

| Service | Database | Port | Status | Next Steps |
|---------|----------|------|--------|------------|
| UserService | UserDb | 5000 | ✅ Complete | [Authorize] integration |
| ResourcesService | ResourceDb | 5001 | ✅ Complete | [Authorize] + service communication |
| ReservationsService | ReservationDb | 5002 | ✅ Complete | [Authorize] + service communication |
| AuthService | AuthDb | 6001 | ✅ Complete | Ready for Ocelot gateway |
| PaymentService | PaymentDb | 5003 | ✅ Complete | [Authorize] + error handling |
| NotificationService | NotificationDb | 5004 | ⏳ Bonus | Create service |

---

## 🔄 Branch Strategy

- **`main`** - Baseline from original homework code
- **`V.Alpha`** - Development branch for Phase 1 Core MVP **(CURRENT - 100% COMPLETE)**
- **`V.Beta`** (future) - Phase 2 integration work
- **`V.Release`** (future) - Production-ready code

---

*Last Updated: April 3, 2026 at 04:30 UTC*
*Phase 1 MVP Status: COMPLETE ✅*
*Next Phase: Integration & Authentication*