using HandwritingPad.Models;

namespace HandwritingPad.Services;

public sealed class RecognitionCandidateReranker
{
    public IReadOnlyList<RecognitionCandidate> Rerank(
        IReadOnlyList<RecognitionCandidate> rawCandidates,
        RecognitionInputMode inputMode,
        InkLayoutInfo layout,
        OcrEnhancementOptions options)
    {
        return rawCandidates
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x with { Text = NormalizeText(x.Text) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .GroupBy(x => x.Text)
            .Select(group =>
            {
                var best = group.OrderByDescending(x => x.Confidence).First();
                var repeatBonus = Math.Min(0.10, (group.Count() - 1) * 0.025);
                var modeBonus = options.EnableInputModeRerank ? GetInputModeBonus(best.Text, inputMode) : 0.0;
                var invalidPenalty = options.EnableInputModeRerank ? GetInvalidPenalty(best.Text, inputMode) : 0.0;
                var layoutBonus = GetLayoutBonus(best.Text, layout);
                var layoutPenalty = GetLayoutPenalty(best.Text, layout);
                var confidence = best.Confidence + repeatBonus + modeBonus + layoutBonus - invalidPenalty - layoutPenalty;
                return new RecognitionCandidate(best.Text, Math.Clamp(confidence, 0.0, 1.0));
            })
            .Where(x => x.Confidence >= options.MinCandidateConfidence)
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Text.Length)
            .Take(options.MaxCandidateCount)
            .ToArray();
    }

    private static string NormalizeText(string text)
    {
        return text.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace(" ", "").Trim();
    }

    private static double GetLayoutBonus(string text, InkLayoutInfo layout)
    {
        if (layout.IsLikelyPunctuation && IsPunctuationText(text)) return 0.18;
        if (layout.IsLikelySingleCjkCharacter && IsSingleCjk(text)) return 0.16;
        return 0.0;
    }

    private static double GetLayoutPenalty(string text, InkLayoutInfo layout)
    {
        if (layout.IsLikelySingleCjkCharacter && IsTwoCjkCharacters(text)) return 0.22;
        if (layout.IsLikelyPunctuation && !IsPunctuationText(text)) return 0.28;
        return 0.0;
    }

    private static double GetInputModeBonus(string text, RecognitionInputMode mode)
    {
        return mode switch
        {
            RecognitionInputMode.ChineseText when text.Any(IsCjk) => 0.08,
            RecognitionInputMode.EnglishText when text.All(IsEnglishLike) => 0.08,
            RecognitionInputMode.Number when text.All(IsNumberChar) => 0.16,
            RecognitionInputMode.PhoneNumber when text.All(IsPhoneChar) => 0.16,
            RecognitionInputMode.Email when text.All(IsEmailChar) && text.Contains('@') => 0.14,
            RecognitionInputMode.Amount when text.All(IsAmountChar) => 0.14,
            RecognitionInputMode.Symbol when text.All(IsSymbolOrPunctuationChar) => 0.12,
            RecognitionInputMode.Punctuation when IsPunctuationText(text) => 0.24,
            _ => 0.0
        };
    }

    private static double GetInvalidPenalty(string text, RecognitionInputMode mode)
    {
        return mode switch
        {
            RecognitionInputMode.Number when text.Any(c => !IsNumberChar(c)) => 0.35,
            RecognitionInputMode.PhoneNumber when text.Any(c => !IsPhoneChar(c)) => 0.35,
            RecognitionInputMode.Email when text.Any(c => !IsEmailChar(c)) => 0.25,
            RecognitionInputMode.Amount when text.Any(c => !IsAmountChar(c)) => 0.30,
            RecognitionInputMode.Symbol when text.Any(c => !IsSymbolOrPunctuationChar(c)) => 0.25,
            RecognitionInputMode.Punctuation when !IsPunctuationText(text) => 0.40,
            RecognitionInputMode.EnglishText when text.Any(IsCjk) => 0.12,
            RecognitionInputMode.ChineseText when text.All(c => c < 128) => 0.08,
            _ => 0.0
        };
    }

    private static bool IsSingleCjk(string text) => text.Length == 1 && IsCjk(text[0]);
    private static bool IsTwoCjkCharacters(string text) => text.Length == 2 && text.All(IsCjk);
    private static bool IsCjk(char c) => c >= '\u4e00' && c <= '\u9fff';

    private static bool IsEnglishLike(char c)
    {
        return char.IsAsciiLetter(c) || char.IsAsciiDigit(c) || c is '\'' or '-' or '_' or '.' or ',' or '!' or '?' or ':' or ';';
    }

    private static bool IsNumberChar(char c) => char.IsDigit(c) || c is '.' or '-' or '+';
    private static bool IsPhoneChar(char c) => char.IsDigit(c) || c is '+' or '-' or ' ';
    private static bool IsEmailChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '@' or '.' or '_' or '-' or '+';
    private static bool IsAmountChar(char c) => char.IsDigit(c) || c is '.' or ',' or '-' or '+' or '￥' or '$' or '¥';
    private static bool IsSymbolOrPunctuationChar(char c) => IsPunctuationChar(c) || (!char.IsLetterOrDigit(c) && !IsCjk(c));
    private static bool IsPunctuationText(string text) => text.Length is >= 1 and <= 3 && text.All(IsPunctuationChar);

    private static bool IsPunctuationChar(char c)
    {
        return c is
            '，' or ',' or
            '。' or '.' or
            '、' or
            '！' or '!' or
            '？' or '?' or
            '：' or ':' or
            '；' or ';' or
            '“' or '”' or '"' or
            '‘' or '’' or '\'' or
            '（' or '）' or '(' or ')' or
            '【' or '】' or '[' or ']' or
            '《' or '》' or '<' or '>' or
            '—' or '-' or
            '…' or
            '·';
    }
}
