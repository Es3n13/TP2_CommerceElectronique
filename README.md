# TP2 Commerce Électronique - Système de Réservation en Ligne

## Description

Projet de fin de cours pour **INF27523 - Technologies du commerce électronique** (UQAR, Hiver 2026).

Système de réservation en ligne basé sur une microservices architecture avec ASP.NET Core.

## Architecture

### Microservices

- **ApiGateway** - Passerelle API (Ocelot) pour centraliser les requêtes
- **ResourcesService** - Gestion des ressources (chambres, salles, événements)
- **ReservationsService** - Gestion des réservations
- **userservice** - Gestion des utilisateurs

## Technologies

- **Framework**: ASP.NET Core 10.0
- **Langage**: C#
- **Passerelle API**: Ocelot
- **Documentation**: Swagger
- **Database**: Entity Framework Core + SQL Server (TODO)
- **Authentification**: JWT (TODO)
- **Paiement**: Stripe (TODO)

## Branches

- `main` - Code initial depuis Nextcloud
- `V.Alpha` - Branche de développement

## Installation

```bash
# Clone le repository
git clone https://github.com/Es3n13/TP2_CommerceElectronique.git

# Restaurer les dépendances
dotnet restore

# Compiler la solution
dotnet build
```

## Exécution

```bash
# Lancer tous les services (plusieurs terminaux requis)
dotnet run --project ApiGateway
dotnet run --project userservice
dotnet run --project ResourcesService
dotnet run --project ReservationsService
```

## Endpoints

### userservice
- `GET /api/users` - Liste tous les utilisateurs
- `POST /api/users` - Crée un nouvel utilisateur

### ResourcesService
- `GET /api/resources` - Liste toutes les ressources
- `POST /api/resources` - Crée une nouvelle ressource

### ReservationsService
- `GET /api/reservations` - Liste toutes les réservations
- `POST /api/reservations` - Crée une nouvelle réservation

### ApiGateway
- Tous les endpoints des microservices passent par le gateway

## Documentation Swagger

Chaque service expose Swagger sur :
- `https://localhost:{port}/swagger`

## Équipe

- **Es3n13** (Snoop Frogg)

## License

Ce projet est un travail académique pour l'UQAR.