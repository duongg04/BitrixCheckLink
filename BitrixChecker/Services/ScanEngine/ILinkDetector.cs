using BitrixChecker.Models;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>Result of a single link detection check.</summary>
public sealed record LinkDetectionResult(bool IsActive, int? HttpCode, string? Fingerprint);

/// <summary>Detects whether a subdomain is an active Bitrix24 instance.</summary>
public interface ILinkDetector
{
    /// <summary>Checks a single subdomain against the target domain.</summary>
    /// <param name="subdomain">The subdomain to check (e.g. "abc").</param>
    /// <param name="targetDomain">The base domain (e.g. "bitrix24.vn").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detection result with active status, HTTP code, and fingerprint.</returns>
    Task<LinkDetectionResult> DetectAsync(string subdomain, string targetDomain, CancellationToken cancellationToken = default);
}