using ZenMLRace.Lightweight.Contracts;
using ZenMLRace.Lightweight.Services;
using System.Text;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run --project ZenMLRace.Lightweight.Cli -- <raceKey> <profilePath>");
    return;
}

var raceKey = args[0];
var profilePath = args[1];

var fetcher = new JraFetcher(raceKey);
var documents = await fetcher.Fetch();

var profileLoader = new JsonRacePredictionProfileLoader();
var profile = profileLoader.LoadFromFile(profilePath);

var request = new PredictionRequest(
    documents,
    profile);

var engine = new LightweightPredictionEngine(
    new JraHtmlRaceNormalizer(),
    new RuleBasedHorseScorer(),
    new DeterministicInsightNarrator());

var result = engine.Predict(request);

Console.WriteLine($"RaceKey: {result.RaceKey}");
Console.WriteLine($"RaceName: {result.RaceName}");
Console.WriteLine("Top 5:");
foreach (var horse in result.Ranking.Take(5))
{
    Console.WriteLine($"- #{horse.HorseNumber} {horse.Name}: {horse.Score:F2}");
}

Console.WriteLine();
Console.WriteLine("Narrative:");
Console.WriteLine(result.Narrative);
Console.WriteLine();
Console.WriteLine("Weights:");
Console.WriteLine(result.WeightRationale);
