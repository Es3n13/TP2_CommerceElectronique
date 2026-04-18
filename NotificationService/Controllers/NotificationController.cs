using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using System;
using System.Threading.Tasks;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/notifications")]
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

        try
        {
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur dans la base de données pendant la sauvegarde de la notification de l'utilisateur {UserId}", request.UserId);
            return StatusCode(500, new { Message = "Erreur inter de la base de données." });
        }

        try
        {
            await _dispatcher.DispatchAsync(notification);
            notification.Status = NotificationStatus.Sent;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatch échoué pour la notification {Id}. inscrit comme Failed.", notification.Id);
            notification.Status = NotificationStatus.Failed;
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            NotificationId = notification.Id,
            Status = notification.Status.ToString(),
            UserId = notification.UserId,
            Content = notification.Content,
            Channel = notification.Channel,
            Message = notification.Status == NotificationStatus.Sent
        ? "Notification envoyée avec succès."
        : "Notification enregistrée, mais une erreur est survenue lors de la livraison."
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
