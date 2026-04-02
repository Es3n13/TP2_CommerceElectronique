using Stripe;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PaymentDbConnection")
    )
);

// Register HttpClient for ReservationsService communication
builder.Services.AddHttpClient("ReservationsService", client =>
{
    client.BaseAddress = new Uri("http://localhost:5002");
});

builder.Services.AddScoped<Services.StripeService>();

// Add Stripe configuration
var stripeSettings = builder.Configuration.GetSection("Stripe");
StripeConfiguration.ApiKey = stripeSettings["SecretKey"];

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Service API",
        Version = "v1",
        Description = "Payment processing service with Stripe integration"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service API v1");
    });

    // Ensure database is created in development
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        context.Database.EnsureCreated();
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();