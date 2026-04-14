using Microsoft.Extensions.Logging;
using NotificationService.Interface;
using NotificationService.Models;

namespace NotificationService.Services;

public class MockNotificationProvider : INotificationProvider
{
    private readonly ILogger<MockNotificationProvider> _logger;
    public NotificationChannel Channel { get; }

    public MockNotificationProvider(NotificationChannel channel, ILogger<MockNotificationProvider> logger)
    {
        Channel = channel;
        _logger = logger;
    }

    public async Task<bool> SendAsync(Notification notification)
    {
        _logger.LogInformation("[MOCK] Sending {Channel} to User {UserId}: {Content}", Channel, notification.UserId, notification.Content);
        await Task.Delay(100); // Simulate network lag
        return true; // Always succeed for the PoC
    }
}
