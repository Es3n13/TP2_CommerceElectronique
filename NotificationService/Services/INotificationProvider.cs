using NotificationService.Models;

namespace NotificationService.Services
{
    public interface INotificationProvider
    {
        Task SendNotificationAsync(NotificationRequest request);
    }
}
