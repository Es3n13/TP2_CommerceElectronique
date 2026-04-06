using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using AuthService.Services;
using AuthService.Data;
using AuthService.Middleware;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"]!
    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

// Register HttpClient
builder.Services.AddHttpClient();

// Register Database Context
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AuthDbConnection")
    )
);

// Register Services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<RevokedAccessTokenService>();
builder.Services.AddScoped<IJwtRevocationValidationService, JwtRevocationValidationService>();

// Register Redis for token revocation caching
try
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        try
        {
            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            logger.LogInformation("Redis connection established at {RedisConnectionString}", redisConnectionString);
            return redis;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to Redis. Token revocation will fall back to database only.");
            return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false,connectTimeout=1000,syncTimeout=1000");
        }
    });
}
catch (Exception ex)
{
    var loggerFactory = builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger<Program>();
    logger.LogWarning(ex, "Redis configuration failed. Token revocation will use database only.");
}

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Inject custom events for revocation checking
    options.EventsType = typeof(JwtRevocationBearerEvents);
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();

// Register Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();