using BitrixChecker.Data;
using BitrixChecker.Models;
using Microsoft.EntityFrameworkCore;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>
/// Manages ScanJob records for tracking scan progress in the database.
/// </summary>
public sealed class ScanJobService(IServiceScopeFactory scopeFactory, ILogger<ScanJobService> logger) : IScanJobService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ScanJobService> _logger = logger;

    public async Task<ScanJob> CreateJobAsync(string name, string jobType, int totalExpected, string? createdByUserId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = new ScanJob
        {
            Name = name,
            JobType = jobType,
            Status = "Pending",
            TotalExpected = totalExpected,
            TotalScanned = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };

        db.ScanJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[SCANJOB] Tạo job #{Id}: {Name} ({JobType}), expected={Total}", job.Id, job.Name, job.JobType, job.TotalExpected);
        return job;
    }

    public async Task SetTotalExpectedAsync(int jobId, int totalExpected, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.ScanJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null) return;

        job.TotalExpected = totalExpected;
        if (job.TotalExpected > 0 && job.TotalScanned >= job.TotalExpected)
        {
            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task IncrementScannedAsync(int jobId, int increment, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Atomic in-database increment: several CheckBatchJob batches run concurrently and a
        // read-modify-write here would lose increments (last writer wins), leaving the job
        // stuck in "Running" because TotalScanned never reaches TotalExpected.
        await db.ScanJobs
            .Where(x => x.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.TotalScanned, j => j.TotalScanned + increment), cancellationToken);

        var job = await db.ScanJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null) return;

        if (job.TotalExpected > 0 && job.TotalScanned >= job.TotalExpected && job.Status != "Completed")
        {
            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkStartedAsync(int jobId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.ScanJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null) return;

        job.Status = "Running";
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(int jobId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.ScanJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null) return;

        job.Status = "Completed";
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(int jobId, string? error, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.ScanJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null) return;

        job.Status = "Failed";
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogError("[SCANJOB] Job #{Id} thất bại: {Error}", jobId, error);
    }
}
