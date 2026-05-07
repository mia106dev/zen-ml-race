using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

public sealed partial class JraHtmlRaceNormalizer : IHtmlRaceNormalizer
{
    public NormalizedRaceData Normalize(RaceSourceDocuments source)
    {
        var configuration = Configuration.Default;
        var context = BrowsingContext.New(configuration);

        var raceCardDocument = context.OpenAsync(req => req.Content(source.RaceCardHtml)).GetAwaiter().GetResult();
        var dataDocument = context.OpenAsync(req => req.Content(source.DataHtml)).GetAwaiter().GetResult();

        var raceName = ReadRaceName(raceCardDocument) ?? "Unknown Race";
        var raceCardMarkdown = ConvertToMarkdown(raceCardDocument);
        var dataMarkdown = ConvertToMarkdown(dataDocument);

        var horses = ExtractHorseProfiles(raceCardDocument);
        var insights = ExtractInsights(dataMarkdown);

        return new NormalizedRaceData(raceName, horses, insights, raceCardMarkdown, dataMarkdown);
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

        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td").Select(c => NormalizeText(c.TextContent)).ToArray();
            if (cells.Length < 3)
            {
                continue;
            }

            if (!int.TryParse(cells[0], out var frameNumber) || !int.TryParse(cells[1], out var horseNumber))
            {
                continue;
            }

            var horseCell = cells[2];
            var name = ExtractHorseName(horseCell);
            var age = ExtractAge(horseCell);
            var raceSummary = NormalizeText(string.Join(" ", cells.Skip(4)));

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
        var horseName = NormalizeText(withoutStats).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
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
}
