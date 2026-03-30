using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationsService.Data;
using ReservationsService.Models;

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

		public ReservationsController(ReservationDbContext context)
		{
			_context = context;
		}

		[HttpGet]
		public async Task<IActionResult> GetAll()
		{
			var reservations = await _context.Reservations
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync();

			return Ok(reservations);
		}

		[HttpGet("{id}")]
		public async Task<IActionResult> GetById(int id)
		{
			var reservation = await _context.Reservations.FindAsync(id);

			if (reservation == null)
			{
				return NotFound(new { Message = $"Reservation with ID {id} not found." });
			}

			return Ok(reservation);
		}

		[HttpGet("user/{userId}")]
		public async Task<IActionResult> GetByUserId(int userId)
		{
			var reservations = await _context.Reservations
				.Where(r => r.UserId == userId)
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync();

			return Ok(reservations);
		}

		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CreateReservationRequest request)
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

		[HttpPut("{id}")]
		public async Task<IActionResult> Update(int id, [FromBody] UpdateReservationRequest request)
		{
			var reservation = await _context.Reservations.FindAsync(id);

			if (reservation == null)
			{
				return NotFound(new { Message = $"Reservation with ID {id} not found." });
			}

			if (request.ReservationDate != default)
				reservation.ReservationDate = request.ReservationDate;
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

			return Ok(reservation);
		}

		[HttpDelete("{id}")]
		public async Task<IActionResult> Delete(int id)
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
	}
}