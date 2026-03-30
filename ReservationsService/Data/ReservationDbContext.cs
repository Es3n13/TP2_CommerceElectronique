using Microsoft.EntityFrameworkCore;
using ReservationsService.Models;

namespace ReservationsService.Data
{
	public class ReservationDbContext : DbContext
	{
		public ReservationDbContext(DbContextOptions<ReservationDbContext> options)
			: base(options)
		{
		}

		public DbSet<Reservation> Reservations { get; set; } = null!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Reservation>(entity =>
			{
				entity.HasIndex(e => new { e.UserId, e.ResourceId, e.ReservationDate })
					.IsUnique();

				entity.Property(e => e.Status).HasDefaultValue("Pending");
			});
		}
	}
}