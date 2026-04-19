using Microsoft.EntityFrameworkCore;
using UserService.Data;
using Microsoft.OpenApi;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Enregistre le contexte EF Core avec SQL Server
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("UserDbConnection")
    )
);

// Enregistre un client HTTP vers AuthService
builder.Services.AddHttpClient("AuthService", client =>
{
    client.BaseAddress = new Uri("http://localhost:6001");
});

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
    // Paramètres de validation du JWT
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

// Enregistre les contrôleurs et Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Document Swagger principal
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UserService API",
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

    // Schéma Bearer aux endpoints documentés
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Active Swagger UI
app.UseSwagger();
app.UseSwaggerUI();


// Active l'authentification avant l'autorisation
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        context.Database.EnsureCreated();
    }
}

// Mappe les routes des contrôleurs
app.MapControllers();

// Démarre l'application
app.Run();