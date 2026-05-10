namespace HandwritingPad.Services;

public static class OnnxRuntimeBuildInfo
{
    public static string Flavor
    {
        get
        {
#if ORT_CUDA
            return "CUDA";
#elif ORT_DIRECTML
            return "DirectML";
#else
            return "CPU";
#endif
        }
    }

    public static OcrExecutionProvider DefaultExecutionProvider
    {
        get
        {
#if ORT_CUDA
            return OcrExecutionProvider.Cuda;
#elif ORT_DIRECTML
            return OcrExecutionProvider.DirectML;
#else
            return OcrExecutionProvider.Cpu;
#endif
        }
    }
}
