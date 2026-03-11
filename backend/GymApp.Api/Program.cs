using System.Text;
using System.Text.Json.Serialization;
using GymApp.Api.Endpoints;
using GymApp.Api.Middleware;
using GymApp.Domain.Interfaces;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JSON: serialize/deserialize enums as strings globally
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// Tenant context (scoped — one per request)
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// Email — provider selected by Email:Provider config ("SendGrid" | "Resend", default: SendGrid)
var emailProvider = builder.Configuration["Email:Provider"] ?? "SendGrid";
if (emailProvider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<Resend.ResendClientOptions>(options =>
        options.ApiToken = builder.Configuration["Resend:ApiKey"] ?? throw new InvalidOperationException("Resend:ApiKey not configured."));
    builder.Services.AddHttpClient<Resend.IResend, Resend.ResendClient>();
    builder.Services.AddScoped<IEmailService, ResendEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, SendGridEmailService>();
}

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required");
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("AdminOrAbove", policy => policy.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("AnyUser", policy => policy.RequireAuthenticatedUser());
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

        // In dev, allow all origins
        if (builder.Environment.IsDevelopment())
            policy.SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Apply migrations and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}

// Map all endpoint groups
app.MapTenantEndpoints();
app.MapAuthEndpoints();
app.MapStudentEndpoints();
app.MapClassTypeEndpoints();
app.MapScheduleEndpoints();
app.MapSessionEndpoints();
app.MapBookingEndpoints();
app.MapPackageEndpoints();
app.MapPackageTemplateEndpoints();
app.MapInstructorEndpoints();
app.MapDashboardEndpoints();

app.MapGet("/health", () => Results.Ok(new { Status = "healthy", Time = DateTime.UtcNow }));

app.Run();
