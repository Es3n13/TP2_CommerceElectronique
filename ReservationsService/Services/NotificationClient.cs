using System.Net.Http.Json;

namespace ReservationsService.Services
{
    public interface INotificationClient
    {
        Task SendNotificationAsync(int userId, string message);
    }

    public class NotificationClient : INotificationClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public NotificationClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

        public async Task SendNotificationAsync(int userId, string message)
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            // 0 = Email
            var request = new { UserId = userId, Content = message, Channel = 0 };
            await client.PostAsJsonAsync("api/notification/send", request);
        }
    }
}