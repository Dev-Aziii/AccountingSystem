using MudBlazor;

namespace AccountingSystem.Client.Shared;

public static class AppTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#379391",
            PrimaryDarken = "#2f7f7d",
            PrimaryLighten = "#53a8a6",
            Background = "#f3f4f6",
            Surface = "#ffffff",
            TextPrimary = "#1f2937",
            TextSecondary = "#6b7280",
            Divider = "#e5e7eb",
            AppbarBackground = "#ffffff",
            AppbarText = "#1f2937",
            DrawerBackground = "#ffffff",
            DrawerText = "#1f2937",
            DrawerIcon = "#6b7280"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px"
        }
    };
}
