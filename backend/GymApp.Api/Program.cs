using System.Text;
using System.Text.Json.Serialization;
using GymApp.Api.Endpoints;
using GymApp.Api.Middleware;
using GymApp.Api.Services;
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
    options.AddPolicy("Affiliate", policy => policy.RequireRole("Affiliate"));
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

// Efí Bank payment gateway
builder.Services.AddSingleton(new EfiOptions
{
    ClientId = builder.Configuration["Efi:ClientId"] ?? string.Empty,
    ClientSecret = builder.Configuration["Efi:ClientSecret"] ?? string.Empty,
    CertificateBase64 = builder.Configuration["Efi:CertificateBase64"] ?? string.Empty,
    CertificatePassword = builder.Configuration["Efi:CertificatePassword"] ?? string.Empty,
    PixKey = builder.Configuration["Efi:PixKey"] ?? string.Empty,
    PlatformPayeeCode = builder.Configuration["Efi:PlatformPayeeCode"],
    PlatformFeePercent = decimal.TryParse(builder.Configuration["Efi:PlatformFeePercent"], out var fee) ? fee : 0,
    Sandbox = !builder.Environment.IsProduction()
});
builder.Services.AddSingleton<EfiService>();

// AbacatePay subscription billing
builder.Services.AddSingleton<AbacatePayService>();
builder.Services.AddHostedService<SubscriptionReminderService>();

// AI Assistant (MiMo-V2-Flash / MiMo-V2-Omni)
builder.Services.AddHttpClient("MiMo", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"] ?? "https://api.xiaomimimo.com");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", builder.Configuration["AI:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AiAssistantService>();
builder.Services.AddScoped<DemoSeedService>();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SubscriptionMiddleware>();

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
app.MapServiceCategoryEndpoints();
app.MapScheduleEndpoints();
app.MapSessionEndpoints();
app.MapBookingEndpoints();
app.MapAppointmentEndpoints();
app.MapAvailabilityEndpoints();
app.MapFinancialEndpoints();
app.MapPackageEndpoints();
app.MapPackageTemplateEndpoints();
app.MapInstructorEndpoints();
app.MapDashboardEndpoints();
app.MapPaymentEndpoints();
app.MapAdminUserEndpoints();
app.MapBillingEndpoints();
app.MapAbacatePayWebhook();
app.MapSuperAdminEndpoints();
app.MapAffiliateEndpoints();
app.MapSuperAdminAffiliateEndpoints();
app.MapSystemConfigEndpoints();
app.MapDemoEndpoints();
app.MapAiAssistantEndpoints();
app.MapLocationEndpoints();

app.MapGet("/health", () => Results.Ok(new { Status = "healthy", Time = DateTime.UtcNow }));

app.Run();
