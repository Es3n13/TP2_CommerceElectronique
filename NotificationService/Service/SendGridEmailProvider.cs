using Microsoft.Extensions.Options;
using NotificationService.Interface;
using NotificationService.Models;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Logging;

namespace NotificationService.Service;

public class SendGridEmailProvider : INotificationProvider
{
    private readonly ISendGridClient _client;
    private readonly SendGridOptions _options;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SendGridEmailProvider> _logger;

    public NotificationChannel Channel => NotificationChannel.Email;

    public SendGridEmailProvider(
        ISendGridClient client, 
        IOptions<SendGridOptions> options, 
        IUserRepository userRepository,
        ILogger<SendGridEmailProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<bool> SendAsync(Notification notification)
    {
        try
        {
            var email = await _userRepository.GetEmailByIdAsync(notification.UserId);
            
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError("Could not resolve email for UserId {UserId}. Email is required for SendGrid.", notification.UserId);
                return false;
            }

            var from = new EmailAddress(_options.FromEmail, _options.FromName);
            var to = new EmailAddress(email, "User");
            var msg = MailHelper.CreateSingleEmail(from, to, "Notification from Commerce Electronique", notification.Content, notification.Content);
            
            var response = await _client.SendEmailAsync(msg);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {Email} (UserId: {UserId}) via SendGrid", email, notification.UserId);
                return true;
            }
            
            _logger.LogError("SendGrid failed to send email to {Email}. Status: {StatusCode}", email, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending email via SendGrid to UserId {UserId}", notification.UserId);
            return false;
        }
    }
}
