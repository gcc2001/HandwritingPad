using System.Runtime.InteropServices;

namespace HandwritingPad.Services;

public static class NativeLibraryBootstrap
{
    public static void Configure()
    {
        if (OperatingSystem.IsWindows())
        {
            ConfigureWindowsDllSearchPath();
        }
    }

    private static void ConfigureWindowsDllSearchPath()
    {
        var cudaDir = Path.Combine(AppContext.BaseDirectory, "cuda");
        if (!Directory.Exists(cudaDir))
        {
            return;
        }

        // Make bundled cuDNN DLLs in ./cuda visible to the Windows loader before ONNX Runtime loads providers.
        SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);
        AddDllDirectory(cudaDir);
    }

    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
    private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr AddDllDirectory(string newDirectory);
}
