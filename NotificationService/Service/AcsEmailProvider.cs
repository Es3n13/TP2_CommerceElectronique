using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Interface;
using NotificationService.Models;
using NotificationService.Service;

namespace NotificationService.Service
{
    public class AcsEmailProvider : INotificationProvider
    {
        private readonly EmailClient _emailClient;
        private readonly AcsEmailOptions _options;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AcsEmailProvider> _logger;

        public NotificationChannel Channel => NotificationChannel.Email;

        public AcsEmailProvider(
            EmailClient emailClient, 
            IOptions<AcsEmailOptions> options, 
            IUserRepository userRepository, 
            ILogger<AcsEmailProvider> logger)
        {
            _emailClient = emailClient;
            _options = options.Value;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<bool> SendAsync(Notification notification)
        {
            try
            {
                var userEmail = await _userRepository.GetEmailByIdAsync(notification.UserId);
                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning("Could not find email for user {UserId}", notification.UserId);
                    return false;
                }

                var emailMessage = new EmailMessage(
                    senderAddress: _options.SenderAddress,
                    content: new EmailContent("Notification", notification.Content),
                    recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(userEmail) })
                );

                var emailSendOperation = await _emailClient.SendAsync(WaitUntil.Completed, emailMessage);
                
                if (emailSendOperation.HasCompleted)
                {
                    _logger.LogInformation("Email sent successfully to {Email}", userEmail);
                    return true;
                }

                _logger.LogError("Email failed to send to {Email}", userEmail);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending email to user {UserId}", notification.UserId);
                return false;
            }
        }
    }
}
