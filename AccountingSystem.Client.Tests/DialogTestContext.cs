using System;
using System.Net.Http;
using System.Threading.Tasks;
using AccountingSystem.Client.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace AccountingSystem.Client.Tests;

public abstract class DialogTestContext : BunitContext, IAsyncLifetime
{
    protected DialogTestContext()
    {
        Services.AddMudServices();
        Services.AddSingleton<IDialogService, DialogService>();
        Services.AddSingleton(new HttpClient { BaseAddress = new Uri("http://localhost") });
        Services.AddSingleton(_ => new TokenStorageService(null!));
        Services.AddSingleton<ApiService>(sp => new ApiService(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<TokenStorageService>(),
            JSInterop.JSRuntime));
        Services.AddSingleton<PayableService>();
        Services.AddSingleton<LedgerService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
    }
}