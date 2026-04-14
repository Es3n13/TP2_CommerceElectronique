using Microsoft.AspNetCore.Mvc;
using NotificationService.Models;
using NotificationService.Services;
using System;
namespace NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly NotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(NotificationDispatcher dispatcher, ILogger<NotificationController> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }
    [HttpPost]
    public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
    {
        _logger.LogInformation("Received notification request for User: {UserId}", request.UserId);

        // 1. Map Request to Entity
        var notification = new NotificationService.Models.Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Content = request.Content,
            Channel = request.Channel,
            Status = NotificationStatus.Pending
        };
        // 2. Dispatch (This calls your existing NotificationDispatcher)
        await _dispatcher.DispatchAsync(notification);

        // 3. Return the result
        return Ok(new
        {
            NotificationId = notification.Id,
            Status = notification.Status.ToString(),
            Message = "Notification processed."
        });
    }
}
