using AccountingSystem.Client.Shared.Dialogs;
using AccountingSystem.Shared.DTOs;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AccountingSystem.Client.Tests;

public class VendorDialogTests : DialogTestContext
{
    [Fact]
    public void Render_WhenCreatingVendor_ShouldShowCreateTitle()
    {
        var cut = RenderComponent<MudDialogProvider>(parameters => parameters
            .AddChildContent<VendorDialog>(p => p
                .Add(d => d.Vendor, null)));

        cut.Markup.Should().Contain("Create New Vendor");
    }

    [Fact]
    public void Render_WhenEditingVendor_ShouldShowEditTitle()
    {
        var cut = RenderComponent<MudDialogProvider>(parameters => parameters
            .AddChildContent<VendorDialog>(p => p
                .Add(d => d.Vendor, new VendorDTO { Id = 9, Name = "ACME" })));

        cut.Markup.Should().Contain("Edit Vendor");
    }
}
