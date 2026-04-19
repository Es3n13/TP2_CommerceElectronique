using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationsService.Data;
using ReservationsService.Models;
using ReservationsService.Services;
using System.Net.Http;

namespace ReservationsService.Controllers
{
    public class CreateReservationRequest
    {
        public int UserId { get; set; }
        public int ResourceId { get; set; }
        public DateTime ReservationDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateReservationRequest
    {
        public DateTime? ReservationDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Status { get; set; }
        public decimal? TotalPrice { get; set; }
        public string? Notes { get; set; }
    }

    [ApiController]
    [Route("api/reservations")]
    public class ReservationsController : ControllerBase
    {
        private readonly ReservationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly INotificationClient _notificationClient;

        // Injecte le contexte, la factory HTTP et le client de notification
        public ReservationsController(
            ReservationDbContext context,
            IHttpClientFactory httpClientFactory,
            INotificationClient notificationClient)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _notificationClient = notificationClient;
        }

        // Vérifie l'existence de l'utilisateur
        private async Task<bool> ValidateUserAsync(int userId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("UserService");
                var response = await client.GetAsync($"{userId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Vérifie la disponibilité de la ressource
        private async Task<bool> IsResourceAvailableAsync(int resourceId, DateTime reservationDate)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ResourcesService");
                var response = await client.GetAsync($"{resourceId}");

                if (!response.IsSuccessStatusCode)
                    return false;

                // Vérifie l'existence d'une réservation à la même date
                var existing = await _context.Reservations
                    .AnyAsync(r => r.ResourceId == resourceId &&
                                r.ReservationDate.Date == reservationDate.Date &&
                                r.Status != "Canceled");

                return !existing;
            }
            catch
            {
                return false;
            }
        }

        // Retourne toutes les réservations triées par date de création
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var reservations = await _context.Reservations
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                return Ok(reservations);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Une erreur s'est produite durant la récupération de la réservations." });
            }
        }

        // Retourne une réservation par son identifiant
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var reservation = await _context.Reservations.FindAsync(id);

                if (reservation == null)
                {
                    return NotFound(new { Message = $"Reservation ID {id} non trouvée" });
                }

                return Ok(reservation);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Une erreur s'est produite durant la récupération de la réservations." });
            }
        }

        // Retourne les réservations d'un utilisateur
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUserId(int userId)
        {
            try
            {
                var reservations = await _context.Reservations
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                return Ok(reservations);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Une erreur s'est produite durant la récupération de la réservations." });
            }
        }

        // Crée une nouvelle réservation
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReservationRequest request)
        {
            try
            {
                // Valide l'identifiant utilisateur
                if (request.UserId <= 0)
                {
                    return BadRequest(new { Message = "Un UserId valide est requis." });
                }

                // Valide l'identifiant ressource
                if (request.ResourceId <= 0)
                {
                    return BadRequest(new { Message = "Un Id ressource valide est requis." });
                }

                // Valide la date de réservation
                if (request.ReservationDate == default)
                {
                    return BadRequest(new { Message = "Uen date de réservation valide est requise." });
                }

                // Vérifie que l'utilisateur existe
                var userExists = await ValidateUserAsync(request.UserId);
                if (!userExists)
                {
                    return BadRequest(new { Message = $"Utilisateur ID {request.UserId} n'existe pas." });
                }

                // Vérifie que la ressource est disponible
                var isAvailable = await IsResourceAvailableAsync(request.ResourceId, request.ReservationDate);
                if (!isAvailable)
                {
                    return BadRequest(new { Message = $"Ressource ID {request.ResourceId} n'est pas disponible le{request.ReservationDate:yyyy-MM-dd}." });
                }

                // Empêche la création d'un doublon
                var existingReservation = await _context.Reservations
                    .FirstOrDefaultAsync(r =>
                        r.UserId == request.UserId &&
                        r.ResourceId == request.ResourceId &&
                        r.ReservationDate == request.ReservationDate
                    );

                if (existingReservation != null)
                {
                    return Conflict(new { Message = $"Utilisateur {request.UserId} à déjà réservé {request.ResourceId} le {request.ReservationDate:yyyy-MM-dd}." });
                }

                // Construit l'entité à stocker
                var reservation = new Reservation
                {
                    UserId = request.UserId,
                    ResourceId = request.ResourceId,
                    ReservationDate = request.ReservationDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Notes = request.Notes,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = reservation.Id },
                    reservation
                );
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Une erreur s'est produite durant la création de la réservations." });
            }
        }

        // Met à jour une réservation
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateReservationRequest request)
        {
            try
            {
                var reservation = await _context.Reservations.FindAsync(id);

                if (reservation == null)
                {
                    return NotFound(new { Message = $"ReservationID {id} non trouvée." });
                }

                // Conserve l'ancien statut pour détecter une transition
                string oldStatus = reservation.Status;

                if (request.ReservationDate.HasValue)
                    reservation.ReservationDate = request.ReservationDate.Value;
                if (request.StartTime.HasValue)
                    reservation.StartTime = request.StartTime;
                if (request.EndTime.HasValue)
                    reservation.EndTime = request.EndTime;
                if (!string.IsNullOrEmpty(request.Status))
                    reservation.Status = request.Status;
                if (request.TotalPrice.HasValue)
                    reservation.TotalPrice = request.TotalPrice;
                if (request.Notes != null)
                    reservation.Notes = request.Notes;

                await _context.SaveChangesAsync();

                // Déclenche une notification lors du passage à l'état Confirmed
                if (oldStatus != "Confirmed" && reservation.Status == "Confirmed")
                {
                    await _notificationClient.SendNotificationAsync(
                        reservation.UserId,
                        "Votre réservation est confirmée.Merci pour votre paiement!"
                    );
                }

                return Ok(reservation);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Une erreur s'est produite durant la modification de la réservations." });
            }
        }

        // Supprime une réservation existante
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var reservation = await _context.Reservations.FindAsync(id);

                if (reservation == null)
                {
                    return NotFound(new { Message = $"ReservationID {id} non trouvée." });
                }

                _context.Reservations.Remove(reservation);
                await _context.SaveChangesAsync();

                return Ok(new { Message = $"ReservationID {id} effacée avec succès." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "Une erreur s'est produite durant la supression de la réservations.." });
            }
        }
    }
}