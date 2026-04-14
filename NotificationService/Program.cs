using Microsoft.EntityFrameworkCore;
using NotificationService.Interface;
using NotificationService.Models;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register the Dispatcher
builder.Services.AddScoped<NotificationDispatcher>();

// Register Mock Providers for all channels
builder.Services.AddSingleton<INotificationProvider>(sp =>
    new MockNotificationProvider(NotificationChannel.Email, sp.GetRequiredService<ILogger<MockNotificationProvider>>()));

builder.Services.AddSingleton<INotificationProvider>(sp =>
    new MockNotificationProvider(NotificationChannel.Sms, sp.GetRequiredService<ILogger<MockNotificationProvider>>()));

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
