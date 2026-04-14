using NotificationService.Models;

namespace NotificationService.Services
{
    public class ConsoleNotificationProvider : INotificationProvider
    {
        public Task SendNotificationAsync(NotificationRequest request)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Magenta;
            
            Console.WriteLine($"\n[NOTIFICATION - {request.Type.ToUpper()}]");
            Console.WriteLine($"To User: {request.UserId}");
            Console.WriteLine($"Message: {request.Message}");
            Console.WriteLine("--------------------------------------------\n");
            
            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
    }
}
