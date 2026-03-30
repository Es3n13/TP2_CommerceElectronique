using Microsoft.EntityFrameworkCore;
using ResourcesService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ResourceDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ResourceDbConnection")
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
		var context = scope.ServiceProvider.GetRequiredService<ResourceDbContext>();
		context.Database.EnsureCreated();
	}
}

app.MapControllers();

app.Run();