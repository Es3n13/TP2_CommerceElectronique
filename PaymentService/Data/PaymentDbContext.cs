using Microsoft.EntityFrameworkCore;
using PaymentService.Models;

namespace PaymentService.Data
{
    public class PaymentDbContext : DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
        {
        }

        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Payment>(entity =>
            {
                // Prévient la duplication des payment intents pour la même réservation
                entity.HasIndex(e => new { e.ReservationId, e.StripePaymentIntentId })
                    .IsUnique();

                // Index pour query les paiement par réservation
                entity.HasIndex(e => e.ReservationId);

                // Index unique pour les Stripe payment intent ID
                entity.HasIndex(e => e.StripePaymentIntentId).IsUnique();

                //  Index pour filter par statut
                entity.HasIndex(e => e.Status);

                // Valeurs par défaut
                entity.Property(e => e.Status).HasDefaultValue("Pending");
                entity.Property(e => e.Currency).HasDefaultValue("cad");
            });
        }
    }
}