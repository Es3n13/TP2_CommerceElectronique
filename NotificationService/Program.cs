using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NotificationService.Data;
using NotificationService.Interface;
using NotificationService.Models;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Enregistre les contrôleurs API
builder.Services.AddControllers();

// Enregistre la génération du document OpenAPI
builder.Services.AddOpenApi();

// Enregistre le dispatcher des notifications
builder.Services.AddScoped<NotificationDispatcher>();

// Enregistre un provider mock pour le canal Email
builder.Services.AddSingleton<INotificationProvider>(sp =>
    new MockNotificationProvider(
        NotificationChannel.Email,
        sp.GetRequiredService<ILogger<MockNotificationProvider>>()));

// Enregistre un provider mock pour le canal Sms
builder.Services.AddSingleton<INotificationProvider>(sp =>
    new MockNotificationProvider(
        NotificationChannel.Sms,
        sp.GetRequiredService<ILogger<MockNotificationProvider>>()));

// Enregistre le contexte EF Core avec SQL Server
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Enregistre Swagger pour la documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Document Swagger principal
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Notification Service API v1",
        Version = "v1"
    });
});

// Politique CORS autorisant uniquement la gateway
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGateway",
        policy => policy.WithOrigins("http://localhost:8080")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1");
    });
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    context.Database.EnsureCreated();
    app.MapOpenApi();
}

// Politique CORS pour les appels du gateway
app.UseCors("AllowGateway");

// Active l'autorisation HTTP
app.UseAuthorization();

// Mappe les routes des contrôleurs
app.MapControllers();

// Démarre l'application
app.Run();