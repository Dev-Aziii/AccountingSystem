using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using AngleSharp.Dom;
using AccountingSystem.Client.Pages.Auth;
using AccountingSystem.Client.Pages.SuperAdmin;
using AccountingSystem.Client.Services;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Security;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace AccountingSystem.Client.Tests;

public class LoginAttemptPolicyClientTests
{
    [Fact]
    public async Task AuthService_Login_WhenStructuredLockoutResponseIsReturned_ShouldThrowTypedFailure()
    {
        await using var context = CreateContext();
        RegisterCommonServices(context, request =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/api/auth/login");

            return new HttpResponseMessage((HttpStatusCode)423)
            {
                Content = JsonContent.Create(new AuthFailureResponseDTO
                {
                    ErrorCode = AuthFailureErrorCodes.TemporaryLockout,
                    Message = "Too many failed sign-in attempts.",
                    LockoutEndUtc = DateTime.UtcNow.AddMinutes(5),
                    RemainingSeconds = 300
                })
            };
        });

        var authService = context.Services.GetRequiredService<AuthService>();

        var act = async () => await authService.Login(new LoginDTO
        {
            Email = "lockout@test.com",
            Password = "WrongPassword123!"
        });

        var exception = await act.Should().ThrowAsync<AuthFailureClientException>();
        exception.Which.ErrorCode.Should().Be(AuthFailureErrorCodes.TemporaryLockout);
        exception.Which.RemainingSeconds.Should().Be(300);
        exception.Which.LockoutEndUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WhenTemporaryLockoutIsApplied_ShouldShowCountdownAndReenableSubmit()
    {
        await using var context = CreateContext();
        var requestCount = 0;
        RegisterCommonServices(context, request =>
        {
            requestCount++;
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/api/auth/login");

            return new HttpResponseMessage((HttpStatusCode)423)
            {
                Content = JsonContent.Create(new AuthFailureResponseDTO
                {
                    ErrorCode = AuthFailureErrorCodes.TemporaryLockout,
                    Message = "Too many failed sign-in attempts.",
                    RemainingSeconds = 2
                })
            };
        });

        var cut = context.Render<Login>();

        SetLoginCredentials(cut, "lockout@test.com", "WrongPassword123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Too many failed sign-in attempts.");
            cut.Markup.Should().Contain("Try again in");
        });
        requestCount.Should().Be(1);

        cut.Find("form").Submit();
        await Task.Delay(200);
        requestCount.Should().Be(1);

        await Task.Delay(TimeSpan.FromSeconds(3));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().NotContain("Try again in");
            cut.Markup.Should().NotContain("Too many failed sign-in attempts.");
        });

        cut.Find("form").Submit();
        cut.WaitForAssertion(() => requestCount.Should().Be(2));
    }

    [Fact]
    public async Task Login_WhenDisabledOrRateLimited_ShouldShowDistinctMessages()
    {
        await using var context = CreateContext();
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = JsonContent.Create(new AuthFailureResponseDTO
                {
                    ErrorCode = AuthFailureErrorCodes.AccountDisabled,
                    Message = "This account has been disabled. Contact your administrator to regain access.",
                    Disabled = true
                })
            },
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = JsonContent.Create(new AuthFailureResponseDTO
                {
                    ErrorCode = AuthFailureErrorCodes.TooManyRequests,
                    Message = "Too many requests. Please wait before retrying.",
                    RetryAfterSeconds = 12
                })
            }
        });

        RegisterCommonServices(context, request =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/api/auth/login");
            return responses.Dequeue();
        });

        var cut = context.Render<Login>();

        SetLoginCredentials(cut, "disabled@test.com", "WrongPassword123!");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("This account has been disabled. Contact your administrator to regain access.");
            cut.Markup.Should().NotContain("Try again in");
            cut.Find("button").HasAttribute("disabled").Should().BeFalse();
        });

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Too many requests. Please wait before retrying. Retry in 12 second(s)."));
    }

    [Fact]
    public async Task GlobalUserManager_ShouldShowDisabledReasonAndTemporaryLockoutSeparately()
    {
        await using var context = CreateContext();
        RegisterCommonServices(context, request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.AbsolutePath.Should().Be("/api/superadmin/users");

            return JsonResponse(new List<GlobalUserDTO>
            {
                new()
                {
                    Id = 44,
                    FullName = "Blocked User",
                    Email = "blocked@test.com",
                    Role = ApplicationRoles.Accounting,
                    CompanyName = "Contoso",
                    CompanyId = 91,
                    IsActive = false,
                    Status = ApplicationUserStatuses.Blocked,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    DisabledReason = ApplicationUserDisableReasons.RepeatedLockouts,
                    LockoutEndUtc = DateTime.UtcNow.AddMinutes(10)
                }
            });
        });

        context.Services.AddSingleton<SuperAdminService>();

        var cut = context.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<MudSnackbarProvider>(2);
            builder.CloseComponent();
            builder.OpenComponent<GlobalUserManager>(3);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(ApplicationUserDisableReasonDisplayNames.RepeatedLockouts);
            cut.Markup.Should().Contain("Locked until");
        });
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        return context;
    }

    private static void RegisterCommonServices(TestContext context, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        context.Services.AddSingleton<Blazored.LocalStorage.ILocalStorageService, InMemoryLocalStorageService>();
        context.Services.AddSingleton<TokenStorageService>();
        context.Services.AddSingleton(httpClient);
        context.Services.AddSingleton<ApiService>(sp => new ApiService(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<TokenStorageService>(),
            context.JSInterop.JSRuntime));
        context.Services.AddSingleton<AuthenticationStateProvider>(_ => new StaticAuthenticationStateProvider());
        context.Services.AddSingleton<PendingMfaLoginStateService>();
        context.Services.AddSingleton<AuthService>();
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload)
        };
    }

    private static void SetLoginCredentials(IRenderedComponent<Login> cut, string email, string password)
    {
        var inputs = cut.FindAll("input").OfType<IElement>().ToList();
        var emailInput = inputs.First(input =>
            !string.Equals(input.GetAttribute("type"), "password", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(input.GetAttribute("type"), "checkbox", StringComparison.OrdinalIgnoreCase));
        var passwordInput = inputs.First(input =>
            string.Equals(input.GetAttribute("type"), "password", StringComparison.OrdinalIgnoreCase));

        emailInput.Input(email);
        emailInput.Change(email);
        passwordInput.Input(password);
        passwordInput.Change(password);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }

    private sealed class StaticAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _state = new(
            new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "test")));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
    }
}
