using Microsoft.Extensions.Logging;
using NotificationService.Interface;
using NotificationService.Models;

namespace NotificationService.Services;

public class NotificationDispatcher
{
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(IEnumerable<INotificationProvider> providers, ILogger<NotificationDispatcher> logger)
    {
        _providers = providers;
        _logger = logger;
    }
    public async Task DispatchAsync(NotificationService.Models.Notification notification)
    {
        var provider = _providers.FirstOrDefault(p => p.Channel == notification.Channel);
        if (provider == null)
        {
            _logger.LogError("No provider found for channel {Channel}", notification.Channel);
            notification.Status = NotificationStatus.Failed;
            return;
        }

        try
        {
            var success = await provider.SendAsync(notification);
            notification.Status = success ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SentAt = success ? DateTime.UtcNow : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching notification {Id}", notification.Id);
            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = ex.Message;
        }
    }
}