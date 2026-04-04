# TP2 Commerce Électronique - Système de Réservation en Ligne

## Description

Projet de fin de cours pour **INF27523 - Technologies du commerce électronique** (UQAR, Hiver 2026).

Système de réservation en ligne basé sur une microservices architecture avec ASP.NET Core.

## Architecture

### Microservices

- **AuthService** - Gestion de l'authentification et tokens JWT (nouveau)
- **UserService** - Gestion des utilisateurs et authentification
- **ResourcesService** - Gestion des ressources (chambres, salles, événements)
- **ReservationsService** - Gestion des réservations
- **ApiGateway** - Passerelle API (Ocelot) pour centraliser les requêtes (TODO)

---

## ✅ Statut du Projet (mis à jour: 3 avril 2026)

**Phase 1: Core MVP - 100% COMPLETE ✅**

| Service | Statut | Database | Authentification | Communication |
|---------|--------|----------|------------------|---------------|
| **AuthService** | ✅ Opérationnel | AuthDb | JWT | UserService ↔ AuthService |
| **UserService** | ✅ Complet | UserDb | Via AuthService | Crée tokens et valide |
| **ResourcesService** | ✅ Complet | ResourceDb | Non (TODO) | Non connecté |
| **ReservationsService** | ✅ Complet | ReservationDb | Non (TODO) | PaymentService → Reservations ✅ |
| **PaymentService** | ✅ Complet | PaymentDb | Non (TODO) | ReservationsService status updates ✅ |
| **ApiGateway** | ❌ Pas commencé | - | - | - |

---

## Technologies

- **Framework**: ASP.NET Core 10.0
- **Langage**: C#
- **ORM**: Entity Framework Core
- **Database**: SQL Server
- **Authentification**: JWT (HS256)
- **Hash des mots de passe**: BCrypt
- **Documentation**: Swagger
- **API Client**: HttpClient (IHttpClientFactory)

---

## Configuration des Ports

| Service | HTTP | HTTPS | Notes |
|---------|------|-------|-------|
| **AuthService** | 6001 | - | Port par défaut pour JWT |
| **UserService** | 5000 | 7075 | Appelle AuthService sur 6001 |
| **ResourcesService** | 5001 | - | REST API |
| **ReservationsService** | 5002 | - | REST API |
| **PaymentService** | 5003 | - | Stripe payment processing |
| **ApiGateway** | - | - | TODO |

---

## Installation

### Prérequis
- .NET 10.0 SDK
- SQL Server (local ou container Docker)
- Git

### Etapes

```bash
# Cloner le repository
git clone https://github.com/Es3n13/TP2_CommerceElectronique.git
cd TP2_CommerceElectronique

# Checkout la branche de développement
git checkout V.Alpha

# Restaurer les dépendances
dotnet restore

# Compiler la solution
dotnet build
```

---

## Exécution

### Lancer les services (plusieurs terminaux requis)

```bash
# Terminal 1 - AuthService (nécessaire pour l'authentification)
cd AuthService
dotnet run
# Écoute sur: http://localhost:6001

# Terminal 2 - UserService
cd UserService
dotnet run
# Écoute sur: http://localhost:5000

# Terminal 3 - ResourcesService
cd ResourcesService
dotnet run
# Écoute sur: http://localhost:5001

# Terminal 4 - ReservationsService
cd ReservationsService
dotnet run
# Écoute sur: http://localhost:5002

# Terminal 5 - PaymentService (optionnel)
cd PaymentService
dotnet run
# Écoute sur: http://localhost:5003
```

---

## Documentation Swagger

Chaque service expose Swagger:

| Service | URL Swagger |
|---------|------------|
| AuthService | http://localhost:6001/swagger |
| UserService | http://localhost:5000/swagger |
| ResourcesService | http://localhost:5001/swagger |
| ReservationsService | http://localhost:5002/swagger |
| PaymentService | http://localhost:5003/swagger |

---

## 🔐 JWT Authentification

### Configuration JWT

```csharp
SecretKey: sk_dyb3FYyquQA3w8ZtrRVeJS7iIn2IXA2g
Issuer: https://localhost:6001
Audience: TP2CommerceElectronique
Expiration: 60 minutes
Algorithm: HS256
```

### Flux d'authentification

1. **Enregistrement (/api/auth/register)**
   - Utilisateur s'inscrit avec pseudo, email, password
   - UserService crée l'utilisateur dans UserDb avec BCrypt hash
   - UserService appelle AuthService pour générer JWT token
   - JWT token est retourné

2. **Connexion (/api/auth/login)**
   - Utilisateur envoie email + password
   - UserService valide credentials avec BCrypt
   - UserService appelle AuthService pour générer JWT token
   - JWT token est retourné

3. **Validation de token (/api/auth/validate)**
   - Vérifie si le JWT est valide
   - Extrait les claims (UserId, Email, Role, etc.)
   - Retourne les informations utilisateur

### Tester avec Swagger

**AuthService:**

```bash
# Générer un token
POST /api/auth/token
Body:
{
  "userId": 1,
  "email": "test@example.com",
  "name": "TestUser",
  "role": "User",
  "firstName": "Test",
  "lastName": "User"
}

# Valider un token
POST /api/auth/validate
Body:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**UserService:**

```bash
# S'inscrire
POST /api/auth/register
Body:
{
  "pseudo": "testUser123",
  "email": "test@example.com",
  "password": "password123",
  "firstName": "Test",
  "lastName": "User",
  "phoneNumber": "555-0123"
}

# Se connecter
POST /api/auth/login
Body:
{
  "email": "test@example.com",
  "password": "password123"
}
```

---

## Endpoints

### AuthService (http://localhost:6001)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| POST | /api/auth/token | Génère un JWT token |
| POST | /api/auth/validate | Valide un JWT token |

### UserService (http://localhost:5000)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| POST | /api/auth/register | Enregistre un nouvel utilisateur |
| POST | /api/auth/login | Connexion utilisateur |
| GET | /api/users | Liste tous les utilisateurs |
| POST | /api/users | Crée un nouvel utilisateur (CRUD direct) |
| GET | /api/users/{id} | Get utilisateur par ID |
| GET | /api/users/email/{email} | Get utilisateur par email |
| PUT | /api/users/{id} | Met à jour un utilisateur |
| DELETE | /api/users/{id} | Supprime un utilisateur |

### ResourcesService (http://localhost:5001)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| GET | /api/resources | Liste toutes les ressources |
| POST | /api/resources | Crée une nouvelle ressource |
| GET | /api/resources/{id} | Get ressource par ID |
| PUT | /api/resources/{id} | Met à jour une ressource |
| DELETE | /api/resources/{id} | Supprime une ressource |

### ReservationsService (http://localhost:5002)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| GET | /api/reservations | Liste toutes les réservations |
| POST | /api/reservations | Crée une nouvelle réservation |
| GET | /api/reservations/{id} | Get réservation par ID |
| PUT | /api/reservations/{id} | Met à jour une réservation |
| DELETE | /api/reservations/{id} | Supprime une réservation |

### PaymentService (http://localhost:5003)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| POST | /api/payments/create | Créer une intention de paiement |
| GET | /api/payments/{id} | Détails d'un paiement |
| GET | /api/payments/reservation/{reservationId} | Paiements par réservation |
| POST | /api/payments/{id}/confirm | Confirmer un paiement |
| POST | /api/payments/{id}/refund | Rembourser un paiement |

---

## 📊 Base de Données

### Connection Strings

```
UserDb: Server=(localdb)\\mssqllocaldb;Database=UserDb;Trusted_Connection=True;
ResourceDb: Server=(localdb)\\mssqllocaldb;Database=ResourceDb;Trusted_Connection=True;
ReservationDb: Server=(localdb)\\mssqllocaldb;Database=ReservationDb;Trusted_Connection=True;
AuthDb: Server=(localdb)\\mssqllocaldb;Database=AuthDb;Trusted_Connection=True;
PaymentDb: Server=(localdb)\\mssqllocaldb;Database=PaymentDb;Trusted_Connection=True;
```

### Modèles de données

**User:**
- Id (int, primary key)
- Pseudo (string, required)
- Email (string, required, unique)
- PasswordHash (string)
- FirstName (string, nullable)
- LastName (string, nullable)
- PhoneNumber (string, nullable)
- CreatedAt (DateTime)
- Role (string, default: "User")

**Resource:**
- Id (int, primary key)
- Name (string)
- Type (string)
- Description (string)
- Capacity (int)
- IsAvailable (bool)
- PricePerDay (decimal)

**Reservation:**
- Id (int, primary key)
- ResourceId (int, foreign key)
- UserId (int, foreign key)
- StartDate (DateTime)
- EndDate (DateTime)
- Status (string)
- CreatedAt (DateTime)

**Payment:**
- Id (int, primary key)
- ReservationId (int, foreign key)
- Amount (decimal)
- StripePaymentIntentId (string)
- Status (string, enum: Pending, Succeeded, Failed, Canceled, Refunded)
- StripeErrorMessage (string, nullable)
- Currency (string, default: "cad")
- CreatedAt (DateTime)
- CompletedAt (DateTime, nullable)

---

## 🚀 Prochaines Étapes (Phase 2 - Integration)

1. **Protection des endpoints** ⏰
   - Ajouter `[Authorize]` sur ResourcesService
   - Ajouter `[Authorize]` sur ReservationsService
   - Ajouter `[Authorize]` sur PaymentService
   - Valider tokens JWT via middleware

2. **Service Communication** ⏰
   - ReservationsService → UserService (valider utilisateur)
   - ReservationsService → ResourcesService (vérifier disponibilité)
   - ResourcesService → UserService (filtre par utilisateur)

3. **Ocelot API Gateway** ⏰
   - Configurer ApiGateway project
   - Router les requêtes aux microservices
   - Centraliser l'authentification
   - Aggrégation Swagger docs

4. **Tests d'intégration** ⏰
   - Test flow complet: register → login → jwt → reserve → pay
   - Test scénarios d'erreur (paiement échoué, service down)
   - Documenter patterns de communication entre services

---

## Branches

- `main` - Code initial depuis Nextcloud
- `V.Alpha` - Branche de développement principale (actuelle)

---

## Équipe

- **Es3n13**

---

## ⚠️ Notes

- Tous les services utilisent `EnsureDeleted()` + `EnsureCreated()` en mode développement
- En production, utiliser les migrations EF Core à la place
- Les tokens JWT expirent après 60 minutes
- Les mots de passe sont hashés avec BCrypt

---

## License

Ce projet est un travail académique pour l'UQAR.

---

## 📝 Changelog Récent (V.Alpha)

### 3 avril 2026
- ✅ Phase 1 Core MVP - 100% COMPLETE
- ✅ PaymentDbContext fully implementation avec indexes
- ✅ Payment method support (PaymentMethodId parameter)
- ✅ Auto-confirmation de paiements Stripe (pm_card_visa, pm_card_mastercard)
- ✅ PaymentService ↔ ReservationsService communication working
- ✅ Test de paiement complet avec succès: create → pay → confirm reservation statut
- ✅ Solution file TP2_CommerceElectronique.sln pour Visual Studio

### 2-3 avril 2026
- ✅ Création du service PaymentService
- ✅ Intégration Stripe.net (v47.3.0)
- ✅ Implémentation payment intents, confirmation, refunds
- ✅ Ajout PaymentDbContext avec SQL Server (PaymentDb)
- ✅ Configuration ports standardisée

### 1-2 avril 2026
- ✅ Création du service AuthService
- ✅ Implémentation JWT token generation + validation + refresh tokens
- ✅ Intégration AuthService ↔ UserService via HttpClient
- ✅ Ajout AuthDbContext avec SQL Server
- ✅ Correction des ports (6001 pour AuthService)
- ✅ Flux d'authentification complet

### 31 mars 2026
- ✅ Correction "Invalid column name Pseudo" (migration AddPseudoColumn)
- ✅ Fix Swagger schema ID conflicts
- ✅ Mise à jour port configuration

---

*Pour plus de détails, voir le fichier mémoire: `/memory/2026-04-01.md`*
