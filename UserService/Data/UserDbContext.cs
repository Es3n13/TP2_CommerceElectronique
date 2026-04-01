using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data
{
	/// <summary>
	/// Database context for UserService
	/// </summary>
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

			// Configure User entity
			modelBuilder.Entity<User>(entity =>
			{
				// Email should be unique
				entity.HasIndex(e => e.Email).IsUnique();

				// Default values
				entity.Property(e => e.Role).HasDefaultValue("User");
			});
		}
	}
}