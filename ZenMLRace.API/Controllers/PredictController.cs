using Microsoft.AspNetCore.Mvc;
using ZenMLRace.Core.Models;
using ZenMLRace.Core.Services;

namespace ZenMLRace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictController(IJobQueue jobQueue) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Predict([FromBody] PredictRequest request)
    {
        if (string.IsNullOrEmpty(request.RaceId))
        {
            return BadRequest("RaceId is required.");
        }

        var jobId = Guid.NewGuid().ToString();
        var job = new PredictJob(jobId, request.RaceId, DateTime.UtcNow);

        await jobQueue.EnqueueAsync(job);

        return Accepted(new { JobId = jobId, Status = job.Status });
    }
}

public record PredictRequest(string RaceId);
