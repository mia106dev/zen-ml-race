using System.Text;
using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

public sealed class LightweightPredictionEngine(
    IHtmlRaceNormalizer normalizer,
    IHorseScorer horseScorer,
    IInsightNarrator narrator) : IPredictior
{
    public PredictionResult Predict(PredictionRequest request)
    {
        ValidateRequest(request);

        var normalizedData = normalizer.Normalize(request.Source);
        var ranking = horseScorer.Score(normalizedData, request.Profile.Weights, request.Profile.Scoring);

        var skeleton = new PredictionResult(
            request.Source.RaceKey,
            normalizedData.RaceName,
            ranking,
            string.Empty,
            DescribeWeights(request.Profile.Weights));

        var narrative = narrator.BuildNarrative(skeleton);
        return skeleton with { Narrative = narrative };
    }

    private static void ValidateRequest(PredictionRequest request)
    {
        if (!string.Equals(request.Source.RaceKey, request.Profile.RaceKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Source.RaceKey と Profile.RaceKey が一致していません。");
        }
    }

    private static string DescribeWeights(WeightProfile weights)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PopularityWeight: {DescribeWeight(weights.PopularityWeight)}");
        sb.AppendLine($"AgeWeight: {DescribeWeight(weights.AgeWeight)}");
        sb.AppendLine($"FrameWeight: {DescribeWeight(weights.FrameWeight)}");
        sb.AppendLine($"PreviousRaceWeight: {DescribeWeight(weights.PreviousRaceWeight)}");
        sb.AppendLine($"WinnerProfileWeight: {DescribeWeight(weights.WinnerProfileWeight)}");
        return sb.ToString().TrimEnd();
    }

    private static string DescribeWeight(double? value) => value.HasValue ? value.Value.ToString("0.##") : "none (neutral=1.0)";
}
