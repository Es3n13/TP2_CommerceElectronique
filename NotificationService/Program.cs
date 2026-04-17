using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NotificationService.Data;
using NotificationService.Interface;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Service;
using Azure.Identity;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register the Dispatcher
builder.Services.AddScoped<NotificationDispatcher>();

// Configure ACS and User Repository
builder.Services.Configure<AcsEmailOptions>(builder.Configuration.GetSection("AzureCommunicationServices"));
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddEmailClient(builder.Configuration["AzureCommunicationServices:Endpoint"], new DefaultAzureCredential());
});
builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UserServiceUrl"] ?? "http://localhost:5000");
});
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Register Providers
builder.Services.AddScoped<INotificationProvider, AcsEmailProvider>();

builder.Services.AddSingleton<INotificationProvider>(sp =>
    new MockNotificationProvider(NotificationChannel.Sms, sp.GetRequiredService<ILogger<MockNotificationProvider>>()));

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Notification Service API v1",
        Version = "v1"
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGateway",
    policy => policy.WithOrigins("http://localhost:8080")
    .AllowAnyMethod()
    .AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1");
    });

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowGateway");

app.UseAuthorization();

app.MapControllers();

app.Run();
