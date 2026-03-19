using AccountingSystem.API.Configuration;
using AccountingSystem.API.Data;
using AccountingSystem.API.Middleware;
using AccountingSystem.API.Services;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

StartupConfigurationValidator.ValidateRequiredSettings(builder.Configuration, builder.Environment);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<AccountingDbContext>(options =>
    options.UseSqlServer(defaultConnection));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthSecurityAuditService, AuthSecurityAuditService>();
builder.Services.AddScoped<IYearEndCloseService, YearEndCloseService>();
builder.Services.AddScoped<IDocumentSequenceService, DocumentSequenceService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IPayableService, PayableService>();
builder.Services.AddScoped<IReceivableService, ReceivableService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHttpClient<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<ICaptchaService, CaptchaService>();

var tokenValidationParameters = JwtSettingsHelper.CreateTokenValidationParameters(builder.Configuration);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = tokenValidationParameters;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var auditService = context.HttpContext.RequestServices.GetRequiredService<IAuthSecurityAuditService>();
        await auditService.WriteAsync(
            "AUTH-RATE-LIMIT",
            userId: TryParseClaim(context.HttpContext.User, "UserId"),
            companyId: TryParseClaim(context.HttpContext.User, "CompanyId"),
            email: context.HttpContext.User.Identity?.Name,
            reason: context.HttpContext.Request.Path.Value,
            policy: context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);

        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { error = "Too many requests. Please wait before retrying." },
                cancellationToken: cancellationToken);
        }
    };

    options.AddPolicy(AuthRateLimitPolicyNames.Login, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"ip:{GetRemoteIpAddress(httpContext)}",
            factory: _ => CreateFixedWindowOptions(
                GetConfiguredPositiveInt("AuthSecurity:RateLimiting:Login:PermitLimit", 5),
                GetConfiguredPositiveInt("AuthSecurity:RateLimiting:Login:WindowSeconds", 60))));

    options.AddPolicy(AuthRateLimitPolicyNames.RegisterCompany, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"ip:{GetRemoteIpAddress(httpContext)}",
            factory: _ => CreateFixedWindowOptions(
                GetConfiguredPositiveInt("AuthSecurity:RateLimiting:RegisterCompany:PermitLimit", 3),
                GetConfiguredPositiveInt("AuthSecurity:RateLimiting:RegisterCompany:WindowSeconds", 600))));

    options.AddPolicy(AuthRateLimitPolicyNames.ChangePassword, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetUserOrIpPartitionKey(httpContext),
            factory: _ => CreateFixedWindowOptions(
                GetConfiguredPositiveInt("AuthSecurity:RateLimiting:ChangePassword:PermitLimit", 5),
                GetConfiguredPositiveInt("AuthSecurity:RateLimiting:ChangePassword:WindowSeconds", 600))));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient",
        policy =>
        {
            policy.WithOrigins("https://localhost:7150", "http://localhost:5240")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Integrated Accounting System API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AccountingDbContext>();
        context.Database.Migrate();
        await DataSeeder.SeedDataAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<JwtMiddleware>();
app.UseMiddleware<TenantAccessMiddleware>();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

int GetConfiguredPositiveInt(string key, int fallbackValue)
{
    var configuredValue = builder.Configuration.GetValue<int?>(key);
    return configuredValue is > 0 ? configuredValue.Value : fallbackValue;
}

static FixedWindowRateLimiterOptions CreateFixedWindowOptions(int permitLimit, int windowSeconds)
{
    return new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(windowSeconds),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true
    };
}

static string GetRemoteIpAddress(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string GetUserOrIpPartitionKey(HttpContext httpContext)
{
    var userId = httpContext.User.FindFirst("UserId")?.Value;
    return string.IsNullOrWhiteSpace(userId)
        ? $"ip:{GetRemoteIpAddress(httpContext)}"
        : $"user:{userId}";
}

static int? TryParseClaim(System.Security.Claims.ClaimsPrincipal user, string claimType)
{
    var claimValue = user.FindFirst(claimType)?.Value;
    return int.TryParse(claimValue, out var parsedValue) ? parsedValue : null;
}
