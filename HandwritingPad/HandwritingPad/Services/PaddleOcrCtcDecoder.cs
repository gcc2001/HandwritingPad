using HandwritingPad.Models;

namespace HandwritingPad.Services;

public sealed class PaddleOcrCtcDecoder
{
    private readonly string[] _characters;

    public PaddleOcrCtcDecoder(string dictionaryPath)
    {
        if (!File.Exists(dictionaryPath))
        {
            throw new FileNotFoundException("找不到 OCR 字典文件。", dictionaryPath);
        }

        var lines = File.ReadAllLines(dictionaryPath)
            .Select(x => x.TrimEnd('\r', '\n'))
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        _characters = new[] { string.Empty }.Concat(lines).ToArray();
    }

    public RecognitionCandidate DecodeGreedy(IReadOnlyList<float> output, int timeSteps, int classCount)
    {
        var result = new List<string>();
        var confidences = new List<float>();
        var lastIndex = -1;

        for (var t = 0; t < timeSteps; t++)
        {
            var offset = t * classCount;
            var maxIndex = 0;
            var maxValue = output[offset];

            for (var c = 1; c < classCount; c++)
            {
                var value = output[offset + c];
                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = c;
                }
            }

            var confidence = EstimateProbability(output, offset, classCount, maxIndex);
            var isBlank = maxIndex == 0;
            var isRepeat = maxIndex == lastIndex;

            if (!isBlank && !isRepeat && maxIndex < _characters.Length)
            {
                result.Add(_characters[maxIndex]);
                confidences.Add(confidence);
            }

            lastIndex = maxIndex;
        }

        var text = string.Concat(result).Trim();
        var score = confidences.Count == 0 ? 0.0 : confidences.Average();
        return new RecognitionCandidate(text, score);
    }

    public IReadOnlyList<RecognitionCandidate> DecodeBeamSearch(
        IReadOnlyList<float> output,
        int timeSteps,
        int classCount,
        int topK,
        int beamWidth,
        int maxCandidates)
    {
        var beams = new Dictionary<string, double> { [""] = 0.0 };

        for (var t = 0; t < timeSteps; t++)
        {
            var offset = t * classCount;
            var topClasses = GetTopK(output, offset, classCount, topK);
            var next = new Dictionary<string, double>();

            foreach (var beam in beams)
            {
                foreach (var item in topClasses)
                {
                    var nextText = beam.Key;

                    if (item.Index != 0 && item.Index < _characters.Length)
                    {
                        var ch = _characters[item.Index];
                        if (!EndsWithSameToken(nextText, ch))
                        {
                            nextText += ch;
                        }
                    }

                    var probability = Math.Max(item.Probability, 1e-8);
                    var nextScore = beam.Value + Math.Log(probability);

                    if (!next.TryGetValue(nextText, out var oldScore) || nextScore > oldScore)
                    {
                        next[nextText] = nextScore;
                    }
                }
            }

            beams = next
                .OrderByDescending(x => x.Value)
                .Take(beamWidth)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        return beams
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new RecognitionCandidate(x.Key.Trim(), Math.Exp(x.Value / Math.Max(1, timeSteps))))
            .OrderByDescending(x => x.Confidence)
            .Take(maxCandidates)
            .ToArray();
    }

    private static bool EndsWithSameToken(string text, string token)
    {
        return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(token) && text.EndsWith(token, StringComparison.Ordinal);
    }

    private static IReadOnlyList<ClassProbability> GetTopK(IReadOnlyList<float> output, int offset, int classCount, int topK)
    {
        var probabilities = new List<ClassProbability>(Math.Min(topK, classCount));
        var looksLikeProbability = LooksLikeProbabilityDistribution(output, offset, classCount);

        if (looksLikeProbability)
        {
            for (var i = 0; i < classCount; i++)
            {
                probabilities.Add(new ClassProbability(i, output[offset + i]));
            }
        }
        else
        {
            var maxLogit = float.NegativeInfinity;
            for (var i = 0; i < classCount; i++)
            {
                maxLogit = Math.Max(maxLogit, output[offset + i]);
            }

            var expValues = new double[classCount];
            var expSum = 0.0;
            for (var i = 0; i < classCount; i++)
            {
                var exp = Math.Exp(output[offset + i] - maxLogit);
                expValues[i] = exp;
                expSum += exp;
            }

            for (var i = 0; i < classCount; i++)
            {
                probabilities.Add(new ClassProbability(i, expValues[i] / expSum));
            }
        }

        return probabilities.OrderByDescending(x => x.Probability).Take(topK).ToArray();
    }

    private static float EstimateProbability(IReadOnlyList<float> output, int offset, int classCount, int maxIndex)
    {
        if (LooksLikeProbabilityDistribution(output, offset, classCount))
        {
            return output[offset + maxIndex];
        }

        var maxLogit = float.NegativeInfinity;
        for (var i = 0; i < classCount; i++)
        {
            maxLogit = Math.Max(maxLogit, output[offset + i]);
        }

        var expSum = 0.0;
        for (var i = 0; i < classCount; i++)
        {
            expSum += Math.Exp(output[offset + i] - maxLogit);
        }

        var exp = Math.Exp(output[offset + maxIndex] - maxLogit);
        return (float)(exp / expSum);
    }

    private static bool LooksLikeProbabilityDistribution(IReadOnlyList<float> output, int offset, int classCount)
    {
        var sum = 0.0f;
        for (var i = 0; i < classCount; i++)
        {
            var value = output[offset + i];
            if (value < 0f || value > 1f)
            {
                return false;
            }
            sum += value;
        }

        return sum > 0.9f && sum < 1.1f;
    }

    private sealed record ClassProbability(int Index, double Probability);
}
