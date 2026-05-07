namespace ZenMLRace.Lightweight.Contracts;

public sealed record RaceSourceDocuments(
    string RaceKey,
    string RaceCardHtml,
    string DataHtml);

public sealed record HorseProfile(
    int FrameNumber,
    int HorseNumber,
    string Name,
    int? Age,
    string? LastRaceCategory,
    int? LastRaceFinishPosition,
    int? LastRacePopularity);

public sealed record DataInsight(
    string Key,
    string Evidence,
    double WeightHint);

public sealed record NormalizedRaceData(
    string RaceName,
    IReadOnlyList<HorseProfile> Horses,
    IReadOnlyList<DataInsight> Insights,
    string RaceCardMarkdown,
    string DataMarkdown);

public sealed record WeightProfile(
    double? PopularityWeight,
    double? AgeWeight,
    double? FrameWeight,
    double? PreviousRaceWeight,
    double? WinnerProfileWeight);

public sealed record RacePredictionProfile(
    string ProfileVersion,
    int TargetYear,
    string RaceKey,
    WeightProfile Weights,
    ScoringProfile Scoring);

public sealed record PredictionRequest(
    RaceSourceDocuments Source,
    RacePredictionProfile Profile);

public sealed record HorseScore(
    string Name,
    int HorseNumber,
    double Score,
    IReadOnlyList<string> Reasons);

public sealed record PredictionResult(
    string RaceKey,
    string RaceName,
    IReadOnlyList<HorseScore> Ranking,
    string Narrative,
    string WeightRationale);

public interface IHtmlRaceNormalizer
{
    NormalizedRaceData Normalize(RaceSourceDocuments source);
}

public interface IHorseScorer
{
    IReadOnlyList<HorseScore> Score(NormalizedRaceData normalizedData, WeightProfile weights, ScoringProfile scoringProfile);
}

public interface IInsightNarrator
{
    string BuildNarrative(PredictionResult result);
}

public interface IPredictionEngine
{
    PredictionResult Predict(PredictionRequest request);
}

public interface IRacePredictionProfileLoader
{
    RacePredictionProfile LoadFromFile(string profilePath);
}
