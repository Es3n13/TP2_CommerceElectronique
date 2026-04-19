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
                entity.HasIndex(e => e.Email).IsUnique();
				entity.Property(e => e.Role).HasDefaultValue("User");
			});
		}
	}
}