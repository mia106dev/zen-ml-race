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

var baseUrl = "https://www.jra.go.jp/keiba/g1/";
var raceCardUrl = $"{baseUrl}{raceKey}/syutsuba.html";
var dataUrl = $"{baseUrl}{raceKey}/data.html";

using var httpClient = new HttpClient();

// NEEDS? Just Manner?
//httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZenMLRace-Lightweight-Test/1.0");

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var sjis = Encoding.GetEncoding("shift_jis");

var raceCardHtml = await FetchHtmlAsync(httpClient, raceCardUrl, sjis);
var dataHtml = await FetchHtmlAsync(httpClient, dataUrl, sjis);

var profileLoader = new JsonRacePredictionProfileLoader();
var profile = profileLoader.LoadFromFile(profilePath);

var engine = new LightweightPredictionEngine(
    new JraHtmlRaceNormalizer(),
    new RuleBasedHorseScorer(),
    new DeterministicInsightNarrator());

var request = new PredictionRequest(
    new RaceSourceDocuments(raceKey, raceCardHtml, dataHtml),
    profile);

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

static async Task<string> FetchHtmlAsync(HttpClient httpClient, string url, Encoding? encoding)
{
    using var response = await httpClient.GetAsync(url);
    response.EnsureSuccessStatusCode();

    var bytes = await response.Content.ReadAsByteArrayAsync();
    return (encoding ?? Encoding.UTF8).GetString(bytes);
}
