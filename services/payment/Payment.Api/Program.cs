using Fintech.Observability.Logging;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Endpoints;
using Payment.Application;
using Payment.Infrastructure;
using Payment.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFintechJsonLogging("payment", builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Apply EF migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.Migrate();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "payment" }));
app.MapPaymentEndpoints();

app.Run();
