using AccountingSystem.Client.Shared.Dialogs;
using AccountingSystem.Shared.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Xunit;

namespace AccountingSystem.Client.Tests;

public class AccountDialogTests : DialogTestContext
{
    [Fact]
    public void Render_WhenCreatingAccount_ShouldShowCreateTitle()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(AccountDialog.Account), null }
        };

        dialogService.Show<AccountDialog>("Create New Account", parameters);

        dialogProvider.WaitForAssertion(() =>
            dialogProvider.Markup.Should().Contain("Create New Account"));
    }

    [Fact]
    public void Render_WhenEditingAccount_ShouldShowEditTitle()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(AccountDialog.Account), new AccountDTO { Id = 22, Name = "Cash" } }
        };

        dialogService.Show<AccountDialog>("Edit Account", parameters);

        dialogProvider.WaitForAssertion(() =>
            dialogProvider.Markup.Should().Contain("Edit Account"));
    }
}
