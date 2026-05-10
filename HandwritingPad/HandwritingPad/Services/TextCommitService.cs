using Avalonia.Controls;

namespace HandwritingPad.Services;

public sealed class TextCommitService : ITextCommitService
{
    private readonly IInputInjector _inputInjector;

    public TextCommitService(IInputInjector inputInjector)
    {
        _inputInjector = inputInjector;
    }

    public async Task CommitAsync(Window owner, string text, bool autoPaste, CancellationToken cancellationToken = default)
    {
        if (owner.Clipboard is null)
        {
            throw new InvalidOperationException("当前平台无法访问系统剪贴板。");
        }

        await owner.Clipboard.SetTextAsync(text);
        await owner.Clipboard.FlushAsync();

        if (!autoPaste)
        {
            return;
        }

        owner.Hide();
        try
        {
            await Task.Delay(180, cancellationToken);
            await _inputInjector.PasteAsync(cancellationToken);
            await Task.Delay(80, cancellationToken);
        }
        finally
        {
            owner.Show();
            owner.Activate();
        }
    }
}
