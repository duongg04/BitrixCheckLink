using BitrixChecker.Models;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>
/// Manages ScanJob records for tracking scan progress in the database.
/// </summary>
public interface IScanJobService
{
    /// <summary>
    /// Creates a new scan job record.
    /// </summary>
    Task<ScanJob> CreateJobAsync(string name, string jobType, int totalExpected, string? createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or updates the total expected count for a job.
    /// </summary>
    Task SetTotalExpectedAsync(int jobId, int totalExpected, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the scanned count for a job and marks completed when reached.
    /// </summary>
    Task IncrementScannedAsync(int jobId, int increment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a job as started.
    /// </summary>
    Task MarkStartedAsync(int jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a job as completed.
    /// </summary>
    Task MarkCompletedAsync(int jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a job as failed with an error message.
    /// </summary>
    Task MarkFailedAsync(int jobId, string? error, CancellationToken cancellationToken = default);
}
