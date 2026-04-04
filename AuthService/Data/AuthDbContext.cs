using Microsoft.EntityFrameworkCore;

namespace AuthService.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RefreshToken>()
                .ToTable("RefreshTokens", schema: "dbo")
                .HasKey(rt => rt.TokenId);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.Token)
                .HasDatabaseName("IX_RefreshTokens_Token");

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.JwtId)
                .HasDatabaseName("IX_RefreshTokens_JwtId");
        }
    }

    public class RefreshToken
    {
        public Guid TokenId { get; set; }
        public string? UserId { get; set; }
        public string Token { get; set; }
        public string JwtId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedByToken { get; set; }
    }
}