using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using AuthService.Services;
using AuthService.Data;

var builder = WebApplication.CreateBuilder(args);

// Récupère la configuration JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Récupère la clé secrète utilisée pour signer les tokens
var secretKey = jwtSettings["SecretKey"]!
    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

// Enregistre HttpClient dans le conteneur DI
builder.Services.AddHttpClient();

// Enregistre le contexte EF Core avec SQL Server
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AuthDbConnection")
    )
);

// Enregistre le service de gestion des tokens
builder.Services.AddScoped<TokenService>();

// Configure l'authentification JWT Bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Paramètres de validation du JWT
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero 
    };
});

// Active le service d'autorisation
builder.Services.AddAuthorization();

// Active les contrôleurs MVC/API
builder.Services.AddControllers();

// Enregistre Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Déclare le document OpenAPI principal
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1"
    });

    // Schéma de sécurité Bearer dans Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    // Applique le schéma Bearer aux endpoints
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Active Swagger
app.UseSwagger();
app.UseSwaggerUI();

// Active l'authentification avant l'autorisation
app.UseAuthentication();
app.UseAuthorization();

// Mappe les routes des contrôleurs
app.MapControllers();

// Démarre l'application
app.Run();