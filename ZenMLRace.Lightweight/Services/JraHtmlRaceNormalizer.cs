using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

public sealed partial class JraHtmlRaceNormalizer : IHtmlRaceNormalizer
{
    public NormalizedRaceData Normalize(RaceSourceDocuments source)
    {
        ValidateInputHtml(source.RaceCardHtml, nameof(source.RaceCardHtml));
        ValidateInputHtml(source.DataHtml, nameof(source.DataHtml));

        var parser = new HtmlParser();
        var raceCardDocument = parser.ParseDocument(source.RaceCardHtml);
        var dataDocument = parser.ParseDocument(source.DataHtml);

        var raceName = ReadRaceName(raceCardDocument) ?? "Unknown Race";
        var raceCardMarkdown = ConvertToMarkdown(raceCardDocument);
        var dataMarkdown = ConvertToMarkdown(dataDocument);

        var horses = ExtractHorseProfiles(raceCardDocument);
        var insights = ExtractInsights(dataMarkdown);

        return new NormalizedRaceData(raceName, horses, insights, raceCardMarkdown, dataMarkdown);
    }

    private static void ValidateInputHtml(string html, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new ArgumentException($"{fieldName} は空にできません。事前にHTMLを取得し、デコード済み文字列を渡してください。");
        }

        if (!html.Contains('<'))
        {
            throw new ArgumentException($"{fieldName} がHTML文字列として不正です。デコード処理を確認してください。");
        }
    }

    private static string? ReadRaceName(IDocument document)
    {
        var h1 = document.QuerySelector("h1");
        if (!string.IsNullOrWhiteSpace(h1?.TextContent))
        {
            return h1.TextContent.Trim();
        }

        return document.Title?.Trim();
    }

    private static string ConvertToMarkdown(IDocument document)
    {
        var body = document.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var element in body.QuerySelectorAll("h1, h2, h3, h4, p, li, table"))
        {
            switch (element.TagName.ToUpperInvariant())
            {
                case "H1":
                    AppendHeader(sb, 1, element.TextContent);
                    break;
                case "H2":
                    AppendHeader(sb, 2, element.TextContent);
                    break;
                case "H3":
                    AppendHeader(sb, 3, element.TextContent);
                    break;
                case "H4":
                    AppendHeader(sb, 4, element.TextContent);
                    break;
                case "P":
                    AppendParagraph(sb, element.TextContent);
                    break;
                case "LI":
                    AppendListItem(sb, element.TextContent);
                    break;
                case "TABLE":
                    AppendTable(sb, element);
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    private static void AppendHeader(StringBuilder sb, int level, string text)
    {
        var cleaned = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        sb.AppendLine($"{new string('#', level)} {cleaned}");
        sb.AppendLine();
    }

    private static void AppendParagraph(StringBuilder sb, string text)
    {
        var cleaned = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        sb.AppendLine(cleaned);
        sb.AppendLine();
    }

    private static void AppendListItem(StringBuilder sb, string text)
    {
        var cleaned = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        sb.AppendLine($"- {cleaned}");
    }

    private static void AppendTable(StringBuilder sb, IElement table)
    {
        var rows = table.QuerySelectorAll("tr");
        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("th, td")
                .Select(c => NormalizeText(c.TextContent))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToArray();

            if (cells.Length == 0)
            {
                continue;
            }

            sb.AppendLine($"| {string.Join(" | ", cells)} |");
        }

        sb.AppendLine();
    }

    private static IReadOnlyList<HorseProfile> ExtractHorseProfiles(IDocument raceCardDocument)
    {
        var rows = raceCardDocument.QuerySelectorAll("tr");
        var horses = new List<HorseProfile>();
        var fallbackHorseNumber = 1;

        foreach (var row in rows)
        {
            var frameCell = row.QuerySelector("td.waku");
            var numberCell = row.QuerySelector("td.num");
            var horseCell = row.QuerySelector("td.horse");

            if (frameCell is null || numberCell is null || horseCell is null)
            {
                continue;
            }

            var hasFrameNumber = TryExtractFrameNumber(frameCell, out var frameNumber);
            var hasHorseNumber = TryExtractHorseNumber(numberCell, out var horseNumber);

            var horseText = NormalizeText(horseCell.TextContent);
            var name = ExtractHorseName(horseText);
            if (string.Equals(name, "Unknown", StringComparison.Ordinal))
            {
                continue;
            }

            if (!hasHorseNumber)
            {
                horseNumber = fallbackHorseNumber;
            }

            if (!hasFrameNumber)
            {
                frameNumber = 0;
            }

            fallbackHorseNumber++;
            var age = ExtractAge(horseText);

            var pastCells = row.QuerySelectorAll("td.past");
            var raceSummary = NormalizeText(string.Join(" ", pastCells.Select(static x => x.TextContent)));

            horses.Add(new HorseProfile(
                frameNumber,
                horseNumber,
                name,
                age,
                ExtractLastRaceCategory(raceSummary),
                ExtractFirstNumber(raceSummary, "着"),
                ExtractFirstNumber(raceSummary, "番人気")));
        }

        return horses;
    }

    private static bool TryExtractFrameNumber(IElement frameCell, out int frameNumber)
    {
        frameNumber = 0;

        var alt = frameCell.QuerySelector("img")?.GetAttribute("alt") ?? string.Empty;
        var frameMatch = FrameNumberRegex().Match(alt);
        if (!frameMatch.Success)
        {
            return false;
        }

        return int.TryParse(frameMatch.Groups[1].Value, out frameNumber);
    }

    private static bool TryExtractHorseNumber(IElement numberCell, out int horseNumber)
    {
        horseNumber = 0;
        var numText = NormalizeText(numberCell.TextContent);
        var numMatch = HorseNumberRegex().Match(numText);
        if (!numMatch.Success)
        {
            return false;
        }

        return int.TryParse(numMatch.Groups[1].Value, out horseNumber);
    }

    private static IReadOnlyList<DataInsight> ExtractInsights(string dataMarkdown)
    {
        var insights = new List<DataInsight>();

        AddInsightIfContains(insights, dataMarkdown, "### 人気", "trend.popularity", 1.08);
        AddInsightIfContains(insights, dataMarkdown, "### 年齢", "trend.age", 1.08);
        AddInsightIfContains(insights, dataMarkdown, "### 前走", "trend.last_race", 1.10);

        // 枠の言及があるレースにも対応できるよう、キーワードベースで緩く検出する。
        AddInsightIfContains(insights, dataMarkdown, "枠", "trend.frame", 1.04);
        AddInsightIfContains(insights, dataMarkdown, "馬番", "trend.frame", 1.03);

        return insights;
    }

    private static void AddInsightIfContains(ICollection<DataInsight> insights, string markdown, string keyword, string key, double weightHint)
    {
        if (!markdown.Contains(keyword, StringComparison.Ordinal))
        {
            return;
        }

        insights.Add(new DataInsight(key, keyword, weightHint));
    }

    private static string ExtractHorseName(string text)
    {
        var withoutStats = Regex.Replace(text, @"\([\d\.\-]+\)", string.Empty);
        var horseName = NormalizeText(withoutStats)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(static token => !token.Contains('/') && token.Length >= 2);
        return string.IsNullOrWhiteSpace(horseName) ? "Unknown" : horseName;
    }

    private static int? ExtractAge(string text)
    {
        var match = AgeRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var age) ? age : null;
    }

    private static string? ExtractLastRaceCategory(string text)
    {
        if (text.Contains("香港", StringComparison.Ordinal) || text.Contains("海外", StringComparison.Ordinal))
        {
            return "Overseas";
        }

        if (text.Contains("シルクロード", StringComparison.Ordinal))
        {
            return "SilkRoad";
        }

        if (text.Contains("オーシャン", StringComparison.Ordinal))
        {
            return "Ocean";
        }

        if (text.Contains("阪急杯", StringComparison.Ordinal))
        {
            return "Hankyu";
        }

        return null;
    }

    private static int? ExtractFirstNumber(string text, string suffix)
    {
        var match = Regex.Match(text, $@"(\d+){Regex.Escape(suffix)}");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static string NormalizeText(string text)
    {
        var normalized = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return Regex.Replace(normalized, @"\s+", " ");
    }

    [GeneratedRegex(@"[牡牝セ](\d+)")]
    private static partial Regex AgeRegex();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex FrameNumberRegex();

    [GeneratedRegex(@"^(\d+)")]
    private static partial Regex HorseNumberRegex();
}
