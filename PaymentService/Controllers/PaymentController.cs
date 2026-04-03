using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;
using PaymentService.Models;
using Stripe;

namespace PaymentService.Controllers
{
	[ApiController]
	[Route("api/payments")]
	public class PaymentController : ControllerBase
	{
		private readonly StripeService _stripeService;
		private readonly PaymentDbContext _context;

		public PaymentController(StripeService stripeService, PaymentDbContext context)
		{
			_stripeService = stripeService;
			_context = context;
		}

		// POST /api/payments/create - Create a payment intent
		[HttpPost("create")]
		public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
		{
			if (request.Amount <= 0)
			{
				return BadRequest(new { Message = "Amount must be greater than 0." });
			}

			if (request.ReservationId <= 0)
			{
				return BadRequest(new { Message = "Valid reservation ID is required." });
			}

			try
			{
				var paymentIntent = await _stripeService.CreatePaymentIntentAsync(
					request.Amount,
					request.Description ?? $"Payment for reservation {request.ReservationId}",
					request.ReservationId,
					request.PaymentMethodId
				);

				// If payment was confirmed and succeeded, update reservation status
				if (!string.IsNullOrEmpty(request.PaymentMethodId) && paymentIntent.Status == "succeeded")
				{
					await _stripeService.UpdateReservationStatusAsync(request.ReservationId, "Confirmed");
				}

				return Ok(new
				{
					PaymentIntentId = paymentIntent.Id,
					ClientSecret = paymentIntent.ClientSecret,
					Amount = paymentIntent.Amount / 100m,
					Currency = paymentIntent.Currency,
					Status = paymentIntent.Status,
					Created = paymentIntent.Created
				});
			}
			catch (StripeException ex)
			{
				return StatusCode(500, new
				{
					Message = "Failed to create payment intent.",
					Error = ex.Message
				});
			}
		}

		// GET /api/payments/{id} - Get payment details
		[HttpGet("{id}")]
		public async Task<IActionResult> GetPayment(int id)
		{
			var payment = await _context.Payments.FindAsync(id);

			if (payment == null)
			{
				return NotFound(new { Message = "Payment not found." });
			}

			try
			{
				var paymentIntent = await _stripeService.GetPaymentIntentAsync(payment.StripePaymentIntentId);

				return Ok(new
				{
					Id = payment.Id,
					ReservationId = payment.ReservationId,
					Amount = payment.Amount,
					StripePaymentIntentId = payment.StripePaymentIntentId,
					Status = payment.Status,
					Currency = payment.Currency,
					CreatedAt = payment.CreatedAt,
					CompletedAt = payment.CompletedAt,
					StripeStatus = paymentIntent.Status
				});
			}
			catch (StripeException ex)
			{
				return StatusCode(500, new
				{
					Message = "Failed to fetch payment from Stripe.",
					Error = ex.Message
				});
			}
		}

		// GET /api/payments/reservation/{reservationId} - Get payments by reservation
		[HttpGet("reservation/{reservationId}")]
		public async Task<IActionResult> GetPaymentsByReservation(int reservationId)
		{
			var payments = await _context.Payments
				.Where(p => p.ReservationId == reservationId)
				.OrderByDescending(p => p.CreatedAt)
				.ToListAsync();

			return Ok(payments);
		}

		// POST /api/payments/{id}/confirm - Confirm payment
		[HttpPost("{id}/confirm")]
		public async Task<IActionResult> ConfirmPayment(int id)
		{
			var payment = await _context.Payments.FindAsync(id);

			if (payment == null)
			{
				return NotFound(new { Message = "Payment not found." });
			}

			try
			{
				var paymentIntent = await _stripeService.ConfirmPaymentIntentAsync(payment.StripePaymentIntentId);

				// Update payment status based on Stripe response
				await _stripeService.UpdatePaymentStatusAsync(
					payment.Id,
					paymentIntent.Status,
					paymentIntent.Status == "requires_payment_method" ? null : paymentIntent.LastPaymentError?.Message
				);

				// Update reservation status if payment succeeded
				if (paymentIntent.Status == "succeeded")
				{
					await _stripeService.UpdateReservationStatusAsync(payment.ReservationId, "Confirmed");
				}

				return Ok(new
				{
					Id = payment.Id,
					ReservationId = payment.ReservationId,
					Amount = payment.Amount,
					Status = paymentIntent.Status,
					StripePaymentIntentId = payment.StripePaymentIntentId
				});
			}
			catch (StripeException ex)
			{
				await _stripeService.UpdatePaymentStatusAsync(payment.Id, "Failed", ex.Message);
				return StatusCode(500, new
				{
					Message = "Failed to confirm payment.",
					Error = ex.Message
				});
			}
		}

		// POST /api/payments/{id}/refund - Refund payment
		[HttpPost("{id}/refund")]
		public async Task<IActionResult> RefundPayment(int id, [FromBody] RefundRequest? request = null)
		{
			var payment = await _context.Payments.FindAsync(id);

			if (payment == null)
			{
				return NotFound(new { Message = "Payment not found." });
			}

			if (payment.Status != "Succeeded")
			{
				return BadRequest(new { Message = "Only succeeded payments can be refunded." });
			}

			try
			{
				var refund = await _stripeService.RefundPaymentAsync(
					payment.StripePaymentIntentId,
					request?.Amount
				);

				await _stripeService.UpdatePaymentStatusAsync(payment.Id, "Refunded");

				return Ok(new
				{
					Id = payment.Id,
					RefundId = refund.Id,
					Amount = refund.Amount / 100m,
					Status = refund.Status
				});
			}
			catch (StripeException ex)
			{
				return StatusCode(500, new
				{
					Message = "Failed to refund payment.",
					Error = ex.Message
				});
			}
		}
	}

	public class CreatePaymentRequest
	{
		public decimal Amount { get; set; }
		public int ReservationId { get; set; }
		public string? Description { get; set; }
		public string? PaymentMethodId { get; set; }
	}

	public class RefundRequest
	{
		public decimal? Amount { get; set; }
	}
}