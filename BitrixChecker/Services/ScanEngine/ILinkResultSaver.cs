using BitrixChecker.Models;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>Saves link detection results to the database.</summary>
public interface ILinkResultSaver
{
    /// <summary>Saves a batch of link results, avoiding duplicates by subdomain.</summary>
    /// <param name="results">The link results to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of newly saved links.</returns>
    Task<int> SaveResultsAsync(IReadOnlyList<LinkResult> results, CancellationToken cancellationToken = default);
}