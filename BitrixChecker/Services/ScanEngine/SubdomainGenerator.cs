namespace BitrixChecker.Services.ScanEngine;

/// <summary>
/// Generates candidate subdomains using systematic character sequences and/or wordlists.
/// </summary>
public sealed class SubdomainGenerator(ILogger<SubdomainGenerator> logger) : ISubdomainGenerator
{
    private const string CHARS = "abcdefghijklmnopqrstuvwxyz0123456789";
    private readonly ILogger<SubdomainGenerator> _logger = logger;

#pragma warning disable CS1998 // Async iterator without await is intentional
    public async IAsyncEnumerable<string> GenerateAsync(
        int minLength,
        int maxLength,
        IReadOnlyList<string>? wordlist,
        bool useWordlist,
        int maxCandidates = 100_000,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Validate lengths
        minLength = Math.Clamp(minLength, 1, 63);
        maxLength = Math.Clamp(maxLength, minLength, 63);
        maxCandidates = Math.Clamp(maxCandidates, 1, 1_000_000);
        var emitted = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Wordlist candidates (if enabled)
        if (useWordlist && wordlist is { Count: > 0 })
        {
            _logger.LogInformation("[GENERATOR] Sử dụng wordlist với {Count} từ khóa.", wordlist.Count);
            foreach (var word in wordlist)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = NormalizeWord(word);
                if (normalized.Length >= minLength && normalized.Length <= maxLength && seen.Add(normalized))
                {
                    if (emitted++ >= maxCandidates) yield break;
                    yield return normalized;
                }
            }
        }

        // 2. Systematic candidate generation
        _logger.LogInformation("[GENERATOR] Sinh subdomain theo thứ tự tuần tự từ {Min} đến {Max} ký tự.", minLength, maxLength);
        string? current = new(CHARS[0], minLength);
        while (current is not null && current.Length <= maxLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(current))
            {
                current = GetNextString(current, maxLength);
                continue;
            }
            if (emitted++ >= maxCandidates) yield break;
            yield return current;
            current = GetNextString(current, maxLength);
        }
    }
#pragma warning restore CS1998

    private static string NormalizeWord(string word)
    {
        // Lowercase, keep only a-z0-9 and hyphen
        var normalized = word.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch == '-')
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    private static string? GetNextString(string input, int totalLimit)
    {
        char[] result = input.ToCharArray();
        int index = result.Length - 1;
        while (index >= 0)
        {
            int charIndex = CHARS.IndexOf(result[index]);
            if (charIndex < CHARS.Length - 1)
            {
                result[index] = CHARS[charIndex + 1];
                return new string(result);
            }
            result[index] = CHARS[0];
            index--;
        }
        if (input.Length < totalLimit)
        {
            return new string(CHARS[0], input.Length + 1);
        }
        return null;
    }
}
