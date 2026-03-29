using Microsoft.AspNetCore.Mvc;
using ReservationsService.Models;

namespace ReservationsService.Controllers
{
    // DTO reçu dans le body du POST
    public class CreateReservationRequest
    {
        public int UserId { get; set; }
        public int ResourceId { get; set; }
        public DateTime Date { get; set; }
    }

    [ApiController]
    [Route("api/reservations")]
    public class ReservationsController : ControllerBase
    {
        // Liste en mémoire pour stocker les réservations
        private static readonly List<Reservation> _reservations = new();
        private static int _nextId = 1;

        // GET /api/reservations
        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_reservations);
        }

        // POST /api/reservations
        [HttpPost]
        public IActionResult Create([FromBody] CreateReservationRequest request)
        {
            var reservation = new Reservation
            {
                Id = _nextId++,
                UserId = request.UserId,
                ResourceId = request.ResourceId,
                Date = request.Date
            };

            _reservations.Add(reservation);

            return Created(string.Empty, reservation);
        }
    }
}
