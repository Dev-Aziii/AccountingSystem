using AccountingSystem.Client.Shared.Dialogs;
using AccountingSystem.Shared.DTOs;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AccountingSystem.Client.Tests;

public class AccountDialogTests : DialogTestContext
{
    [Fact]
    public void Render_WhenCreatingAccount_ShouldShowCreateTitle()
    {
        var cut = Render<MudDialogProvider>(parameters => parameters
            .AddChildContent<AccountDialog>(p => p
                .Add(d => d.Account, null)));
        cut.Markup.Should().Contain("Create New Account");
    }

    [Fact]
    public void Render_WhenEditingAccount_ShouldShowEditTitle()
    {
        var cut = Render<MudDialogProvider>(parameters => parameters
            .AddChildContent<AccountDialog>(p => p
                .Add(d => d.Account, new AccountDTO { Id = 22, Name = "Cash" })));
        cut.Markup.Should().Contain("Edit Account");
    }
}