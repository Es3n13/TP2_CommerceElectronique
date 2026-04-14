namespace NotificationService.Models
{
    public enum NotificationChannel { Email, Sms }
    public enum NotificationStatus { Pending, Sent, Failed }

    public class Notification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public NotificationChannel Channel { get; set; }
        public NotificationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
