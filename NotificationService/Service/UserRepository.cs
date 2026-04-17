using System.Net.Http.Json;

namespace NotificationService.Service
{
    public class UserRepository : IUserRepository
    {
        private readonly HttpClient _httpClient;

        public UserRepository(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("UserService");
        }

        public async Task<string?> GetEmailByIdAsync(int userId)
        {
            try
            {
                var user = await _httpClient.GetFromJsonAsync<UserDto>($"api/users/{userId}");
                return user?.Email;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private class UserDto
        {
            public int Id { get; set; }
            public string Email { get; set; } = string.Empty;
        }
    }
}
