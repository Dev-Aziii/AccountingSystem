using AccountingSystem.Client.Shared.Components;
using Bunit;
using FluentAssertions;

namespace AccountingSystem.Client.Tests;

public class MfaQrCodeTests : IDisposable
{
    private readonly BunitContext _context = new();

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Render_WhenValueProvided_ShouldRenderSvgDataUriImage()
    {
        var cut = _context.Render<MfaQrCode>(parameters => parameters
            .Add(p => p.Value, "otpauth://totp/AccountingSystem:test@example.com?secret=JBSWY3DPEHPK3PXP&issuer=AccountingSystem"));

        var image = cut.Find("img");

        image.GetAttribute("src").Should().StartWith("data:image/svg+xml;base64,");
        image.GetAttribute("alt").Should().Be("Google Authenticator QR code");
        image.GetAttribute("width").Should().Be("220");
        image.GetAttribute("height").Should().Be("220");
    }
}
