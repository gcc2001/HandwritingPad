using Avalonia.Controls;

namespace HandwritingPad.Services;

public interface ITextCommitService
{
    Task CommitAsync(
        Window owner,
        string text,
        bool autoPaste,
        CancellationToken cancellationToken = default);
}
