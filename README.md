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

## ✅ Statut du Projet (mis à jour: 1 avril 2026)

**Phase 1: Core MVP - 40% complet**

| Service | Statut | Database | Authentification | Notes |
|---------|--------|----------|------------------|-------|
| **AuthService** | ✅ Opérationnel | Non (TODO) | JWT | Token generation + validation |
| **UserService** | ✅ Complet | UserDb | Via AuthService | Register, login, CRUD des utilisateurs |
| **ResourcesService** | ✅ Complet | ResourceDb | Non (TODO) | CRUD des ressources |
| **ReservationsService** | ✅ Complet | ReservationDb | Non (TODO) | CRUD des réservations |
| **PaymentService** | ❌ Pas commencé | - | - | TODO |
| **ApiGateway** | ❌ Pas commencé | - | - | TODO |

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

---

## 📊 Base de Données

### Connection Strings

```
UserDb: Server=(localdb)\\mssqllocaldb;Database=UserDb;Trusted_Connection=True;
ResourceDb: Server=(localdb)\\mssqllocaldb;Database=ResourceDb;Trusted_Connection=True;
ReservationDb: Server=(localdb)\\mssqllocaldb;Database=ReservationDb;Trusted_Connection=True;
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

---

## 🚀 Prochaines Étapes (Priorisation)

1. **EF Core pour AuthService** ⏰
   - créer AuthDb database
   - Stocker tokens / refresh tokens
   - Tracker l'expiration des tokens

2. **Protection des endpoints** ⏰
   - Ajouter `[Authorize]` sur ResourcesService
   - Ajouter `[Authorize]` sur ReservationsService
   - Valider tokens JWT

3. **Ocelot API Gateway** ⏰
   - Créer ApiGateway project
   - Router les requêtes aux microservices
   - Centraliser l'authentification

4. **Middleware de validation JWT** ⏰
   - Valider tokens dans chaque service
   - Extraire les claims utilisateur
   - Mettre en place le contexte utilisateur

5. **Service de Paiement** ⏰
   - Intégrer Stripe pour les paiements
   - Déclencher paiement après réservation
   - Mettre à jour le statut de réservation

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

### 1 avril 2026
- ✅ Création du service AuthService
- ✅ Implémentation JWT token generation + validation
- ✅ Intégration AuthService ↔ UserService via HttpClient
- ✅ Correction des ports (6001 pour AuthService)
- ✅ Correction Swagger (conflits de noms de classes)
- ✅ Flux d'authentification complet testé

### 31 mars 2026
- ✅ Correction "Invalid column name Pseudo" (migration AddPseudoColumn)
- ✅ Fix Swagger schema ID conflicts
- ✅ Mise à jour port configuration

---

*Pour plus de détails, voir le fichier mémoire: `/memory/2026-04-01.md`*
