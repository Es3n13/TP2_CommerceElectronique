namespace NotificationService.Models
{
    public class NotificationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "Email"; // Email, SMS, etc.
    }
}
