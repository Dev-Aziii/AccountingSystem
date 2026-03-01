using System;
using AccountingSystem.Client.Shared.Dialogs;
using AccountingSystem.Shared.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Xunit;

namespace AccountingSystem.Client.Tests;

public class AuditDetailsDialogTests : DialogTestContext
{
    [Fact]
    public async Task Render_WhenLogProvided_ShouldShowAuditDetails()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var log = new AuditLogDTO
        {
            Id = 1,
            UserEmail = "auditor@example.com",
            Action = "POST",
            EntityName = "/api/invoices",
            EntityId = "12",
            Timestamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Changes = "{\"amount\":100}"
        };

        var parameters = new DialogParameters
        {
            { nameof(AuditDetailsDialog.Log), log }
        };

        await dialogService.ShowAsync<AuditDetailsDialog>("Audit Log Details", parameters);

        dialogProvider.Markup.Should().Contain("Audit Log Details");
        dialogProvider.Markup.Should().Contain("auditor@example.com");
    }

    [Fact]
    public async Task Render_WhenLogActionIsDelete_ShouldUseDeleteBadgeClass()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var log = new AuditLogDTO
        {
            UserEmail = "auditor@example.com",
            Action = "DELETE",
            EntityName = "/api/invoices",
            Timestamp = DateTime.UtcNow,
            Changes = "{}"
        };

        var parameters = new DialogParameters
        {
            { nameof(AuditDetailsDialog.Log), log }
        };

        await dialogService.ShowAsync<AuditDetailsDialog>("Audit Log Details", parameters);

        dialogProvider.Markup.Should().Contain("badge-red");
    }
}