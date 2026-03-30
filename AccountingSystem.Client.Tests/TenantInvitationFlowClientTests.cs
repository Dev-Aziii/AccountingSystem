using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using AccountingSystem.Client.Pages.Admin;
using AccountingSystem.Client.Pages.Auth;
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

public class TenantInvitationFlowClientTests
{
    [Fact]
    public async Task ConfirmEmail_WhenInviteRequiresPasswordSetup_ShouldRedirectToInviteResetFlow()
    {
        await using var context = CreateContext();

        ConfigureAuthClient(context, request =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/api/auth/confirm-email");

            return JsonResponse(new ConfirmEmailResultDTO
            {
                Message = "Email confirmed. Continue to create your password to activate the account.",
                RequiresPasswordSetup = true,
                RedirectPath = "/reset-password?email=invite%40test.com&token=encoded-token&flow=invite"
            });
        });

        var navManager = context.Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/confirm-email?email=invite%40test.com&token=encoded-confirm-token");

        var cut = context.Render(builder =>
        {
            builder.OpenComponent<ConfirmEmail>(0);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() =>
            navManager.Uri.Should().EndWith("/reset-password?email=invite%40test.com&token=encoded-token&flow=invite"));
    }

    [Fact]
    public async Task ResetPassword_WhenInviteFlow_ShouldShowActivationCopy()
    {
        await using var context = CreateContext();

        ConfigureAuthClient(context, _ => JsonResponse(new { message = "ok" }));
        var navManager = context.Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/reset-password?email=invite%40test.com&token=encoded-reset-token&flow=invite");

        var cut = context.Render(builder =>
        {
            builder.OpenComponent<ResetPassword>(0);
            builder.CloseComponent();
        });

        cut.Markup.Should().Contain("Finish account setup");
        cut.Markup.Should().Contain("Create Password");
        cut.Markup.Should().Contain("activate your account");
    }

    [Fact]
    public async Task UserManagement_ShouldRenderInviteFieldsAndPendingSetupUsers()
    {
        await using var context = CreateContext();

        ConfigureUserClient(context, request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.PathAndQuery.Should().Contain("api/users?includeArchived=False");

            var users = new List<UserDTO>
            {
                new()
                {
                    Id = 1,
                    Email = "pending@test.com",
                    FullName = "Pending User",
                    RoleName = ApplicationRoles.Accounting,
                    Status = ApplicationUserStatuses.Invited,
                    IsActive = false,
                    IsDeleted = false
                },
                new()
                {
                    Id = 2,
                    Email = "active@test.com",
                    FullName = "Active User",
                    RoleName = ApplicationRoles.Management,
                    Status = ApplicationUserStatuses.Active,
                    IsActive = true,
                    IsDeleted = false
                }
            };

            return JsonResponse(users);
        });

        var cut = context.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<MudSnackbarProvider>(2);
            builder.CloseComponent();
            builder.OpenComponent<UserManagement>(3);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Invite New User");
            cut.Markup.Should().Contain("First Name (Optional)");
            cut.Markup.Should().Contain("Last Name (Optional)");
            cut.Markup.Should().Contain("Send Invite");
            cut.Markup.Should().Contain("Pending Setup");
            cut.Markup.Should().Contain("aria-label=\"Resend Invite\"");
            cut.Markup.Should().NotContain("Full Name");
            cut.Markup.Should().NotContain("Create Account");
        });

        cut.WaitForAssertion(() =>
        {
            var assignableRoles = cut.FindComponents<MudSelectItem<string>>()
                .Select(component => component.Instance.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            assignableRoles.Should().BeEquivalentTo(
                new[]
                {
                    ApplicationRoles.Accounting,
                    ApplicationRoles.Management
                });
            assignableRoles.Should().NotContain(ApplicationRoles.TenantOwner);
            assignableRoles.Should().NotContain(ApplicationRoles.SuperAdmin);
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

    private static void ConfigureAuthClient(TestContext context, Func<HttpRequestMessage, HttpResponseMessage> responder)
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
        context.Services.AddSingleton<AuthService>();
    }

    private static void ConfigureUserClient(TestContext context, Func<HttpRequestMessage, HttpResponseMessage> responder)
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
        context.Services.AddSingleton<UserService>();
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload)
        };
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
