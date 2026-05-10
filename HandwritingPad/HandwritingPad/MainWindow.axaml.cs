using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandwritingPad.Controls;
using HandwritingPad.Models;
using HandwritingPad.Services;
using HandwritingPad.ViewModels;

namespace HandwritingPad;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(
            new SwitchableHandwritingRecognizer(),
            new TextCommitService(new PlatformInputInjector()));

        DataContext = _viewModel;
        InkPad.StrokesChanged += InkPad_OnStrokesChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }

    private void InkPad_OnStrokesChanged(object? sender, EventArgs e)
    {
        if (sender is not InkPad inkPad)
        {
            return;
        }

        _viewModel.OnStrokesChanged(
            inkPad.GetSnapshot(),
            new Size(inkPad.Bounds.Width, inkPad.Bounds.Height));
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        InkPad.Clear();
        _viewModel.Reset();
    }

    private async void CandidateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RecognitionCandidate candidate })
        {
            await _viewModel.CommitCandidateAsync(this, candidate);
        }
    }

    private async void SuggestionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RecognitionCandidate suggestion })
        {
            await _viewModel.CommitSuggestionAsync(this, suggestion);
        }
    }
}
