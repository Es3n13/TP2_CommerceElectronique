namespace NotificationService.Models;

public class NotificationRequest
{
    public int UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; } // Email, Sms
}