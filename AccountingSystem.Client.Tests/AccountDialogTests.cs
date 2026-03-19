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
    public async Task Render_WhenCreatingAccount_ShouldShowCreateTitle()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(AccountDialog.Account), null }
        };

        await dialogService.ShowAsync<AccountDialog>("Create New Account", parameters);

        dialogProvider.Markup.Should().Contain("Create New Account");
    }

    [Fact]
    public async Task Render_WhenEditingAccount_ShouldShowEditTitle()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(AccountDialog.Account), new AccountDTO { Id = 22, Name = "Cash" } }
        };

        await dialogService.ShowAsync<AccountDialog>("Edit Account", parameters);

        dialogProvider.Markup.Should().Contain("Edit Account");
    }
}