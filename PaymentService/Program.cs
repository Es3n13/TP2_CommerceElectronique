using Stripe;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using Microsoft.OpenApi;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Enregistre le contexte EF Core avec SQL Server
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PaymentDbConnection")
    )
);

// Enregistre un client HTTP pour le service des réservations
builder.Services.AddHttpClient("ReservationsService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002");
});

// Enregistre le service Stripe
builder.Services.AddScoped<PaymentService.Services.StripeService>();

// Initialise la configuration Stripe
var stripeSettings = builder.Configuration.GetSection("Stripe");
StripeConfiguration.ApiKey = stripeSettings["SecretKey"];

// Configure l'authentification JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? "sk_dyb3FYyquQA3w8ZtrRVeJS7iIn2IXA2g";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Règles de validation du token JWT
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

builder.Services.AddAuthorization();

// Enregistre les contrôleurs et les services Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Document Swagger 
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PaymentService API",
        Version = "v1"
    });

    // Schéma JWT Bearer dans Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}' below."
    });

    // Schéma Bearer aux endpoints
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service API v1");
    });

if (app.Environment.IsDevelopment())
{
    // Crée la base si elle n'existe pas encore
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    context.Database.EnsureCreated();
}

// Active le pipeline de sécurité et mappe les contrôleurs
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Démarre l'application
app.Run();