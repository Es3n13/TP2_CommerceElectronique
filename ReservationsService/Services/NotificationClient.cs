using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ReservationsService.Services
{
    public class NotificationClient : INotificationClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NotificationClient> _logger;

        public NotificationClient(IHttpClientFactory httpClientFactory, ILogger<NotificationClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task SendConfirmationAsync(int userId, int reservationId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("NotificationService");

                var payload = new
                {
                    UserId = userId,
                    Content = $"Your reservation #{reservationId} has been successfully confirmed!",
                    Channel = "Email"
                };

                var response = await client.PostAsJsonAsync("api/notifications", payload);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NotificationService returned {StatusCode} when sending confirmation to User {UserId}",
                    response.StatusCode, userId);
                }
            }
            catch (Exception ex)
            {
                // Log the failure but do NOT throw. This prevents a notification failure
                // from rolling back a successful reservation update.
                _logger.LogError(ex, "Failed to send confirmation notification for User {UserId}, Reservation {ReservationId}",
                userId, reservationId);
            }
        }
    }
}