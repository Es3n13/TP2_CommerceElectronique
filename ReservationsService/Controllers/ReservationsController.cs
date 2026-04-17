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

        public ReservationsController(ReservationDbContext context, IHttpClientFactory httpClientFactory, INotificationClient notificationClient)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _notificationClient = notificationClient;
        }

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

		private async Task<bool> IsResourceAvailableAsync(int resourceId, DateTime reservationDate)
		{
			try
			{
				var client = _httpClientFactory.CreateClient("ResourcesService");
				var response = await client.GetAsync($"{resourceId}");
				
				if (!response.IsSuccessStatusCode)
					return false;

				// Check existing reservations for this resource & date
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
				return StatusCode(500, new { Message = "An error occurred while retrieving reservations." });
			}
		}

		[HttpGet("{id}")]
		public async Task<IActionResult> GetById(int id)
		{
			try
			{
				var reservation = await _context.Reservations.FindAsync(id);

				if (reservation == null)
				{
					return NotFound(new { Message = $"Reservation with ID {id} not found." });
				}

				return Ok(reservation);
			}
			catch (Exception)
			{
				return StatusCode(500, new { Message = "An error occurred while retrieving the reservation." });
			}
		}

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
				return StatusCode(500, new { Message = "An error occurred while retrieving user reservations." });
			}
		}

		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CreateReservationRequest request)
		{
			try
			{
				if (request.UserId <= 0)
				{
					return BadRequest(new { Message = "Valid UserId is required." });
				}

				if (request.ResourceId <= 0)
				{
					return BadRequest(new { Message = "Valid ResourceId is required." });
				}

				if (request.ReservationDate == default)
				{
					return BadRequest(new { Message = "ReservationDate is required." });
				}

				var userExists = await ValidateUserAsync(request.UserId);
				if (!userExists)
				{
					return BadRequest(new { Message = $"User with ID {request.UserId} does not exist." });
				}

				var isAvailable = await IsResourceAvailableAsync(request.ResourceId, request.ReservationDate);
				if (!isAvailable)
				{
					return BadRequest(new { Message = $"Resource with ID {request.ResourceId} is not available on {request.ReservationDate:yyyy-MM-dd}." });
				}

				var existingReservation = await _context.Reservations
					.FirstOrDefaultAsync(r =>
						r.UserId == request.UserId &&
						r.ResourceId == request.ResourceId &&
						r.ReservationDate == request.ReservationDate
					);

				if (existingReservation != null)
				{
					return Conflict(new { Message = $"User {request.UserId} already reserved resource {request.ResourceId} on {request.ReservationDate:yyyy-MM-dd}." });
				}

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
				return StatusCode(500, new { Message = "An error occurred while creating the reservation." });
			}
		}

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateReservationRequest request)
        {
            try
            {
                var reservation = await _context.Reservations.FindAsync(id);

                if (reservation == null)
                {
                    return NotFound(new { Message = $"Reservation with ID {id} not found." });
                }

                // 1. Capture the status before we change it // <--- NEW
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

                // 2. Check if the status just transitioned to 'Confirmed' // <--- NEW
                if (oldStatus != "Confirmed" && reservation.Status == "Confirmed")
                {
                    await _notificationClient.SendNotificationAsync(
                    reservation.UserId,
                    "Your reservation has been confirmed! Thank you for your payment."
                    );
                }

                return Ok(reservation);
            }
            catch (Exception)
            {
                return StatusCode(500, new { Message = "An error occurred while updating the reservation." });
            }
        }


        [HttpDelete("{id}")]
		public async Task<IActionResult> Delete(int id)
		{
			try
			{
				var reservation = await _context.Reservations.FindAsync(id);

				if (reservation == null)
				{
					return NotFound(new { Message = $"Reservation with ID {id} not found." });
				}

				_context.Reservations.Remove(reservation);
				await _context.SaveChangesAsync();

				return Ok(new { Message = $"Reservation with ID {id} deleted successfully." });
			}
			catch (Exception)
			{
				return StatusCode(500, new { Message = "An error occurred while deleting the reservation." });
			}
		}
	}
}