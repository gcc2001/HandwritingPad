namespace HandwritingPad.Services;

public interface IInputInjector
{
    Task PasteAsync(CancellationToken cancellationToken = default);
}
