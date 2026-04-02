namespace AuthService.Services
{
    public interface ITokenService
    {
        Task<string> GenerateTokenAsync(User user);
        Task<string> GenerateRefreshTokenAsync(User user, string jwtId);
        Task<bool> ValidateRefreshTokenAsync(string refreshToken);
        Task RevokeRefreshTokenAsync(string refreshToken, string? reason = null);
    }
}