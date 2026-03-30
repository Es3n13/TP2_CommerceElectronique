using Microsoft.EntityFrameworkCore;
using userservice.Data;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURATION
// ============================================================

// Add EF Core with SQL Server
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("UserDbConnection")
    )
);

// Add API Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // JWT Support (we'll add in Phase 1 Step 2)
    // c.AddSecurityDefinition("Bearer", ...);
    // c.AddSecurityRequirement(...);
});

var app = builder.Build();

// ============================================================
// MIDDLEWARE
// ============================================================

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Optional: Auto-migrate in development (NOT recommended for production)
if (app.Environment.IsDevelopment())
{
	using (var scope = app.Services.CreateScope())
	{
		var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();
		// Create database if it doesn't exist
		context.Database.EnsureCreated();
	}
}

app.MapControllers();

app.Run();