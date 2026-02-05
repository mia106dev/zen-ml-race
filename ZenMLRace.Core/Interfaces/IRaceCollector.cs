using ZenMLRace.Core.Entities;

namespace ZenMLRace.Core.Interfaces;

public interface IRaceCollector
{
    Task<Race?> CollectRaceAsync(string raceId, CancellationToken cancellationToken = default);
}
