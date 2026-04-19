using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using MMLib.SwaggerForOcelot.DependencyInjection;
using MMLib.SwaggerForOcelot.Middleware;

// Crée le builder principal de l'application
var builder = WebApplication.CreateBuilder(args);

// Charge la configuration Ocelot et les variables d'environnement
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Récupère la section JWT depuis la configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Utilise la clé secrète configurée ou une valeur par défaut
var secretKey = jwtSettings["SecretKey"] ?? "sk_dyb3FYyquQA3w8ZtrRVeJS7iIn2IXA2g";

// Configure l'authentification JWT Bearer par défaut
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Définit les règles de validation du token JWT
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "https://localhost:6001",
        ValidAudience = jwtSettings["Audience"] ?? "TP2CommerceElectronique",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// Configure une politique CORS ouverte pour toutes les origines
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Active le service d'autorisation
builder.Services.AddAuthorization();

// Ajoute Ocelot comme API Gateway
builder.Services.AddOcelot(builder.Configuration);

// Ajoute Swagger pour Ocelot
builder.Services.AddSwaggerForOcelot(builder.Configuration);

// Construit l'application
var app = builder.Build();

// Active CORS avant le pipeline Ocelot
app.UseCors("CorsPolicy");

// Expose l'interface Swagger agrégée de la gateway
app.UseSwaggerForOcelotUI(opt =>
{
    opt.PathToSwaggerGenerator = "/swagger/docs";
});

// Active le middleware Ocelot
await app.UseOcelot();

// Démarre l'application
app.Run();