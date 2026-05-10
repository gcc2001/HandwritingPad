using System.Runtime.InteropServices;

namespace HandwritingPad.Services;

public static class CudaDependencyDiagnostics
{
    public static void ThrowIfCudaDependenciesMissing()
    {
        if (!IsCudaBuild())
        {
            return;
        }

        var missing = GetMissingCudaLibraries();
        if (missing.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "CUDA 推理后端启动失败：缺少 NVIDIA Driver / CUDA Toolkit / cuDNN 动态库。"
            + Environment.NewLine
            + Environment.NewLine
            + "缺失库："
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing.Select(x => " - " + x))
            + Environment.NewLine
            + Environment.NewLine
            + GetFixHint());
    }

    public static IReadOnlyList<string> GetMissingCudaLibraries()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetMissingWindowsLibraries();
        }

        if (OperatingSystem.IsLinux())
        {
            return GetMissingLinuxLibraries();
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> GetMissingWindowsLibraries()
    {
        // User is assumed to install NVIDIA Driver + CUDA Toolkit 12.x.
        // App package supplies cuDNN 9.x under ./cuda.
        var libraries = new[]
        {
            "nvcuda.dll",          // NVIDIA driver, system-provided
            "cudart64_12.dll",    // CUDA Toolkit, system-provided under assumption
            "cublas64_12.dll",    // CUDA Toolkit
            "cublasLt64_12.dll",  // CUDA Toolkit
            "cufft64_11.dll",     // CUDA Toolkit
            "curand64_10.dll",    // CUDA Toolkit
            "cudnn64_9.dll"       // bundled cuDNN or system cuDNN
        };

        return libraries
            .Where(x => !TryLoadWindows(x))
            .ToArray();
    }

    private static IReadOnlyList<string> GetMissingLinuxLibraries()
    {
        var libraries = new[]
        {
            "libcuda.so.1",       // NVIDIA driver, system-provided
            "libcudart.so.12",    // CUDA Toolkit, system-provided under assumption
            "libcublas.so.12",    // CUDA Toolkit
            "libcublasLt.so.12",  // CUDA Toolkit
            "libcufft.so.11",     // CUDA Toolkit
            "libcurand.so.10",    // CUDA Toolkit
            "libcudnn.so.9"       // bundled cuDNN or system cuDNN
        };

        return libraries
            .Where(x => !TryLoadLinux(x))
            .ToArray();
    }

    private static bool TryLoadWindows(string libraryName)
    {
        // nvcuda and CUDA Toolkit libraries are expected from system PATH.
        // cuDNN can be bundled under ./cuda; try that path first for all non-driver DLLs.
        if (!string.Equals(libraryName, "nvcuda.dll", StringComparison.OrdinalIgnoreCase))
        {
            var bundledPath = Path.Combine(AppContext.BaseDirectory, "cuda", libraryName);
            if (File.Exists(bundledPath) && TryLoad(bundledPath))
            {
                return true;
            }
        }

        return TryLoad(libraryName);
    }

    private static bool TryLoadLinux(string libraryName)
    {
        if (!string.Equals(libraryName, "libcuda.so.1", StringComparison.Ordinal))
        {
            var bundledPath = Path.Combine(AppContext.BaseDirectory, "cuda", libraryName);
            if (File.Exists(bundledPath) && TryLoad(bundledPath))
            {
                return true;
            }
        }

        return TryLoad(libraryName);
    }

    private static bool TryLoad(string libraryNameOrPath)
    {
        try
        {
            if (NativeLibrary.TryLoad(libraryNameOrPath, out var handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCudaBuild()
    {
#if ORT_CUDA
        return true;
#else
        return false;
#endif
    }

    private static string GetFixHint()
    {
        if (OperatingSystem.IsWindows())
        {
            return
                "Windows 修复方式：" + Environment.NewLine +
                "1. 确认 NVIDIA Driver 已安装，且 nvidia-smi 可运行。" + Environment.NewLine +
                "2. 确认 CUDA Toolkit 12.x 已安装，CUDA bin 已加入 PATH。" + Environment.NewLine +
                "3. 运行 scripts/download_cuda_deps.ps1，把 cuDNN 9.x 下载到 Native/cuda/win-x64。" + Environment.NewLine +
                "4. 重新执行 scripts/build-win-x64.ps1 发布。";
        }

        if (OperatingSystem.IsLinux())
        {
            return
                "Linux 修复方式：" + Environment.NewLine +
                "1. 确认 NVIDIA Driver 已安装，且 nvidia-smi 可运行。" + Environment.NewLine +
                "2. 确认 CUDA Toolkit 12.x 已安装，CUDA lib64 已在 LD_LIBRARY_PATH 或 ldconfig 中。" + Environment.NewLine +
                "3. 运行 scripts/download_cuda_deps.sh，把 cuDNN 9.x 下载到 Native/cuda/linux-x64。" + Environment.NewLine +
                "4. 重新执行 scripts/build-linux-x64.sh 发布，并用 publish/linux-x64-cuda/run-handwritingpad-cuda.sh 启动。";
        }

        return "当前平台不支持 CUDA 后端。";
    }
}
