using ZenMLRace.Core.Models;

namespace ZenMLRace.Core.Services;

public interface IJobQueue
{
    ValueTask EnqueueAsync(PredictJob job, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PredictJob> DequeueAllAsync(CancellationToken cancellationToken = default);
}
