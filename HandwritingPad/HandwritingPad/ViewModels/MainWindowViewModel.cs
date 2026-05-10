using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using HandwritingPad.Models;
using HandwritingPad.Services;

namespace HandwritingPad.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly SwitchableHandwritingRecognizer _recognizer;
    private readonly ITextCommitService _textCommitService;
    private readonly AssociativeTextSuggestionService _suggestionService = new();
    private readonly object _ctsLock = new();

    private CancellationTokenSource? _activeRecognitionCts;
    private long _recognitionVersion;
    private bool _disposed;

    public ObservableCollection<RecognitionCandidate> Candidates { get; } = new();
    public ObservableCollection<RecognitionCandidate> Suggestions { get; } = new();

    public IReadOnlyList<OcrModelProfile> ModelProfiles { get; } =
        Enum.GetValues<OcrModelProfile>();

    [ObservableProperty]
    private OcrModelProfile _selectedModelProfile = OcrModelProfile.高精度;

    [ObservableProperty]
    private string _status = "请在手写区书写。停笔约 700ms 后自动识别。";

    [ObservableProperty]
    private string _runtimeStatus = $"后端：{OnnxRuntimeBuildInfo.Flavor}";

    [ObservableProperty]
    private string _lastInferenceText = "本次推理：—";

    [ObservableProperty]
    private bool _autoPaste;

    public TimeSpan InactivityDelay { get; set; } = TimeSpan.FromMilliseconds(700);

    public MainWindowViewModel(
        SwitchableHandwritingRecognizer recognizer,
        ITextCommitService textCommitService
    )
    {
        _recognizer = recognizer;
        _textCommitService = textCommitService;
        RuntimeStatus =
            $"模型：{FormatModelName(SelectedModelProfile)}，后端：{OnnxRuntimeBuildInfo.Flavor}";
    }

    partial void OnSelectedModelProfileChanged(OcrModelProfile value)
    {
        CancelPendingRecognition();
        Candidates.Clear();
        Suggestions.Clear();
        LastInferenceText = "本次推理：—";
        RuntimeStatus = $"模型：{FormatModelName(value)}，后端：{OnnxRuntimeBuildInfo.Flavor}";
        Status = $"已切换模型：{FormatModelName(value)}。请重新书写。";
    }

    public void Reset()
    {
        CancelPendingRecognition();
        Candidates.Clear();
        Suggestions.Clear();
        LastInferenceText = "本次推理：—";
        Status = "已清空。请重新书写。";
    }

    public void OnStrokesChanged(IReadOnlyList<InkStroke> strokes, Size canvasSize)
    {
        if (_disposed)
        {
            return;
        }

        var pointCount = strokes.Sum(x => x.Points.Count);
        if (pointCount == 0)
        {
            CancelPendingRecognition();
            Candidates.Clear();
            Status = "请在手写区书写。";
            return;
        }

        Suggestions.Clear();

        var version = Interlocked.Increment(ref _recognitionVersion);
        var cts = ReplaceRecognitionToken();
        var modelProfile = SelectedModelProfile;

        _ = RecognizeAfterIdleAsync(modelProfile, strokes, canvasSize, version, cts);
    }

    public async Task CommitCandidateAsync(Window owner, RecognitionCandidate candidate)
    {
        try
        {
            await _textCommitService.CommitAsync(owner, candidate.Text, AutoPaste);
            UpdateSuggestions(candidate.Text);

            Status = AutoPaste
                ? $"已复制并尝试粘贴：{candidate.Text}"
                : $"已复制到剪贴板：{candidate.Text}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public async Task CommitSuggestionAsync(Window owner, RecognitionCandidate suggestion)
    {
        try
        {
            await _textCommitService.CommitAsync(owner, suggestion.Text, AutoPaste);
            UpdateSuggestions(suggestion.Text);

            Status = AutoPaste
                ? $"已复制并尝试粘贴联想：{suggestion.Text}"
                : $"已复制联想到剪贴板：{suggestion.Text}";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private void UpdateSuggestions(string committedText)
    {
        Suggestions.Clear();

        foreach (var item in _suggestionService.GetSuggestions(committedText))
        {
            Suggestions.Add(item);
        }
    }

    private CancellationTokenSource ReplaceRecognitionToken()
    {
        var newCts = new CancellationTokenSource();
        CancellationTokenSource? oldCts;

        lock (_ctsLock)
        {
            oldCts = _activeRecognitionCts;
            _activeRecognitionCts = newCts;
        }

        TryCancel(oldCts);
        return newCts;
    }

    private void CancelPendingRecognition()
    {
        Interlocked.Increment(ref _recognitionVersion);

        CancellationTokenSource? oldCts;
        lock (_ctsLock)
        {
            oldCts = _activeRecognitionCts;
            _activeRecognitionCts = null;
        }

        TryCancel(oldCts);
    }

    private static void TryCancel(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    private bool IsCurrentRecognition(long version, CancellationTokenSource cts)
    {
        if (cts.IsCancellationRequested)
        {
            return false;
        }

        if (version != Interlocked.Read(ref _recognitionVersion))
        {
            return false;
        }

        lock (_ctsLock)
        {
            return ReferenceEquals(_activeRecognitionCts, cts);
        }
    }

    private async Task RecognizeAfterIdleAsync(
        OcrModelProfile modelProfile,
        IReadOnlyList<InkStroke> strokes,
        Size canvasSize,
        long version,
        CancellationTokenSource cts
    )
    {
        var token = cts.Token;

        try
        {
            Status = "等待停笔中……";
            await Task.Delay(InactivityDelay, token);

            if (!IsCurrentRecognition(version, cts))
            {
                return;
            }

            Status =
                $"识别中……模型：{FormatModelName(modelProfile)}，后端：{OnnxRuntimeBuildInfo.Flavor}";

            var sw = Stopwatch.StartNew();
            var result = await _recognizer.RecognizeAsync(modelProfile, strokes, canvasSize, token);
            sw.Stop();

            if (!IsCurrentRecognition(version, cts))
            {
                return;
            }

            Candidates.Clear();
            foreach (var candidate in result.Candidates.OrderByDescending(x => x.Confidence))
            {
                Candidates.Add(candidate);
            }

            RuntimeStatus =
                $"模型：{FormatModelName(result.ModelProfile)}，后端：{result.EffectiveProvider}";
            LastInferenceText =
                $"本次推理：{sw.ElapsedMilliseconds} ms，后端：{result.EffectiveProvider}";
            Status =
                Candidates.Count == 0
                    ? $"未识别到候选结果。{LastInferenceText}"
                    : $"识别完成，共 {Candidates.Count} 个候选。{LastInferenceText}";
        }
        catch (OperationCanceledException)
        {
            // 用户继续书写或清空，这是正常路径。旧任务结果会被丢弃。
        }
        catch (Exception ex)
        {
            if (IsCurrentRecognition(version, cts))
            {
                Candidates.Clear();
                LastInferenceText = "本次推理：失败";
                Status = $"识别失败：{ex.Message}";
            }
        }
        finally
        {
            lock (_ctsLock)
            {
                if (ReferenceEquals(_activeRecognitionCts, cts))
                {
                    _activeRecognitionCts = null;
                }
            }

            cts.Dispose();
        }
    }

    private static string FormatModelName(OcrModelProfile profile)
    {
        return profile switch
        {
            OcrModelProfile.快速 => "PP-OCRv5 Mobile",
            OcrModelProfile.高精度 => "PP-OCRv5 Server",
            _ => profile.ToString(),
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingRecognition();
        _recognizer.Dispose();
    }
}
