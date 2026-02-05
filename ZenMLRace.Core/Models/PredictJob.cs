namespace ZenMLRace.Core.Models;

public record PredictJob(string JobId, string RaceId, DateTime CreatedAt)
{
    public string Status { get; set; } = "Pending";
}
