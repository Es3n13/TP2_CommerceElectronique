using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentService.Models
{
	[Table("Payments")]
	public class Payment
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[Required]
		public int ReservationId { get; set; }

		[Required]
		public decimal Amount { get; set; }

		[Required]
		public string StripePaymentIntentId { get; set; } = string.Empty;

		[Required]
		public string Status { get; set; } = "Pending";

		public string? StripeErrorMessage { get; set; }

		[Required]
		public string Currency { get; set; } = "cad";

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? CompletedAt { get; set; }
	}
}