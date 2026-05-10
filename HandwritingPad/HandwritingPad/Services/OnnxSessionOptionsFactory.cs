using Microsoft.ML.OnnxRuntime;

namespace HandwritingPad.Services;

public static class OnnxSessionOptionsFactory
{
    public static SessionOptions Create(
        PaddleOcrOnnxOptions options,
        out string effectiveProvider,
        out string providerMessage)
    {
        SessionOptions sessionOptions;

        switch (options.ExecutionProvider)
        {
            case OcrExecutionProvider.Cuda:
#if ORT_CUDA
                CudaDependencyDiagnostics.ThrowIfCudaDependenciesMissing();
                sessionOptions = SessionOptions.MakeSessionOptionWithCudaProvider(options.DeviceId);
                effectiveProvider = "CUDA";
                providerMessage = $"CUDA Execution Provider, device {options.DeviceId}";
                break;
#else
                throw new InvalidOperationException(
                    "当前程序不是 CUDA 版构建。请使用 -p:OnnxRuntimeFlavor=cuda 编译发布。 ");
#endif

            case OcrExecutionProvider.DirectML:
#if ORT_DIRECTML
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("DirectML 只支持 Windows。 ");
                }
                sessionOptions = new SessionOptions();
                sessionOptions.AppendExecutionProvider_DML(options.DeviceId);
                effectiveProvider = "DirectML";
                providerMessage = $"DirectML Execution Provider, device {options.DeviceId}";
                break;
#else
                throw new InvalidOperationException(
                    "当前程序不是 DirectML 版构建。请使用 -p:OnnxRuntimeFlavor=directml 编译发布。 ");
#endif

            case OcrExecutionProvider.Cpu:
            default:
                sessionOptions = CreateCpuOptions(options);
                effectiveProvider = "CPU";
                providerMessage = "CPU Execution Provider";
                break;
        }

        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        sessionOptions.EnableMemoryPattern = true;
        sessionOptions.EnableCpuMemArena = true;
        return sessionOptions;
    }

    private static SessionOptions CreateCpuOptions(PaddleOcrOnnxOptions options)
    {
        var sessionOptions = new SessionOptions
        {
            IntraOpNumThreads = Math.Max(1, options.ThreadCount),
            InterOpNumThreads = 1
        };

        if (options.DisableThreadSpinning)
        {
            sessionOptions.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
            sessionOptions.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");
        }

        return sessionOptions;
    }
}
