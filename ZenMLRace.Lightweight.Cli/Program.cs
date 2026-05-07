using ZenMLRace.Lightweight.Contracts;
using ZenMLRace.Lightweight.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run --project ZenMLRace.Lightweight.Cli -- <raceKey> <profilePath> [scorerKey]");
    Console.WriteLine("scorerKey: standard | pop-suppressed");
    return;
}

var raceKey = args[0];
var profilePath = $"{args[1]}\\profile-2026-{raceKey}.json";
const int TopN = 5;
var scorerKey = args.Length >= 3 ? args[2] : "standard";

var fetcher = new JraHtmlFetcher(raceKey);
var documents = await fetcher.Fetch();

var profileLoader = new JsonRacePredictionProfileLoader();
var profile = profileLoader.LoadFromFile(profilePath);

var request = new PredictionRequest(
    documents,
    profile);

var scorer = ResolveScorer(scorerKey);
var engine = new LightweightPredictionEngine(
    new JraHtmlRaceNormalizer(),
    scorer,
    new DeterministicInsightNarrator());

var result = engine.Predict(request);
var reasonValueRegex = new Regex(@"([+\-]\d+\.\d+)", RegexOptions.Compiled);

var report = new StringBuilder();
report.AppendLine($"RaceKey: {result.RaceKey}");
report.AppendLine($"RaceName: {result.RaceName}");
report.AppendLine($"Scorer: {scorerKey}");
report.AppendLine($"Total Horses: {result.Ranking.Count}");

if (result.Ranking.Count > 0)
{
    var max = result.Ranking.Max(static x => x.Score);
    var min = result.Ranking.Min(static x => x.Score);
    var avg = result.Ranking.Average(static x => x.Score);
    report.AppendLine($"Score Range: {min:F2} .. {max:F2} / Avg: {avg:F2}");
}

report.AppendLine();
report.AppendLine($"Core Picks (Top {Math.Min(TopN, result.Ranking.Count)}):");
foreach (var (horse, rank) in result.Ranking.Take(TopN).Select((horse, index) => (horse, index + 1)))
{
    report.AppendLine($"[{rank}] #{horse.HorseNumber} {horse.Name}  Score: {horse.Score:F2}");

    var positive = 0.0;
    var negative = 0.0;
    foreach (var reason in horse.Reasons)
    {
        var match = reasonValueRegex.Match(reason);
        if (match.Success
            && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            if (value >= 0)
            {
                positive += value;
            }
            else
            {
                negative += value;
            }
        }

        report.AppendLine($"    - {reason}");
    }

    if (horse.Reasons.Count == 0)
    {
        report.AppendLine("    - reasons: (none)");
    }

    report.AppendLine($"    => Positive: {positive:+0.00;-0.00} / Negative: {negative:+0.00;-0.00}");
}

report.AppendLine();
var watchlistCandidates = result.Ranking
    .Skip(TopN)
    .Take(5)
    .Select(static horse => new
    {
        Horse = horse,
        ReasonStrength = horse.Reasons.Sum(ParseReasonValue)
    })
    .OrderByDescending(static x => x.ReasonStrength)
    .ToArray();

report.AppendLine($"Watchlist (Ranks {TopN + 1}-{Math.Min(TopN + 5, result.Ranking.Count)}):");
if (watchlistCandidates.Length == 0)
{
    report.AppendLine("- (none)");
}
else
{
    foreach (var entry in watchlistCandidates)
    {
        report.AppendLine(
            $"- #{entry.Horse.HorseNumber} {entry.Horse.Name}  Score: {entry.Horse.Score:F2}  ReasonStrength: {entry.ReasonStrength:+0.00;-0.00}");
    }
}

report.AppendLine();
report.AppendLine("Full Ranking (Score only):");
foreach (var (horse, rank) in result.Ranking.Select((horse, index) => (horse, index + 1)))
{
    report.AppendLine($"[{rank,2}] #{horse.HorseNumber,2} {horse.Name,-16} {horse.Score,6:F2}");
}

report.AppendLine();
report.AppendLine("Narrative:");
report.AppendLine(result.Narrative);
report.AppendLine();
report.AppendLine("Weights:");
report.AppendLine(result.WeightRationale);

Console.WriteLine(report.ToString());

static double ParseReasonValue(string reason)
{
    var match = Regex.Match(reason, @"([+\-]\d+\.\d+)");
    if (!match.Success)
    {
        return 0.0;
    }

    return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        ? value
        : 0.0;
}

static IHorseScorer ResolveScorer(string scorerKey) => scorerKey.ToLowerInvariant() switch
{
    "standard" => new RuleBasedHorseScorer(),
    "pop-suppressed" => new PopularitySuppressedHorseScorer(),
    _ => throw new ArgumentException(
        $"Unknown scorerKey: {scorerKey}. Available: standard, pop-suppressed")
};
