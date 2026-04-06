using Microsoft.EntityFrameworkCore;
using ReservationsService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReservationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ReservationDbConnection")
    )
);

builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/api/users/");
});

builder.Services.AddHttpClient("ResourcesService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/api/resources/");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ReservationsService API",
        Version = "v1"
    });
});

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