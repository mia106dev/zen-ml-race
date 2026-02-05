using ZenMLRace.Core.Entities;
using ZenMLRace.Core.Interfaces;

namespace ZenMLRace.Infrastructure.Scrapers;

public class PlaywrightRaceCollector : IRaceCollector
{
    public async Task<Race?> CollectRaceAsync(string raceId, CancellationToken cancellationToken = default)
    {
        // TODO: Playwright によるスクレイピングの実装
        await Task.Delay(1000, cancellationToken); // モック待機

        return new Race
        {
            Id = raceId,
            RaceName = "Test Race",
            RaceDate = DateTime.Now
        };
    }
}
