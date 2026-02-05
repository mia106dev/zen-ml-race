using ZenMLRace.Core.Interfaces;
using ZenMLRace.Core.Services;

namespace ZenMLRace.Worker;

public class Worker(
    ILogger<Worker> logger,
    IJobQueue jobQueue,
    IRaceCollector raceCollector) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker started. Watching for jobs...");

        try
        {
            await foreach (var job in jobQueue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    logger.LogInformation("Processing job {JobId} for Race {RaceId} at {time}",
                        job.JobId, job.RaceId, DateTimeOffset.Now);

                    job.Status = "Processing";

                    // 1. スクレイピング
                    var race = await raceCollector.CollectRaceAsync(job.RaceId, stoppingToken);

                    if (race != null)
                    {
                        logger.LogInformation("Successfully collected data for race: {RaceName}", race.RaceName);
                        // TODO: DB への保存 (DbContext をスコープ付きで解決する必要あり)
                        // TODO: 推論
                    }
                    else
                    {
                        logger.LogWarning("Failed to collect data for race ID: {RaceId}", job.RaceId);
                    }

                    job.Status = "Completed";
                    logger.LogInformation("Job {JobId} completed.", job.JobId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing job {JobId}", job.JobId);
                    job.Status = "Failed";
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Worker is stopping due to cancellation.");
        }
    }
}
