using System;
using System.Net.Http;
using AccountingSystem.Client.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace AccountingSystem.Client.Tests;

public abstract class DialogTestContext : BunitContext
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
    }
}
