using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDbConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var inboxRoot = builder.Configuration["App:InboxRoot"] ?? "/root/.openclaw/workspace/notifications/inbox";

// POST /notify
app.MapPost("/notify", async (NotificationRequest request, NotificationDbContext db) =>
{
    var notification = new Notification
    {
        UserId = request.UserId,
        Message = request.Message,
        Type = request.Type,
        CreatedAt = DateTime.UtcNow,
        IsRead = false
    };

    db.Notifications.Add(notification);
    await db.SaveChangesAsync();

    // Simulate "delivery" by writing an HTML file
    var userInboxDir = Path.Combine(inboxRoot, request.UserId);
    Directory.CreateDirectory(userInboxDir);

    var filePath = Path.Combine(userInboxDir, $"{notification.Id}.html");
    var htmlContent = $@"
    <html>
        <body style='font-family: Arial, sans-serif; margin: 20px;'>
            <div style='border: 1px solid #ccc; padding: 20px; border-radius: 10px; max-width: 600px;'>
                <h2 style='color: #333;'>Notification: {notification.Type}</h2>
                <p style='font-size: 16px; color: #666;'>{notification.Message}</p>
                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;' />
                <p style='font-size: 12px; color: #999;'>Sent on: {notification.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
                <p style='font-size: 12px; color: #999;'>Notification ID: {notification.Id}</p>
            </div>
        </body>
    </html>";

    await File.WriteAllTextAsync(filePath, htmlContent, Encoding.UTF8);

    return Results.Ok(new { Id = notification.Id, Message = "Notification sent and archived in virtual inbox." });
});

// GET /inbox/{userId}
app.MapGet("/inbox/{userId}", async (string userId, NotificationDbContext db) =>
{
    var notifications = await db.Notifications
        .Where(n => n.UserId == userId)
        .OrderByDescending(n => n.CreatedAt)
        .ToListAsync();
    
    return Results.Ok(notifications);
});

// GET /view/{id}
app.MapGet("/view/{id}", async (int id, NotificationDbContext db) =>
{
    var notification = await db.Notifications.FindAsync(id);
    if (notification == null) return Results.NotFound("Notification not found.");

    var filePath = Path.Combine(inboxRoot, notification.UserId, $"{notification.Id}.html");

    if (!File.Exists(filePath)) return Results.NotFound("Notification delivery file not found.");

    var htmlContent = await File.ReadAllTextAsync(filePath);
    return Results.Content(htmlContent, "text/html");
});

app.Run("http://0.0.0.0:5005");

// Model for POST /notify
public record NotificationRequest(string UserId, string Message, string Type);
