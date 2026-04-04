using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using MMLib.SwaggerForOcelot.Configuration;
using MMLib.SwaggerForOcelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load Ocelot configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Configure JWT authentication at gateway level
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"]
    ?? "sk_dyb3FYyquQA3w8ZtrRVeJS7iIn2IXA2g";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
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

// SwaggerForOcelot services (aggregates Swagger from all services)
builder.Services.AddSwaggerForOcelot(builder.Configuration, (o) =>
{
    o.PathToSwaggerGenerator = "/swagger/v1/swagger.json";
});

// Ocelot configuration
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// Enable SwaggerForOcelot (aggregates Swagger from all services)
app.UseSwaggerForOcelot(builder.Configuration, (o) =>
{
    o.PathToSwaggerGenerator = "/swagger/v1/swagger.json";
});

// Enable authentication middleware
app.UseAuthentication();
app.UseAuthorization();

// Use Ocelot
await app.UseOcelot();

app.Run();