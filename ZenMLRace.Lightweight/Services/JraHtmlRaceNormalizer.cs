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

        var raceTable = FindRaceTable(raceCardDocument);
        var raceCard = ParseRaceCard(raceTable);
        var raceName = raceCard.Race.Name;
        var raceCardMarkdown = ConvertToMarkdown(raceCardDocument);
        var dataMarkdown = ConvertToMarkdown(dataDocument);
        var horses = MapHorseProfiles(raceCard.Entries);
        var insights = ExtractInsights(dataMarkdown);

        return new NormalizedRaceData(raceName, horses, insights, raceCardMarkdown, dataMarkdown, raceCard);
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

    private static IElement FindRaceTable(IDocument document)
    {
        return document.QuerySelector("#main_contents #race_syutsuba #syutsuba table.basic")
            ?? document.QuerySelector("#syutsuba table.basic")
            ?? document.QuerySelector("table.basic")
            ?? throw new InvalidOperationException("出馬表テーブル(table.basic)を検出できませんでした。HTML構造が変更されている可能性があります。");
    }

    private static RaceCard ParseRaceCard(IElement raceTable)
    {
        var raceInfo = ParseRaceInfo(raceTable);
        var entries = ParseRaceEntries(raceTable);
        return new RaceCard(raceInfo, entries);
    }

    private static RaceCardRaceInfo ParseRaceInfo(IElement raceTable)
    {
        var raceName = NormalizeText(raceTable.QuerySelector("caption .race_title .race_name")?.TextContent);
        if (string.IsNullOrWhiteSpace(raceName))
        {
            raceName = "Unknown Race";
        }

        var date = NormalizeText(raceTable.QuerySelector("caption .date_line .cell.date")?.TextContent);
        var course = NormalizeText(raceTable.QuerySelector("caption .type .cell.course")?.TextContent);
        var distance = NormalizeText(raceTable.QuerySelector("caption .type .cell.course .cap")?.ParentElement?.TextContent);
        var eligibility = NormalizeText(raceTable.QuerySelector("caption .type .cell.category")?.TextContent);
        var classification = NormalizeText(raceTable.QuerySelector("caption .type .cell.class")?.TextContent);
        var rule = NormalizeText(raceTable.QuerySelector("caption .type .cell.rule")?.TextContent);
        var weightRule = NormalizeText(raceTable.QuerySelector("caption .type .cell.weight")?.TextContent);

        return new RaceCardRaceInfo(
            raceName,
            NullIfEmpty(date),
            NullIfEmpty(course),
            NullIfEmpty(distance),
            NullIfEmpty(eligibility),
            NullIfEmpty(classification),
            NullIfEmpty(rule),
            NullIfEmpty(weightRule));
    }

    private static IReadOnlyList<RaceCardEntry> ParseRaceEntries(IElement raceTable)
    {
        var rows = raceTable.QuerySelectorAll("tbody > tr");
        var entries = new List<RaceCardEntry>();
        foreach (var row in rows)
        {
            var frameCell = row.QuerySelector("td.waku");
            var numberCell = row.QuerySelector("td.num");
            var horseCell = row.QuerySelector("td.horse");
            var jockeyCell = row.QuerySelector("td.jockey");
            if (frameCell is null || numberCell is null || horseCell is null || jockeyCell is null)
            {
                continue;
            }

            var horseName = NormalizeText(horseCell.QuerySelector(".name_line .name")?.TextContent);
            if (string.IsNullOrWhiteSpace(horseName))
            {
                continue;
            }

            var ageText = NormalizeText(jockeyCell.QuerySelector("p.age")?.TextContent);
            var (sex, age, coatColor) = ParseSexAge(ageText);
            var assignedWeight = ParseWeightKg(NormalizeText(jockeyCell.QuerySelector("p.weight")?.TextContent));
            var jockey = NormalizeText(jockeyCell.QuerySelector("p.jockey")?.TextContent);

            entries.Add(new RaceCardEntry(
                TryParseFrameNumber(frameCell) ?? 0,
                TryParseHorseNumber(numberCell) ?? 0,
                horseName,
                sex,
                age,
                coatColor,
                assignedWeight,
                NullIfEmpty(jockey),
                ParsePastRuns(row)));
        }

        return entries;
    }

    private static IReadOnlyList<RaceCardPastRun> ParsePastRuns(IElement row)
    {
        var runs = new List<RaceCardPastRun>(4);
        for (var i = 1; i <= 4; i++)
        {
            var pastCell = row.QuerySelector($"td.past.p{i}");
            if (pastCell is null)
            {
                continue;
            }

            var raceName = NullIfEmpty(NormalizeText(pastCell.QuerySelector(".race_line .name")?.TextContent));
            if (raceName is null)
            {
                continue;
            }

            runs.Add(new RaceCardPastRun(
                i,
                NullIfEmpty(NormalizeText(pastCell.QuerySelector(".date_line .date")?.TextContent)),
                NullIfEmpty(NormalizeText(pastCell.QuerySelector(".date_line .rc")?.TextContent)),
                raceName,
                TryParseFinish(NormalizeText(pastCell.QuerySelector(".place_line .place")?.TextContent)),
                TryParsePopularity(NormalizeText(pastCell.QuerySelector(".place_line .num .pop")?.TextContent))));
        }

        return runs;
    }

    private static IReadOnlyList<HorseProfile> MapHorseProfiles(IReadOnlyList<RaceCardEntry> entries)
    {
        var horses = new List<HorseProfile>(entries.Count);
        foreach (var entry in entries)
        {
            var firstPast = entry.PastRuns.OrderBy(static x => x.Index).FirstOrDefault();
            horses.Add(new HorseProfile(
                entry.FrameNumber,
                entry.HorseNumber,
                entry.HorseName,
                entry.Age,
                firstPast?.RaceName,
                firstPast?.FinishPosition ?? 0,
                firstPast?.Popularity ?? 0));
        }

        return horses;
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

    private static (string? Sex, int? Age, string? CoatColor) ParseSexAge(string? ageText)
    {
        if (string.IsNullOrWhiteSpace(ageText))
        {
            return (null, null, null);
        }

        var match = SexAgeColorRegex().Match(ageText);
        if (!match.Success)
        {
            return (null, null, NullIfEmpty(ageText));
        }

        var age = TryParseFirstInt(match.Groups["age"].Value);
        return (
            NullIfEmpty(match.Groups["sex"].Value),
            age,
            NullIfEmpty(match.Groups["color"].Value));
    }

    private static int? TryParseFrameNumber(IElement frameCell)
    {
        var alt = frameCell.QuerySelector("img")?.GetAttribute("alt") ?? string.Empty;
        return TryParseFirstInt(alt);
    }

    private static int? TryParseHorseNumber(IElement numberCell)
    {
        var numText = NormalizeText(numberCell.TextContent);
        return TryParseFirstInt(numText);
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

    private static int? TryParseFirstInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = NumberRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static int? TryParseFinish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = FinishRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static int? TryParsePopularity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = PopularityRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static double? ParseWeightKg(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = WeightRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static string? NullIfEmpty(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return Regex.Replace(normalized, @"\s+", " ");
    }

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"(\d+)着")]
    private static partial Regex FinishRegex();

    [GeneratedRegex(@"(\d+)番人気")]
    private static partial Regex PopularityRegex();

    [GeneratedRegex(@"([0-9]+(?:\.[0-9]+)?)\s*kg")]
    private static partial Regex WeightRegex();

    [GeneratedRegex(@"^(?<sex>[牡牝セ])(?<age>\d+)(?:/(?<color>.+))?$")]
    private static partial Regex SexAgeColorRegex();
}
