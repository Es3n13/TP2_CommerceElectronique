using Microsoft.EntityFrameworkCore;
using userservice.Data;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURATION

// Ajouter EF Core et SQL Server
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("UserDbConnection")
    )
);

// Ajouter API Controllers et Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // JWT Support (we'll add in Phase 1 Step 2)
    // c.AddSecurityDefinition("Bearer", ...);
    // c.AddSecurityRequirement(...);
});

var app = builder.Build();

// MIDDLEWARE

// Configure le HTTP request pipeline
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

//Migration automatique
if (app.Environment.IsDevelopment())
{
	using (var scope = app.Services.CreateScope())
	{
		var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        // Créer la base de données si elle n'existe pas
        context.Database.EnsureCreated();
	}
}

app.MapControllers();

app.Run();