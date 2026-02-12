using AccountingSystem.API.Data;
using AccountingSystem.API.Middleware;
using AccountingSystem.API.Services;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. License Setup ---
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddHttpContextAccessor(); // REQUIRED for TenantService
builder.Services.AddScoped<ITenantService, TenantService>();

// --- 2. Database Context Setup ---
builder.Services.AddDbContext<AccountingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 3. Dependency Injection (Register Services) ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IPayableService, PayableService>();
builder.Services.AddScoped<IReceivableService, ReceivableService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHttpClient<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<ICaptchaService, CaptchaService>();

// --- 4. Authentication Setup (JWT) ---
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured in appsettings.json");
var key = Encoding.ASCII.GetBytes(secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ClockSkew = TimeSpan.Zero
    };
});

// Configure CORS for Blazor Client
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

// --- 5. Swagger Configuration (OpenAPI) ---
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

// --- 6. DATA SEEDING ---
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
    }
}

// --- 7. HTTP Request Pipeline ---

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging(); // Enable WASM debugging locally
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- CRITICAL DEPLOYMENT SETTINGS START ---
// 1. Serve the Blazor WebAssembly framework files (.wasm, .dll)
app.UseBlazorFrameworkFiles();

// 2. Serve static files (css, images, js)
app.UseStaticFiles();
// --- CRITICAL DEPLOYMENT SETTINGS END ---

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseMiddleware<JwtMiddleware>();
app.UseMiddleware<TenantAccessMiddleware>();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();

// --- CRITICAL FALLBACK ROUTING ---
// 3. If the user requests a page that isn't an API endpoint (like /dashboard), load the Blazor app.
app.MapFallbackToFile("index.html");

app.Run();