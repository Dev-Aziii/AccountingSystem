using AccountingSystem.Client.Shared.Dialogs;
using AccountingSystem.Shared.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Xunit;

namespace AccountingSystem.Client.Tests;

public class VendorDialogTests : DialogTestContext
{
    [Fact]
    public async Task Render_WhenCreatingVendor_ShouldShowCreateTitle()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(VendorDialog.Vendor), null }
        };

        await dialogService.ShowAsync<VendorDialog>("Create New Vendor", parameters);

        dialogProvider.Markup.Should().Contain("Create New Vendor");
    }

    [Fact]
    public async Task Render_WhenEditingVendor_ShouldShowEditTitle()
    {
        var dialogProvider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(VendorDialog.Vendor), new VendorDTO { Id = 9, Name = "ACME" } }
        };

        await dialogService.ShowAsync<VendorDialog>("Edit Vendor", parameters);

        dialogProvider.Markup.Should().Contain("Edit Vendor");
    }
}