using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bit.Fabric.Server.Api.Infrastructure.HealthChecks;

/// <summary>
/// Reports whether the server's backup job keeps finishing (deploy/backup/backup.sh), which it marks in the locked bucket
/// once both the database and the files are copied. The job runs every six hours, so a mark older than seven is a missed run.
/// </summary>
public class BackupHealthCheck(IAmazonS3 s3, string bucketName) : IHealthCheck
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(7);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var finishedRuns = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucketName, Prefix = "finished/" }, cancellationToken);
        var lastFinished = finishedRuns.S3Objects?.Max(run => run.LastModified)?.ToUniversalTime();

        return lastFinished > DateTime.UtcNow - MaximumAge
            ? HealthCheckResult.Healthy($"The last backup finished at {lastFinished:u}")
            : new HealthCheckResult(context.Registration.FailureStatus, lastFinished is null ? "No backup has finished yet" : $"The last backup finished at {lastFinished:u}, too long ago");
    }
}
