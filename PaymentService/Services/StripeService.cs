using Stripe;
using PaymentService.Models;
using PaymentService.Data;

namespace PaymentService.Services
{
	public class StripeService
	{
		private readonly IConfiguration _config;
		private readonly PaymentDbContext _context;
		private readonly HttpClient _httpClient;

		public StripeService(IConfiguration config, PaymentDbContext context, IHttpClientFactory httpClientFactory)
		{
			_config = config;
			_context = context;
			_httpClient = httpClientFactory.CreateClient("ReservationsService");

			StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
		}

		public async Task<PaymentIntent> CreatePaymentIntentAsync(decimal amount, string description, int reservationId)
		{
			var options = new PaymentIntentCreateOptions
			{
				Amount = (long)(amount * 100),
				Currency = "cad",
				Description = description,
				Metadata = new Dictionary<string, string>
				{
					{ "reservation_id", reservationId.ToString() }
				}
			};

			var service = new PaymentIntentService();
			var paymentIntent = await service.CreateAsync(options);

            // Sauvegarder le paiement dans la base de données
            var payment = new Payment
			{
				ReservationId = reservationId,
				Amount = amount,
				StripePaymentIntentId = paymentIntent.Id,
				Status = "Pending",
				Currency = "cad",
				CreatedAt = DateTime.UtcNow,
				CompletedAt = null
			};

			_context.Payments.Add(payment);
			await _context.SaveChangesAsync();

			return paymentIntent;
		}

		public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId)
		{
			var service = new PaymentIntentService();
			return await service.GetAsync(paymentIntentId);
		}

		public async Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId)
		{
			var options = new PaymentIntentConfirmOptions
			{
				ReturnUrl = _config["App:BaseUrl"] ?? "http://localhost:5003"
			};

			var service = new PaymentIntentService();
			return await service.ConfirmAsync(paymentIntentId, options);
		}

		public async Task<Refund> RefundPaymentAsync(string paymentIntentId, decimal? amount = null)
		{
			var refundOptions = new RefundCreateOptions
			{
				PaymentIntent = paymentIntentId
			};

			if (amount.HasValue)
			{
				refundOptions.Amount = (long)(amount.Value * 100);
			}

			var service = new RefundService();
			return await service.CreateAsync(refundOptions);
		}

		public async Task<string> GetPaymentStatusAsync(string paymentIntentId)
		{
			var paymentIntent = await GetPaymentIntentAsync(paymentIntentId);
			return paymentIntent.Status;
		}

		public async Task UpdatePaymentStatusAsync(int paymentId, string status, string? errorMessage = null)
		{
			var payment = await _context.Payments.FindAsync(paymentId);
			if (payment != null)
			{
				payment.Status = status;
				payment.StripeErrorMessage = errorMessage;
				if (status == "Succeeded")
				{
					payment.CompletedAt = DateTime.UtcNow;
				}
				await _context.SaveChangesAsync();
			}
		}

		public async Task UpdateReservationStatusAsync(int reservationId, string status)
		{
			var updateUrl = $"/api/reservations/{reservationId}";
			var body = new { Status = status };

			var response = await _httpClient.PutAsJsonAsync(updateUrl, body);
			response.EnsureSuccessStatusCode();
		}
	}
}