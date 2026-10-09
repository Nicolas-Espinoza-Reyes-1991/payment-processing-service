using Microsoft.EntityFrameworkCore;
using PaymentProcessingService.Application.Abstractions;
using PaymentProcessingService.Application.UseCases;
using PaymentProcessingService.Infrastructure.Persistence;
using PaymentProcessingService.Infrastructure.Acquiring;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<PaymentProcessingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentProcessingDb")));

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IAcquirerClient, AcquirerMockClient>();
builder.Services.AddScoped<CreatePaymentUseCase>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(feature?.Error, "Error no controlado procesando {Path}", context.Request.Path);

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Ocurrió un error interno inesperado. Contacte a soporte si el problema persiste."
        });
    });
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();