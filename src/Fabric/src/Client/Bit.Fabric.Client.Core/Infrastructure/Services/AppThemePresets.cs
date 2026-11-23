namespace Bit.Fabric.Client.Core.Infrastructure.Services;

/// <summary>
/// The design system this app was created with, in the one place every head reads it from.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Light"/> / <see cref="Dark"/> are the bit BlazorUI presets the host pages pin as
/// <c>bit-theme-light</c> / <c>bit-theme-dark</c>, and <see cref="Background"/> is the page background the
/// native chrome is painted with - the Windows caption, the Android status bar, and every WebView before it
/// has painted anything. Those are not part of the document, so no CSS variable can reach them and the color
/// has to exist in C# before the app starts.
/// </para>
/// <para>
/// <c>Styles/_brand.scss</c> re-values both presets in place, so <see cref="LightBackground"/> and
/// <see cref="DarkBackground"/> have to stay equal to the <c>--bit-clr-bg-pri</c> it gives them, or the native
/// chrome and the page meet at a visible seam on launch and on every theme switch.
/// </para>
/// </remarks>
public static class AppThemePresets
{
    public const string Light = BitExtraThemePresets.Fluent2Light;

    public const string Dark = BitExtraThemePresets.Fluent2Dark;

    /// <summary><c>--bit-clr-bg-pri</c> of <see cref="Light"/>, as <c>Styles/_brand.scss</c> defines it.</summary>
    public const string LightBackground = "#FFFFFF";

    /// <summary><c>--bit-clr-bg-pri</c> of <see cref="Dark"/>, as <c>Styles/_brand.scss</c> defines it.</summary>
    public const string DarkBackground = "#1E1D1D";

    /// <summary>The page background of the theme the app is currently on.</summary>
    public static string Background(bool isDark) => isDark ? DarkBackground : LightBackground;

    /// <summary>
    /// Registers the re-valued surfaces of <see cref="Light"/> and <see cref="Dark"/>, so whatever reads the
    /// packaged presets' surfaces - the first-paint theme-color tag of <c>BitThemeHead</c> - carries them too.
    /// </summary>
    public static void RegisterSurfaces()
    {
        BitThemePresetRegistry.Register(new BitThemePreset { Name = Light, BackgroundPrimary = LightBackground, BackgroundSecondary = "#F9F8F7" });
        BitThemePresetRegistry.Register(new BitThemePreset { Name = Dark, BackgroundPrimary = DarkBackground, BackgroundSecondary = "#282726" });
    }
}
