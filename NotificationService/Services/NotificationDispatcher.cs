using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Interface;
using NotificationService.Models;
using NotificationService.Data;

namespace NotificationService.Services;

public class NotificationDispatcher
{
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly NotificationDbContext _context;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(IEnumerable<INotificationProvider> providers, NotificationDbContext context, ILogger<NotificationDispatcher> logger)
    {
        _providers = providers;
        _context = context;
        _logger = logger;
    }
    public async Task DispatchAsync(NotificationService.Models.Notification notification)
    {
        var provider = _providers.FirstOrDefault(p => p.Channel == notification.Channel);
        if (provider == null)
        {
            _logger.LogError("No provider found for channel {Channel}", notification.Channel);
            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = "No provider found for the requested channel.";

            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync();
            return;
        }

        try
        {
            var success = await provider.SendAsync(notification);
            notification.Status = success ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SentAt = success ? DateTime.UtcNow : null;
            if (!success)
            {
                notification.ErrorMessage = "Provider failed to send notification.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching notification {Id}", notification.Id);
            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = ex.Message;
        }

        // Update persistence with result
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync();
    }
}