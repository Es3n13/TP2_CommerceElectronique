# TP2 Commerce Électronique - Plateforme de Microservices E-Commerce

**Cours :** INF27523 - Technologies du commerce électronique  
**Trimestre :** Hiver 2026  
**Institution :** UQAR (Université du Québec à Rimouski)  
**Dépôt :** `Es3n13/TP2_CommerceElectronique`  
**Branche :** `V.Beta` (Stable/En pause) | `V.Alpha` (Développement) | `main` (Base)

---

## 🎯 Aperçu du Projet

Une plateforme de microservices e-commerce **headless** prête pour la production, construite avec ASP.NET Core. Ce projet implémente une architecture backend uniquement (Web APIs) conçue pour être consommée par des frontends externes ou intégrée via une passerelle d'API (API Gateway).

**État actuel :** ❄️ **EN PAUSE** (Stabilisée et finalisée pour la phase actuelle).

**Caractéristiques principales :**
- ✅ 6 microservices pleinement fonctionnels
- ✅ Authentification basée sur JWT pour tous les services
- ✅ Passerelle API Ocelot avec authentification unifiée
- ✅ Intégration des paiements Stripe
- ✅ Interface Swagger améliorée avec sécurité (cadenas d'autorisation)
- ✅ Maillage de communication complet entre les services
- ✅ Bases de données SQL Server par service
- ✅ Agrégation Swagger avec MMLib.SwaggerForOcelot
- ✅ Configuration CORS résolue
- ✅ Standardisation sur .NET 10.0 pour tous les services

---

## 🔥 Mises à jour récentes (15 avril 2026)

### 🔧 Stabilisations Finales
1. **Vérification de l'architecture Headless ✅**
   - Confirmation du système comme une suite pure de Web API.
   - Aucune implémentation frontend ; toutes les interactions se font via SwaggerUI ou des appels API.
2. **Intégration du service de notification ✅**
   - Implémentation et vérification réussies du `NotificationService`.
   - Les services ont désormais la capacité d'envoyer des notifications (vérifié et fonctionnel).
3. **Stabilité du UserService ✅**
   - Vérification finale de la stabilité du `UserService` et de son intégration avec le flux d'authentification.
4. **État du projet : En pause ❄️**
   - Le projet est maintenant dans un état stabilisé, avec toutes les fonctionnalités de base et bonus pour la phase actuelle implémentées et vérifiées.

---

## 🏗️ Architecture

### Microservices

| Service | Base de données | Port | Rôle | Auth JWT |
|---------|----------------|------|---------|----------|
| **AuthService** | AuthDb | 6001 | Génération et validation de jetons JWT | ✅ Oui |
| **UserService** | UserDb | 5000 | Gestion des utilisateurs et authentification | ✅ Oui |
| **ResourcesService** | ResourceDb | 5001 | Gestion des produits/ressources | ✅ Oui |
| **ReservationsService** | ReservationDb | 5002 | Gestion des réservations/commandes | ✅ Oui |
| **PaymentService** | PaymentDb | 5003 | Traitement des paiements (Stripe) | ✅ Oui |
| **NotificationService**| NotificationDb | 5004 | Notifications système (Bonus) | ✅ Oui |
| **ApiGateway** | - | 8080 | Point d'entrée API unifié + auth JWT | ✅ Oui |

### Pile Technologique

- **Framework :** .NET 10.0 (standardisé pour tous les services)
- **Langage :** C#
- **Bases de données :** SQL Server (une par service)
- **ORM :** Entity Framework Core
- **Authentification :** JWT (HS256)
- **Passerelle API :** Ocelot 24.1.0
- **Paiement :** Stripe.net v47.3.0
- **Documentation API :** Swashbuckle.AspNetCore v10.1.7
- **Agrégation Swagger :** MMLib.SwaggerForOcelot v10.0.0
- **OpenAPI :** Microsoft.OpenApi v2.4.1

### Couches de Sécurité

```
┌─────────────────────────────────────────────────────────────┐
│                    Passerelle API Ocelot                    │
│  Port : 8080 | Validation JWT | Configuration CORS          │
└─────────────────────┬───────────────────────────────────────┘
                      │
      ┌───────────────┼───────────────┐
      │               │               │
┌─────▼─────┐   ┌─────▼─────┐   ┌─────▼─────┐
│ UserService  │  Resources    │ Reservations
│  (Port 5000)  │  (Port 5001)  │  (Port 5002)
│  Auth JWT ✅  │  Auth JWT ✅ │  Auth JWT ✅
└─────┬─────┘   └─────┬─────┘   └─────┬─────┘
      │               │               │
      └───────────────┼───────────────┘
                      │
      ┌───────────────▼───────────────┐
      │  AuthService (Port 6001)      │
      │  Émetteur de jetons JWT ✅     │
      └───────────────────────────────┘
                      │
      ┌───────────────▼───────────────┐
      │  PaymentService (Port 5003)   │
      │  Intégration Stripe ✅        │
      │  Auth JWT ✅                  │
      └───────────────────────────────┘
```

---

## 🚀 Démarrage Rapide

### Prérequis

- SDK .NET 10.0
- SQL Server (ou SQL Express)
- Node.js (pour OpenClaw si applicable)

### Installation

1. **Cloner le dépôt :**
   ```bash
   git clone https://github.com/Es3n13/TP2_CommerceElectronique.git
   cd TP2_CommerceElectronique
   git checkout V.Beta
   ```

2. **Restaurer les dépendances :**
   ```bash
   dotnet restore
   ```

3. **Configurer les bases de données :**
   - Mettre à jour les chaînes de connexion dans `appsettings.json` pour chaque service
   - Par défaut : `(localdb)\\mssqllocaldb`

4. **Compiler la solution :**
   ```bash
   dotnet build
   ```

### Exécution des Services

**Option 1 : Manuelle (7 terminaux) :**
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

## 📡 Points d'Accès

### Passerelle (Point d'entrée unifié)
- **URL de base :** `http://localhost:8080`
- **Swagger UI :** `http://localhost:8080/swagger`
- **Swagger agrégé :** `http://localhost:8080/swagger/docs`

---

## 🔐 Authentification

### Flux JWT

1. **S'enregistrer :** `POST /api/users/register`
2. **Se connecter :** `POST /api/users/login` $\rightarrow$ Obtenir le jeton
3. **Accès :** Utiliser l'en-tête `Authorization: Bearer {token}`

---

## 🔒 Guide d'autorisation Swagger UI

### Utilisation du cadenas "Authorize"

1. **Obtenir le jeton** via `/api/users/login`
2. **Cliquer sur 🔒 Authorize** dans n'importe quelle interface Swagger UI
3. **Saisir :** `Bearer eyJhbGci... `
4. **Tester** les points de terminaison protégés.

---

## 🔃 Révocation des jetons JWT ✅

La plateforme inclut un système complet de révocation de jetons JWT utilisant Redis pour une invalidation immédiate.

**Points de terminaison API :**
- `POST /api/TokenRevocation/revoke` - Jeton unique
- `GET /api/TokenRevocation/check/{tokenJti}` - Vérification du statut
- `POST /api/TokenRevocation/revoke-all/{userId}` - Tous les jetons d'un utilisateur
- `POST /api/TokenRevocation/cleanup` - Purge des jetons expirés

---

## 💳 Intégration des Paiements

### Implémentation Stripe
- **Créer une intention de paiement :** `POST /api/payments/create`
- **Retourne :** `paymentIntentId` et `clientSecret` pour l'intégration frontend.

---

## 🔄 Communication entre Services

- **Appels client HTTP directs** pour la communication interne.
- **Confiance service-à-service** (aucune authentification interne requise).
- **La passerelle API** gère toute la validation JWT externe.

---

## 📊 Documentation de l'API

- **Swagger agrégé de la passerelle :** `http://localhost:8080/swagger`
- **Swaggers individuels des services :** Accessibles via `/swagger` sur le port de chaque service.

---

## 🧪 Tests

```bash
dotnet test
```

---

## 📁 Structure du Projet

```
TP2_CommerceElectronique/
├── AuthService/              # Service d'authentification JWT
├── UserService/              # Gestion des utilisateurs
├── ResourcesService/         # Produits/ressources
├── ReservationsService/      # Réservations/commandes
├── PaymentService/           # Traitement des paiements
├── NotificationService/      # Notifications système (Bonus)
├── ApiGateway/               # Passerelle Ocelot
└── TP2_CommerceElectronique.sln
```

---

## 📈 Progrès

### Phase 1 : MVP de base ✅ TERMINÉ
- [x] Intégration EF Core pour tous les services
- [x] Authentification JWT avec jetons de rafraîchissement
- [x] Service de paiement avec Stripe
- [x] Toutes les opérations CRUD
- [x] Maillage de communication entre services

### Phase 2 : Intégration ✅ TERMINÉ
- [x] Passerelle API Ocelot
- [x] Authentification JWT au niveau de la passerelle
- [x] Communication service-à-service
- [x] Agrégation Swagger (MMLib.SwaggerForOcelot)
- [x] Configuration CORS résolue
- [x] Définitions de sécurité Swagger (cadenas d'autorisation)
- [x] Standardisation du framework (.NET 10.0)
- [x] Bonus : Service de notification implémenté et vérifié

### Phase 3 : Déploiement ⏳ NON COMMENCÉ
- [ ] Déploiement Azure
- [ ] Configuration de production

---

## 🐛 Problèmes Connus

**✅ RÉSOLU - Problèmes de CORS**
- Corrigé : Suppression de la redirection HTTPS, mise à jour de la configuration CORS.

**✅ RÉSOLU - Autorisation Swagger**
- Corrigé : Ajout du cadenas "Authorize" à tous les services.

---

## 🛠️ Configuration

- **Framework .NET :** 10.0
- **Chaînes de connexion :** SQL Server `(localdb)\mssqllocaldb`
- **Paiement :** Stripe.net v47.3.0

---

## 👥 Auteurs

- **Snoop Frogg** (@snoopfrogg7085)
- Cours : INF27523 - Technologies du commerce électronique
- Trimestre : Hiver 2026

---

*Dernière mise à jour : 15 avril 2026 | État du projet : ❄️ En pause*
