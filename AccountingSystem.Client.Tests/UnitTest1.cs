using System.Security.Claims;
using AccountingSystem.Client.Shared;
using AccountingSystem.Client.Shared.Dialogs;
using AccountingSystem.Shared.DTOs;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace AccountingSystem.Client.Tests;

public class RedirectToLoginTests : BunitContext
{
    [Fact]
    public void OnInitializedAsync_WhenUserIsAnonymous_ShouldRedirectToRoot()
    {
        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var authState = new AuthenticationState(anonymousPrincipal);

        Render<RedirectToLogin>(parameters => parameters
            .AddCascadingValue(Task.FromResult(authState)));

        var navigationManager = Services.GetService<Microsoft.AspNetCore.Components.NavigationManager>();

        navigationManager.Should().NotBeNull();
        navigationManager!.Uri.Should().EndWith('/');
    }
}

public class AuditDetailsDialogTests : BunitContext
{
    public AuditDetailsDialogTests()
    {
        Services.AddMudServices();
    }

    [Fact]
    public void Render_WhenLogProvided_ShouldShowAuditDetails()
    {
        var log = new AuditLogDTO
        {
            UserEmail = "auditor@contoso.com",
            Action = "USER-CREATE",
            EntityName = "User",
            Changes = "{\"name\":\"Alice\"}",
            Timestamp = DateTime.UtcNow
        };

        var cut = Render<AuditDetailsDialog>(parameters => parameters.Add(p => p.Log, log));

        cut.Markup.Should().Contain("Audit Log Details");
        cut.Markup.Should().Contain("auditor@contoso.com");
        cut.Markup.Should().Contain("USER-CREATE");
    }

    [Fact]
    public void Render_WhenLogActionIsDelete_ShouldUseDeleteBadgeClass()
    {
        var log = new AuditLogDTO
        {
            UserEmail = "auditor@contoso.com",
            Action = "DELETE",
            EntityName = "Invoice",
            Changes = "{}",
            Timestamp = DateTime.UtcNow
        };

        var cut = Render<AuditDetailsDialog>(parameters => parameters.Add(p => p.Log, log));

        cut.Markup.Should().Contain("badge-red");
    }
}
