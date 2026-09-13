using BitrixChecker.Data;
using BitrixChecker.Models;
using Microsoft.EntityFrameworkCore;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>
/// Saves link detection results to the database, reactivating existing records or creating new ones.
/// </summary>
public sealed class LinkResultSaver(IServiceScopeFactory scopeFactory, ILogger<LinkResultSaver> logger) : ILinkResultSaver
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<LinkResultSaver> _logger = logger;

    public async Task<int> SaveResultsAsync(IReadOnlyList<LinkResult> results, CancellationToken cancellationToken = default)
    {
        if (results.Count == 0) return 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var uniqueResults = results
            .Where(x => !string.IsNullOrWhiteSpace(x.Subdomain))
            .GroupBy(x => x.Subdomain.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var domains = uniqueResults.Select(x => x.Subdomain).ToList();
        var existingLinks = await db.LinkResults
            .Where(x => domains.Contains(x.Subdomain))
            .ToListAsync(cancellationToken);

        var existingMap = existingLinks.ToDictionary(x => x.Subdomain, StringComparer.OrdinalIgnoreCase);
        var newLinks = new List<LinkResult>();
        int newlyActiveCount = 0;

        foreach (var result in uniqueResults)
        {
            if (existingMap.TryGetValue(result.Subdomain, out var existing))
            {
                if (existing.Status != LinkStatuses.Active || existing.IsDeleted)
                {
                    existing.Status = LinkStatuses.Active;
                    existing.HttpCode = result.HttpCode;
                    existing.ResponseFingerprint = result.ResponseFingerprint;
                    existing.LastChecked = DateTime.UtcNow;
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    if (result.ScanJobId.HasValue) existing.ScanJobId = result.ScanJobId;
                    newlyActiveCount++;
                }
                else
                {
                    existing.LastChecked = DateTime.UtcNow;
                    existing.HttpCode = result.HttpCode;
                    if (!string.IsNullOrEmpty(result.ResponseFingerprint))
                    {
                        existing.ResponseFingerprint = result.ResponseFingerprint;
                    }
                }
            }
            else
            {
                newLinks.Add(result);
                newlyActiveCount++;
            }
        }

        if (newLinks.Count > 0)
        {
            await db.LinkResults.AddRangeAsync(newLinks, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[SAVER] Đã lưu {NewCount} link mới, {ActiveCount} link active tổng cộng.", newLinks.Count, newlyActiveCount);

        return newlyActiveCount;
    }
}
