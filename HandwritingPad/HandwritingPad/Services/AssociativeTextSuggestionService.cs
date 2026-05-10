using HandwritingPad.Models;

namespace HandwritingPad.Services;

public sealed class AssociativeTextSuggestionService
{
    private readonly Dictionary<string, string[]> _nextTextMap = new(StringComparer.Ordinal)
    {
        ["测"] = ["试", "量", "绘"],
        ["试"] = ["验", "用", "试"],
        ["开"] = ["发", "始", "启"],
        ["发"] = ["送", "布", "现"],
        ["你"] = ["好", "们"],
        ["好"] = ["的", "了", "吗"],
        ["我"] = ["们", "的", "是"],
        ["需"] = ["要", "求"],
        ["要"] = ["求", "是", "的"],
        ["用"] = ["户", "例", "法"],
        ["户"] = ["名"],
        ["识"] = ["别"],
        ["别"] = ["率", "人"],
        ["准"] = ["确", "备"],
        ["确"] = ["认", "定", "实"],
        ["输"] = ["入", "出"],
        ["入"] = ["法", "口"],
        ["文"] = ["字", "件", "本"],
        ["字"] = ["符", "段"],
        ["模"] = ["型", "式", "块"],
        ["型"] = ["号"],
        ["数"] = ["据", "字", "量"],
        ["据"] = ["库"],
        ["程"] = ["序", "度"],
        ["序"] = ["列", "号"],
        ["功"] = ["能"],
        ["能"] = ["力", "够"],
        ["配"] = ["置", "合"],
        ["置"] = ["换"],
        ["推"] = ["理", "荐"],
        ["理"] = ["解", "论"],
        ["速"] = ["度", "率"],
        ["度"] = ["量"],
        ["错"] = ["误", "别"],
        ["误"] = ["差"],
        ["完"] = ["成", "善"],
        ["成"] = ["功", "本"],
        ["清"] = ["空", "除", "晰"],
        ["空"] = ["格"],
        ["点"] = ["击", "选"],
        ["击"] = ["后"],
        ["选"] = ["择", "项"],
        ["择"] = ["一"],
        ["候"] = ["选"],
        ["联"] = ["想", "系"],
        ["想"] = ["法", "要"],
        ["剪"] = ["贴"],
        ["贴"] = ["板", "上"],
        ["板"] = ["书"],
        ["手"] = ["写", "动"],
        ["写"] = ["入", "字"],
        ["动"] = ["作", "态", "词"],
        ["作"] = ["为", "用"],
        ["为"] = ["了", "主"],
        ["中"] = ["文", "止", "间"],
        ["止"] = ["推", "损"],
        ["重"] = ["新", "试"],
        ["新"] = ["建", "增", "版"],
        ["版"] = ["本"],
        ["本"] = ["次", "地", "文"],
        ["次"] = ["推", "识"],
        ["时"] = ["长", "间"],
        ["长"] = ["度", "按"],
        ["后"] = ["台", "续"],
        ["台"] = ["中"],
        ["系"] = ["统"],
        ["统"] = ["一", "计"],
        ["移"] = ["动", "除"],
        ["除"] = ["了", "非"],
        ["增"] = ["加", "强"],
        ["加"] = ["速", "入"],
        ["高"] = ["精", "级"],
        ["精"] = ["准", "度"],
        ["快"] = ["速", "捷"],
        ["保"] = ["存", "留"],
        ["存"] = ["储", "在"],
        ["管"] = ["理"],
        ["显"] = ["示", "卡"],
        ["卡"] = ["顿", "片"],
        ["词"] = ["语", "库"],
        ["语"] = ["句", "言"],
        ["言"] = ["文"],
        ["项"] = ["目"],
        ["目"] = ["标", "录"],
        ["标"] = ["点", "准", "题"],
        ["题"] = ["目"],
        ["路"] = ["径"],
        ["径"] = ["向"],
        ["构"] = ["建", "造"],
        ["建"] = ["议", "立"],
        ["议"] = ["题"],
        ["测 试"] = ["用例"],
        ["用户"] = ["输入", "手写", "点击"],
        ["识别"] = ["结果", "准确率", "模型"],
        ["输入"] = ["法", "模式", "文字"],
        ["推理"] = ["时长", "速度", "后端"],
        ["手写"] = ["文字", "输入", "板"],
        ["剪贴"] = ["板"],
        ["模型"] = ["切换", "下载", "推理"]
    };

    public IReadOnlyList<RecognitionCandidate> GetSuggestions(string committedText)
    {
        var key = Normalize(committedText);
        if (string.IsNullOrEmpty(key))
        {
            return Array.Empty<RecognitionCandidate>();
        }

        var keys = new List<string> { key };
        if (key.Length > 1)
        {
            keys.Add(key[^1].ToString());
            keys.Add(key[^Math.Min(2, key.Length)..]);
        }

        var suggestions = new List<RecognitionCandidate>();

        foreach (var candidateKey in keys.Distinct())
        {
            if (_nextTextMap.TryGetValue(candidateKey, out var values))
            {
                for (var i = 0; i < values.Length; i++)
                {
                    suggestions.Add(new RecognitionCandidate(values[i], 0.90 - i * 0.06));
                }
            }
        }

        return suggestions
            .GroupBy(x => x.Text)
            .Select(x => x.OrderByDescending(c => c.Confidence).First())
            .OrderByDescending(x => x.Confidence)
            .Take(8)
            .ToArray();
    }

    private static string Normalize(string text)
    {
        return text
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\t", "")
            .Trim();
    }
}
