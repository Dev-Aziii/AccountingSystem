using System;
using AccountingSystem.Client.Shared.Dialogs;
using AccountingSystem.Shared.DTOs;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AccountingSystem.Client.Tests;

public class AuditDetailsDialogTests : DialogTestContext
{
    [Fact]
    public void Render_WhenLogProvided_ShouldShowAuditDetails()
    {
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

        var cut = Render<MudDialogProvider>(parameters => parameters
            .AddChildContent<AuditDetailsDialog>(p => p
                .Add(d => d.Log, log)));

        cut.Markup.Should().Contain("Audit Log Details");
        cut.Markup.Should().Contain("auditor@example.com");
    }

    [Fact]
    public void Render_WhenLogActionIsDelete_ShouldUseDeleteBadgeClass()
    {
        var log = new AuditLogDTO
        {
            UserEmail = "auditor@example.com",
            Action = "DELETE",
            EntityName = "/api/invoices",
            Timestamp = DateTime.UtcNow,
            Changes = "{}"
        };

        var cut = Render<MudDialogProvider>(parameters => parameters
            .AddChildContent<AuditDetailsDialog>(p => p
                .Add(d => d.Log, log)));

        cut.Markup.Should().Contain("badge-red");
    }
}