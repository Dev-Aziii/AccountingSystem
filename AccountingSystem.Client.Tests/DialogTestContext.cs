using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Blazored.LocalStorage;
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
        Services.AddSingleton<ILocalStorageService, InMemoryLocalStorageService>();
        Services.AddSingleton<TokenStorageService>();
        Services.AddSingleton<ApiService>(sp => new ApiService(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<TokenStorageService>(),
            JSInterop.JSRuntime));
        Services.AddSingleton<PayableService>();
        Services.AddSingleton<LedgerService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await Services.DisposeAsync();
    }
}

internal sealed class InMemoryLocalStorageService : ILocalStorageService
{
    private readonly Dictionary<string, object?> _storage = new(StringComparer.Ordinal);

    public event EventHandler<ChangingEventArgs>? Changing;

    public event EventHandler<ChangedEventArgs>? Changed;

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        _storage.Clear();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ContainKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_storage.ContainsKey(key));
    }

    public ValueTask<string> KeyAsync(int index, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_storage.Keys.ElementAt(index));
    }

    public ValueTask<IEnumerable<string>> KeysAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IEnumerable<string>>(_storage.Keys.ToArray());
    }

    public ValueTask<int> LengthAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_storage.Count);
    }

    public ValueTask<T?> GetItemAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_storage.TryGetValue(key, out var value) && value is T typed)
        {
            return ValueTask.FromResult<T?>(typed);
        }

        return ValueTask.FromResult<T?>(default);
    }

    public ValueTask<string?> GetItemAsStringAsync(string key, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_storage.TryGetValue(key, out var value) ? value?.ToString() : null);
    }

    public ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default)
    {
        _storage.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveItemsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            _storage.Remove(key);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask SetItemAsync<T>(string key, T data, CancellationToken cancellationToken = default)
    {
        _storage[key] = data;
        return ValueTask.CompletedTask;
    }

    public ValueTask SetItemAsStringAsync(string key, string data, CancellationToken cancellationToken = default)
    {
        _storage[key] = data;
        return ValueTask.CompletedTask;
    }
}
