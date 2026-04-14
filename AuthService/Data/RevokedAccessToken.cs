namespace AuthService.Data
{
    public class RevokedAccessToken
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string TokenJti { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime RevokedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}