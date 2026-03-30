using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReservationsService.Models
{
	[Table("Reservations")]
	public class Reservation
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[Required]
		public int UserId { get; set; }

		[Required]
		public int ResourceId { get; set; }

		[Required]
		public DateTime ReservationDate { get; set; }

		public DateTime? StartTime { get; set; }

		public DateTime? EndTime { get; set; }

		public string? Status { get; set; } = "Pending";

		public decimal? TotalPrice { get; set; }

		public string? Notes { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}