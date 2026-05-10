using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;

namespace HandwritingPad.Services;

public static class OcrRuntimeEnvironmentVerifier
{
    public static OcrRuntimeEnvironment VerifyOrThrow(PaddleOcrOnnxOptions options)
    {
        var platform = GetPlatformName();
        var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                $"当前 OCR 方案只支持 Windows/Linux。当前平台：{platform}");
        }

        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException(
                $"找不到 {options.ModelProfile} ONNX 模型文件。请运行 scripts/download_models 脚本。",
                options.ModelPath);
        }

        if (!File.Exists(options.DictionaryPath))
        {
            throw new FileNotFoundException(
                $"找不到 {options.ModelProfile} 字典文件。请运行 scripts/download_models 脚本。",
                options.DictionaryPath);
        }

        var sha256 = ComputeSha256(options.ModelPath);
        var expectedSha256 = PaddleOcrOnnxOptions.GetExpectedSha256(options.ModelProfile);

        if (options.VerifyModelSha256 &&
            !string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "PP-OCRv5 ONNX 模型 SHA256 不匹配，文件可能下载错误或版本不一致。"
                + Environment.NewLine
                + $"模型：{options.ModelProfile}"
                + Environment.NewLine
                + $"文件：{options.ModelPath}"
                + Environment.NewLine
                + $"期望：{expectedSha256}"
                + Environment.NewLine
                + $"实际：{sha256}");
        }

        using var sessionOptions = OnnxSessionOptionsFactory.Create(
            options,
            out var effectiveProvider,
            out _);
        using var session = new InferenceSession(options.ModelPath, sessionOptions);

        var input = session.InputMetadata.First();
        var output = session.OutputMetadata.First();

        var inputShape = string.Join(",", input.Value.Dimensions.Select(x => x.ToString()));
        var outputShape = string.Join(",", output.Value.Dimensions.Select(x => x.ToString()));

        if (input.Value.Dimensions.Length != 4)
        {
            throw new InvalidOperationException(
                $"OCR 模型输入维度应为 [N,C,H,W]，当前为：[{inputShape}]");
        }

        if (output.Value.Dimensions.Length is not 2 and not 3)
        {
            throw new InvalidOperationException(
                $"OCR 模型输出维度应为 [N,T,C] 或 [T,C]，当前为：[{outputShape}]");
        }

        var dictLines = File.ReadAllLines(options.DictionaryPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Count();

        if (dictLines < 1000)
        {
            throw new InvalidOperationException(
                $"OCR 字典文件行数异常：{dictLines}。请确认使用 ppocrv5_dict.txt。");
        }

        return new OcrRuntimeEnvironment
        {
            Platform = platform,
            Architecture = architecture,
            ModelPath = options.ModelPath,
            DictionaryPath = options.DictionaryPath,
            ModelSha256 = sha256,
            InputName = input.Key,
            InputShape = inputShape,
            OutputName = output.Key,
            OutputShape = outputShape
        };
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetPlatformName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }
}
