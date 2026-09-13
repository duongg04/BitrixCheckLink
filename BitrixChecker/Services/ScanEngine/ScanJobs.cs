using System.Collections.Concurrent;
using BitrixChecker.Configuration;
using BitrixChecker.Data;
using BitrixChecker.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>
/// Hangfire background jobs for the smart scanning engine.
/// Jobs: GenerateScanJob, CheckBatchJob, RecheckJob, NotificationJob.
/// </summary>
public sealed class ScanJobs
{
    private readonly ISubdomainGenerator _generator;
    private readonly ILinkDetector _detector;
    private readonly ILinkResultSaver _saver;
    private readonly INotificationService _notificationService;
    private readonly IScanJobService _scanJobService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScanOptions _scanOptions;
    private readonly ILogger<ScanJobs> _logger;

    public ScanJobs(
        ISubdomainGenerator generator,
        ILinkDetector detector,
        ILinkResultSaver saver,
        INotificationService notificationService,
        IScanJobService scanJobService,
        IServiceScopeFactory scopeFactory,
        IOptions<ScanOptions> scanOptions,
        ILogger<ScanJobs> logger)
    {
        _generator = generator;
        _detector = detector;
        _saver = saver;
        _notificationService = notificationService;
        _scanJobService = scanJobService;
        _scopeFactory = scopeFactory;
        _scanOptions = scanOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// GenerateScanJob: Generates candidate subdomains (random + wordlist), creates a ScanJob record,
    /// and enqueues CheckBatchJob for each batch.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [JobDisplayName("GenerateScanJob: Sinh subdomain ({0} - {1})")]
    public Task GenerateScanJob(int minLength, int maxLength, IReadOnlyList<string>? wordlist, bool useWordlist, string? createdByUserId)
        => GenerateScanJob(minLength, maxLength, wordlist, useWordlist, createdByUserId, 100_000);

    [AutomaticRetry(Attempts = 0)]
    [JobDisplayName("GenerateScanJob: Sinh subdomain ({0} - {1}, max: {5})")]
    public async Task GenerateScanJob(int minLength, int maxLength, IReadOnlyList<string>? wordlist, bool useWordlist, string? createdByUserId, int maxCandidates)
    {
        var targetDomain = _scanOptions.TargetBaseDomain;
        var batchSize = _scanOptions.BatchSize;

        // Create ScanJob record for tracking
        var job = await _scanJobService.CreateJobAsync(
            $"Scan {targetDomain} ({minLength}-{maxLength})",
            "Manual",
            0, // will be updated as we count
            createdByUserId);

        await _scanJobService.MarkStartedAsync(job.Id);

        var batch = new List<string>(batchSize);
        var totalCount = 0;

        await foreach (var subdomain in _generator.GenerateAsync(minLength, maxLength, wordlist, useWordlist, maxCandidates))
        {
            while (ScanEngineState.IsSystemPaused) await Task.Delay(1000);

            batch.Add(subdomain);
            totalCount++;

            if (batch.Count >= batchSize)
            {
                var batchCopy = new List<string>(batch);
                BackgroundJob.Enqueue<ScanJobs>(x => x.CheckBatchJob(batchCopy, job.Id, targetDomain));
                batch.Clear();
                await Task.Delay(50);
            }
        }

        if (batch.Count > 0)
        {
            var batchCopy = new List<string>(batch);
            BackgroundJob.Enqueue<ScanJobs>(x => x.CheckBatchJob(batchCopy, job.Id, targetDomain));
        }

        // Record exact candidate count so the job auto-completes when all batches are scanned.
        await _scanJobService.SetTotalExpectedAsync(job.Id, totalCount);
        _logger.LogInformation("[GENERATE] Đã sinh {Count} subdomain cho job #{JobId}.", totalCount, job.Id);
    }

    /// <summary>
    /// CheckBatchJob: Checks a batch of subdomains using the LinkDetector and saves results.
    /// </summary>
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    [JobDisplayName("CheckBatchJob: Kiểm tra ({0} links)")]
    public async Task CheckBatchJob(List<string> subdomains, int scanJobId, string targetDomain)
    {
        while (ScanEngineState.IsSystemPaused) await Task.Delay(1000);

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _scanOptions.Parallelism };
        var activeLinks = new ConcurrentBag<LinkResult>();
        var scannedCount = 0;

        await Parallel.ForEachAsync(subdomains, parallelOptions, async (sub, cancellationToken) =>
        {
            // Wait instead of skipping: skipping would leave TotalScanned < TotalExpected
            // (job stuck "Running" forever) and those subdomains would never be checked.
            while (ScanEngineState.IsSystemPaused) await Task.Delay(1000);

            Interlocked.Increment(ref scannedCount);

            try
            {
                var result = await _detector.DetectAsync(sub, targetDomain, cancellationToken);
                if (result.IsActive)
                {
                    activeLinks.Add(new LinkResult
                    {
                        Subdomain = sub,
                        FullUrl = $"https://{sub}.{targetDomain}",
                        Status = LinkStatuses.Active,
                        HttpCode = result.HttpCode,
                        ResponseFingerprint = result.Fingerprint,
                        ScanJobId = scanJobId
                    });
                    _logger.LogInformation("✅ [TÌM THẤY] {Sub}.{Domain} (Code: {Code})", sub, targetDomain, result.HttpCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[CHECK] Lỗi khi kiểm tra {Sub}: {Message}", sub, ex.Message);
            }
        });

        // Save active links
        var newLinks = activeLinks.ToList();
        if (newLinks.Count > 0)
        {
            var saved = await _saver.SaveResultsAsync(newLinks);
            if (saved > 0)
            {
                // Enqueue NotificationJob for newly discovered links
                var newActive = newLinks.Take(saved).ToList();
                BackgroundJob.Enqueue<ScanJobs>(x => x.NotificationJob(newActive));
            }
        }

        // Update scan job progress
        await _scanJobService.IncrementScannedAsync(scanJobId, scannedCount);
    }

    /// <summary>
    /// RecheckJob: Re-checks all active links periodically. Moves inactive links to INACTIVE status.
    /// </summary>
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    [JobDisplayName("RecheckJob: Re-check active links")]
    public async Task RecheckJob()
    {
        _logger.LogInformation("[RECHECK] Bat dau re-check cac link active.");

        // Create ScanJob record for tracking this re-check
        var job = await _scanJobService.CreateJobAsync(
            $"Recheck {_scanOptions.TargetBaseDomain}",
            "Recheck",
            0,
            null);
        await _scanJobService.MarkStartedAsync(job.Id);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activeLinks = await db.LinkResults
            .Where(x => !x.IsDeleted && x.Status == LinkStatuses.Active)
            .ToListAsync();

        _logger.LogInformation("[RECHECK] Tìm thấy {Count} link active cần re-check.", activeLinks.Count);

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _scanOptions.Parallelism };
        var stillActive = new ConcurrentBag<LinkResult>();
        var nowInactive = new ConcurrentBag<LinkResult>();

        await Parallel.ForEachAsync(activeLinks, parallelOptions, async (link, cancellationToken) =>
        {
            if (ScanEngineState.IsSystemPaused) return;

            try
            {
                var result = await _detector.DetectAsync(link.Subdomain, _scanOptions.TargetBaseDomain, cancellationToken);
                link.LastChecked = DateTime.UtcNow;
                if (result.IsActive)
                {
                    link.HttpCode = result.HttpCode;
                    link.ResponseFingerprint = result.Fingerprint;
                    stillActive.Add(link);
                }
                else
                {
                    link.Status = LinkStatuses.Inactive;
                    link.HttpCode = result.HttpCode;
                    nowInactive.Add(link);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[RECHECK] Lỗi khi re-check {Sub}: {Message}", link.Subdomain, ex.Message);
            }
        });

        await db.SaveChangesAsync();
        _logger.LogInformation("[RECHECK] Complete: {Active} still active, {Inactive} moved to inactive.",
            stillActive.Count, nowInactive.Count);

        // Notify when links go inactive
        var inactiveResults = nowInactive.ToList();
        if (inactiveResults.Count > 0)
        {
            await _notificationService.NotifyInactiveLinksAsync(inactiveResults);
        }

        await _scanJobService.MarkCompletedAsync(job.Id);
    }

        /// <summary>
    /// NotificationJob: Sends notifications for newly discovered active links.
    /// </summary>
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        [JobDisplayName("NotificationJob: Notify {0} new links")]
    public async Task NotificationJob(List<LinkResult> newLinks)
    {
        await _notificationService.NotifyNewActiveLinksAsync(newLinks);
    }

        /// <summary>
        /// CheckSingleLink: Manually re-checks a single link by subdomain and updates the database.
        /// </summary>
        [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        [JobDisplayName("CheckSingleLink: Re-check {0}")]
        public async Task CheckSingleLink(string subdomain, string targetDomain, int linkResultId)
        {
            var result = await _detector.DetectAsync(subdomain, targetDomain);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var link = await db.LinkResults.FirstOrDefaultAsync(x => x.Id == linkResultId && !x.IsDeleted);
            if (link is null) return;

            link.LastChecked = DateTime.UtcNow;
            if (result.IsActive)
            {
                link.Status = LinkStatuses.Active;
                link.HttpCode = result.HttpCode;
                link.ResponseFingerprint = result.Fingerprint;
                await _notificationService.NotifyNewActiveLinksAsync(new List<LinkResult> { link });
            }
            else
            {
                link.Status = LinkStatuses.Inactive;
                link.HttpCode = result.HttpCode;
                await _notificationService.NotifyInactiveLinksAsync(new List<LinkResult> { link });
            }

            _logger.LogInformation("[RECHECK] {Sub}: Active={Active}, Code={Code}", subdomain, result.IsActive, result.HttpCode);
            await db.SaveChangesAsync();
        }
    }
