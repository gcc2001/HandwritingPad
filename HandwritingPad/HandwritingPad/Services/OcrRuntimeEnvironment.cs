namespace HandwritingPad.Services;

public sealed record OcrRuntimeEnvironment
{
    public string Platform { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string ModelPath { get; init; } = "";
    public string DictionaryPath { get; init; } = "";
    public string ModelSha256 { get; init; } = "";
    public string DictionarySha256 { get; init; } = "";
    public string InputName { get; init; } = "";
    public string InputShape { get; init; } = "";
    public string OutputName { get; init; } = "";
    public string OutputShape { get; init; } = "";
}
