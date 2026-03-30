using Microsoft.EntityFrameworkCore;
using ResourcesService.Models;

namespace ResourcesService.Data
{
	// Database context pour ResourcesService
	public class ResourceDbContext : DbContext
	{
		public ResourceDbContext(DbContextOptions<ResourceDbContext> options)
			: base(options)
		{
		}

		public DbSet<Resource> Resources { get; set; } = null!;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<Resource>(entity =>
			{
                entity.HasIndex(e => new { e.Name, e.Location }).IsUnique();
                entity.Property(e => e.IsAvailable).HasDefaultValue(true);
			});
		}
	}
}