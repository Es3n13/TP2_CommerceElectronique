using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data
{

	public class UserDbContext : DbContext
	{
		public UserDbContext(DbContextOptions<UserDbContext> options)
			: base(options)
		{
		}

		public DbSet<User> Users { get; set; } = null!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<User>(entity =>
			{
                // Le email devrait être unique
                entity.HasIndex(e => e.Email).IsUnique();

				// Valeur pas défaut
				entity.Property(e => e.Role).HasDefaultValue("User");
			});
		}
	}
}