using System.Text.Json;
using HandwritingPad.Models;

namespace HandwritingPad.Services;

public sealed class CjkComponentCorrector
{
    private readonly IReadOnlyList<ComponentCorrectionRule> _rules;

    public CjkComponentCorrector()
    {
        _rules = LoadRules();
    }

    public IReadOnlyList<RecognitionCandidate> CreateCorrectionCandidates(
        IReadOnlyList<RecognitionCandidate> candidates,
        InkLayoutInfo layout,
        RecognitionInputMode inputMode)
    {
        if (!layout.IsLikelySingleCjkCharacter)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        if (inputMode is RecognitionInputMode.Number or RecognitionInputMode.Email or RecognitionInputMode.PhoneNumber or RecognitionInputMode.Amount or RecognitionInputMode.Symbol or RecognitionInputMode.Punctuation)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        var result = new List<RecognitionCandidate>();

        foreach (var candidate in candidates)
        {
            var text = Normalize(candidate.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (var rule in _rules)
            {
                if (string.Equals(text, rule.Input, StringComparison.Ordinal))
                {
                    var confidence = Math.Clamp(candidate.Confidence + rule.Bonus, 0.0, 1.0);
                    result.Add(new RecognitionCandidate(rule.Output, confidence));
                }
            }
        }

        return result
            .GroupBy(x => x.Text)
            .Select(x => x.OrderByDescending(c => c.Confidence).First())
            .OrderByDescending(x => x.Confidence)
            .Take(6)
            .ToArray();
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace(" ", "").Trim();
    }

    private static IReadOnlyList<ComponentCorrectionRule> LoadRules()
    {
        var rules = new List<ComponentCorrectionRule>
        {
            new("云力", "动", 0.32),
            new("云 力", "动", 0.32),
            new("雲力", "动", 0.18),
            new("日月", "明", 0.28),
            new("木木", "林", 0.28),
            new("口马", "吗", 0.26),
            new("口巴", "吧", 0.24),
            new("女也", "她", 0.25),
            new("亻尔", "你", 0.25),
            new("言吾", "语", 0.22),
            new("讠吾", "语", 0.24),
            new("土也", "地", 0.24),
            new("王里", "理", 0.22),
            new("氵青", "清", 0.22),
            new("氵每", "海", 0.22),
            new("扌巴", "把", 0.22),
            new("扌丁", "打", 0.22),
            new("忄青", "情", 0.22),
            new("忄生", "性", 0.22),
            new("月生", "胜", 0.20),
            new("禾火", "秋", 0.20)
        };

        var externalPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Rules", "cjk_component_corrections.json");
        if (!File.Exists(externalPath))
        {
            return rules;
        }

        try
        {
            var json = File.ReadAllText(externalPath);
            var externalRules = JsonSerializer.Deserialize<List<ComponentCorrectionRule>>(json);
            if (externalRules is not null)
            {
                rules.AddRange(externalRules);
            }
        }
        catch
        {
            // Ignore invalid external rule file.
        }

        return rules
            .GroupBy(x => x.Input + "\u0000" + x.Output)
            .Select(x => x.OrderByDescending(r => r.Bonus).First())
            .ToArray();
    }

    public sealed record ComponentCorrectionRule(string Input, string Output, double Bonus);
}
