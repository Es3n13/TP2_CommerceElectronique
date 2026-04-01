using Microsoft.EntityFrameworkCore;
using ReservationsService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReservationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ReservationDbConnection")
    )
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
	using (var scope = app.Services.CreateScope())
	{
		var context = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
		context.Database.EnsureCreated();
	}
}

app.MapControllers();

app.Run();