using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
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
    private readonly NotificationDbContext _context;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(NotificationDispatcher dispatcher, NotificationDbContext context, ILogger<NotificationController> logger)
    {
        _dispatcher = dispatcher;
        _context = context;
        _logger = logger;
    }

    // POST: api/notification/send
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
    {
        if (request == null) return BadRequest("Request body cannot be null.");

        _logger.LogInformation("Requête de notification reçue de l'utilisateur: {UserId}", request.UserId);

        var notification = new NotificationService.Models.Notification
        {
            UserId = request.UserId,
            Content = request.Content,
            Channel = request.Channel,
            Status = NotificationStatus.Pending
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        await _dispatcher.DispatchAsync(notification);
        return Ok(new
        {
            NotificationId = notification.Id,
            Status = notification.Status.ToString(),
            UserId = notification.UserId,
            Content = notification.Content,
            Channel = notification.Channel,
            Message = "Notification envoyée pour livraison."
        });
    }

    // GET: api/notification/status/{id}
    [HttpGet("status/{id}")]
    public async Task<IActionResult> GetStatus(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return NotFound();

        return Ok(new
        {
            NotificationId = notification.Id,
            Status = notification.Status.ToString(),
            UserId = notification.UserId,
            Content = notification.Content,
            Channel = notification.Channel,
            Timestamp = notification.SentAt ?? notification.CreatedAt
        });
    }
}
