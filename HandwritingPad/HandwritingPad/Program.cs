using Avalonia;
using Avalonia.Media;
using HandwritingPad.Services;

namespace HandwritingPad;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        NativeLibraryBootstrap.Configure();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(
                new FontManagerOptions
                {
                    // Debian minimal images may not have a usable system default font.
                    DefaultFamilyName =
                        "avares://HandwritingPad/Assets/NotoSansSC-Regular.otf#Noto Sans SC",
                }
            )
            .LogToTrace();
    }
}
