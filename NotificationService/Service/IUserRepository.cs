namespace NotificationService.Service
{
    public interface IUserRepository
    {
        Task<string?> GetEmailByIdAsync(int userId);
    }
}
