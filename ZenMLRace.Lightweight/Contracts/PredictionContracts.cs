namespace ZenMLRace.Lightweight.Contracts;

// TODO: be private
public sealed record RaceSourceDocuments(
    string RaceKey,
    string RaceCardHtml,
    string DataHtml);

// TODO: Coreのものと統合
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

public interface IPredictor
{
    PredictionResult Predict(PredictionRequest request);
}

public interface IRacePredictionProfileLoader
{
    RacePredictionProfile LoadFromFile(string profilePath);
}
