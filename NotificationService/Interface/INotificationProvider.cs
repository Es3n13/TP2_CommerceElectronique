using NotificationService.Models;

namespace NotificationService.Interface
{
    public interface INotificationProvider
    {
        NotificationChannel Channel { get; }
        Task<bool> SendAsync(NotificationService.Models.Notification notification);
    }
}
