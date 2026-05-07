using System.Text.Json;
using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

public sealed class JsonRacePredictionProfileLoader : IRacePredictionProfileLoader
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RacePredictionProfile LoadFromFile(string profilePath)
    {
        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<RacePredictionProfile>(json, jsonOptions)
            ?? throw new InvalidOperationException($"プロフィールのJSONを読み込めませんでした: {profilePath}");

        Validate(profile, profilePath);
        return profile;
    }

    private static void Validate(RacePredictionProfile profile, string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profile.RaceKey))
        {
            throw new InvalidOperationException($"raceKey は必須です: {profilePath}");
        }

        if (profile.TargetYear < 1900 || profile.TargetYear > 3000)
        {
            throw new InvalidOperationException($"targetYear が不正です: {profilePath}");
        }

        ValidateWeight(profile.Weights.PopularityWeight, nameof(profile.Weights.PopularityWeight), profilePath);
        ValidateWeight(profile.Weights.AgeWeight, nameof(profile.Weights.AgeWeight), profilePath);
        ValidateWeight(profile.Weights.FrameWeight, nameof(profile.Weights.FrameWeight), profilePath);
        ValidateWeight(profile.Weights.PreviousRaceWeight, nameof(profile.Weights.PreviousRaceWeight), profilePath);
        ValidateWeight(profile.Weights.WinnerProfileWeight, nameof(profile.Weights.WinnerProfileWeight), profilePath);
    }

    private static void ValidateWeight(double? weight, string fieldName, string profilePath)
    {
        if (weight.HasValue && weight.Value <= 0)
        {
            throw new InvalidOperationException($"{fieldName} は正の値を指定するか null を使用してください: {profilePath}");
        }
    }
}
