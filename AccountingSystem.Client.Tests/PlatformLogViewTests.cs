using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using AccountingSystem.Client.Pages.SuperAdmin;
using AccountingSystem.Client.Services;
using AccountingSystem.Shared.DTOs;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace AccountingSystem.Client.Tests;

public class PlatformLogViewTests
{
    [Fact]
    public async Task AdminAuditLogs_ShouldRenderPlatformActionsSecurityEventsAndIpAddresses()
    {
        await using var context = CreateContext(request =>
        {
            request.RequestUri.Should().NotBeNull();

            return request.RequestUri!.AbsolutePath switch
            {
                "/api/superadmin/audit-logs" => JsonResponse(new List<SuperAdminAuditLogDTO>
                {
                    new()
                    {
                        Id = 1,
                        AdminEmail = "superadmin@test.com",
                        Action = "USER_STATUS_CHANGE",
                        TargetType = "User",
                        TargetName = "Target User",
                        TargetId = 20,
                        OldValue = "Active",
                        NewValue = "Blocked",
                        Details = "Status updated.",
                        IpAddress = "203.0.113.10",
                        Timestamp = new DateTime(2026, 3, 30, 4, 0, 0, DateTimeKind.Utc)
                    }
                }),
                "/api/superadmin/security-events" => JsonResponse(new List<PlatformSecurityEventDTO>
                {
                    new()
                    {
                        Id = 11,
                        CompanyId = 42,
                        CompanyName = "Contoso Books",
                        UserEmail = "owner@contoso.test",
                        Action = "AUTH-LOGIN-FAILURE",
                        Path = "/api/auth/login",
                        Details = "{\"reason\":\"InvalidPassword\"}",
                        IpAddress = "198.51.100.20",
                        Timestamp = new DateTime(2026, 3, 30, 4, 5, 0, DateTimeKind.Utc)
                    }
                }),
                _ => throw new InvalidOperationException($"Unexpected request: {request.RequestUri}")
            };
        });

        var cut = context.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<MudSnackbarProvider>(2);
            builder.CloseComponent();
            builder.OpenComponent<AdminAuditLogs>(3);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Platform Logs");
            cut.Markup.Should().Contain("Platform Admin Actions");
            cut.Markup.Should().Contain("Platform Security Events");
            cut.Markup.Should().Contain("203.0.113.10");
            cut.Markup.Should().Contain("Mar 30, 2026");
            cut.Markup.Should().Contain("12:00 PM");
        });

        cut.FindAll(".mud-tab").Last().Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("198.51.100.20");
            cut.Markup.Should().Contain("Contoso Books");
            cut.Markup.Should().Contain("Login Failure");
            cut.Markup.Should().Contain("12:05 PM");
        });
    }

    private static TestContext CreateContext(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

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
        context.Services.AddSingleton<SuperAdminService>();

        return context;
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
}
