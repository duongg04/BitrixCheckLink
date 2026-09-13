namespace BitrixChecker.Services.ScanEngine;

/// <summary>Generates candidate subdomains for scanning.</summary>
public interface ISubdomainGenerator
{
    /// <summary>Generates a list of candidate subdomains based on configuration.</summary>
    /// <param name="minLength">Minimum subdomain length.</param>
    /// <param name="maxLength">Maximum subdomain length.</param>
    /// <param name="wordlist">Optional list of keywords to include.</param>
    /// <param name="useWordlist">Whether to use the wordlist.</param>
    /// <param name="maxCandidates">Maximum number of candidates to yield.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of candidate subdomains.</returns>
    IAsyncEnumerable<string> GenerateAsync(int minLength, int maxLength, IReadOnlyList<string>? wordlist, bool useWordlist, int maxCandidates = 100_000, CancellationToken cancellationToken = default);
}
