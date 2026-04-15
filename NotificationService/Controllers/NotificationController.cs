using Microsoft.AspNetCore.Mvc;
using NotificationService.Models;
using NotificationService.Services;
using System;
using System.Threading.Tasks;

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

    // POST: api/notification/send
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
    {
        if (request == null) return BadRequest("Request body cannot be null.");

        _logger.LogInformation("Received notification request for User: {UserId}", request.UserId);

        var notification = new NotificationService.Models.Notification
        {
            UserId = request.UserId,
            Content = request.Content,
            Channel = request.Channel,
            Status = NotificationStatus.Pending
        };

        await _dispatcher.DispatchAsync(notification);
        return Ok(new
        {
            NotificationId = notification.Id,
            Status = notification.Status.ToString(),
            Message = "Notification sent for delivery."
        });
    }

    // GET: api/notification/status/{id}
    [HttpGet("status/{id}")]
    public IActionResult GetStatus(int id)
    {

        return Ok(new
        {
            NotificationId = id,
            Status = "Processed (Mock)",
            Timestamp = DateTime.UtcNow
        });
    }
}
