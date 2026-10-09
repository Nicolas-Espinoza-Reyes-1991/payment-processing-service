using Microsoft.EntityFrameworkCore;
using PaymentProcessingService.Application.Abstractions;
using PaymentProcessingService.Application.UseCases;
using PaymentProcessingService.Infrastructure.Persistence;
using PaymentProcessingService.Infrastructure.Acquiring;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "API Key requerida para los endpoints de /payments"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

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
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PaymentProcessingDb"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

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

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isExempt = path.StartsWithSegments("/swagger") || path.StartsWithSegments("/health");

    if (!isExempt)
    {
        var expectedApiKey = builder.Configuration["Security:ApiKey"];
        var providedApiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(expectedApiKey) || providedApiKey != expectedApiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "API Key inválida o ausente. Incluí el header X-Api-Key." });
            return;
        }
    }

    await next();
});

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();