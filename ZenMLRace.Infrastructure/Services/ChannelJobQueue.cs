using System.Threading.Channels;
using ZenMLRace.Core.Models;
using ZenMLRace.Core.Services;

namespace ZenMLRace.Infrastructure.Services;

public class ChannelJobQueue : IJobQueue
{
    private readonly Channel<PredictJob> channel;

    public ChannelJobQueue() =>
        // 開発初期なので容量無制限のチャネルを作成
        channel = Channel.CreateUnbounded<PredictJob>(new UnboundedChannelOptions
        {
            SingleReader = true, // Worker (BackgroundService) は通常 1 つ
            SingleWriter = false // API (Controller) からは複数から書き込まれる可能性がある
        });

    public async ValueTask EnqueueAsync(PredictJob job, CancellationToken cancellationToken = default)
        => await channel.Writer.WriteAsync(job, cancellationToken);

    public IAsyncEnumerable<PredictJob> DequeueAllAsync(CancellationToken cancellationToken = default)
        => channel.Reader.ReadAllAsync(cancellationToken);
}
