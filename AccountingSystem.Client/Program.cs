using AccountingSystem.Client;
using AccountingSystem.Client.Auth;
using AccountingSystem.Client.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 1. Infrastructure Services
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<TokenStorageService>();
builder.Services.AddScoped<ApiService>();

// 2. Authentication Services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<AuthService>();

// 3. Domain Services
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<PayableService>();
builder.Services.AddScoped<ReceivableService>();
builder.Services.AddScoped<IPaymentClientService, PaymentClientService>();

// 4. HTTP Configuration
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7273") });

await builder.Build().RunAsync();