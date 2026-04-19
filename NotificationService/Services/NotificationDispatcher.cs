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

    // Injecte le contexte EF Core et le logger
    public NotificationDispatcher(
        IEnumerable<INotificationProvider> providers,
        NotificationDbContext context,
        ILogger<NotificationDispatcher> logger)
    {
        _providers = providers;
        _context = context;
        _logger = logger;
    }

    // Sélectionne un provider et tente l'envoi de la notification
    public async Task DispatchAsync(NotificationService.Models.Notification notification)
    {
        // Sélectionne le provider selon le canal demandé
        var provider = _providers.FirstOrDefault(p => p.Channel == notification.Channel);

        if (provider == null)
        {
            // Journalise l'absence de provider compatible
            _logger.LogError("No provider found for channel {Channel}", notification.Channel);

            // Marque la notification comme échouée
            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = "No provider found for the requested channel.";

            // Persiste l'état d'échec en bd
            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync();
            return;
        }

        try
        {
            // Délègue l'envoi au provider sélectionné
            var success = await provider.SendAsync(notification);

            // Met à jour le statut selon le résultat
            notification.Status = success ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SentAt = success ? DateTime.UtcNow : null;

            if (!success)
            {
                // Stocke un message d'erreur
                notification.ErrorMessage = "Provider failed to send notification.";
            }
        }
        catch (Exception ex)
        {
            // Journalise l'exception
            _logger.LogError(ex, "Error dispatching notification {Id}", notification.Id);

            // Marque la notification comme échouée
            notification.Status = NotificationStatus.Failed;
            notification.ErrorMessage = ex.Message;
        }

        // Stock le résultat final de l'envoi
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync();
    }
}