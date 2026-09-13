using BitrixChecker.Configuration;
using Microsoft.Extensions.Options;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>Detects whether a subdomain is an active Bitrix24 instance using HTTP requests.</summary>
public sealed class LinkDetector(
    IHttpClientFactory httpClientFactory,
    IOptions<ScanOptions> scanOptions,
    ILogger<LinkDetector> logger) : ILinkDetector
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ScanOptions _scanOptions = scanOptions.Value;
    private readonly ILogger<LinkDetector> _logger = logger;

    public async Task<LinkDetectionResult> DetectAsync(string subdomain, string targetDomain, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("BitrixClient");
        var url = $"https://{subdomain}.{targetDomain}";

        var retryCount = Math.Clamp(_scanOptions.RetryCount, 0, 5);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Clamp(_scanOptions.RetryDelayMs, 0, 10000));

        for (int attempt = 1; attempt <= retryCount + 1; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                int code = (int)response.StatusCode;

                if (code is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location?.ToString() ?? "";
                    if (location.Contains("/auth/", StringComparison.OrdinalIgnoreCase) ||
                        location.Contains("login=yes", StringComparison.OrdinalIgnoreCase) ||
                        location.Contains("oauth", StringComparison.OrdinalIgnoreCase) ||
                        location.Contains("bitrix24.net", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LinkDetectionResult(true, code, $"redirect:{location[..Math.Min(location.Length, 200)]}");
                    }
                }
                else if (code == 200)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (content.Contains("name=\"USER_LOGIN\"") ||
                        content.Contains("name='USER_LOGIN'") ||
                        content.Contains("login-item") ||
                        content.Contains("b24-network-auth-form"))
                    {
                        if (!content.Contains("Create new Bitrix24", StringComparison.OrdinalIgnoreCase))
                        {
                            var fingerprint = content.Length > 2000 ? content[..2000] : content;
                            return new LinkDetectionResult(true, code, fingerprint);
                        }
                    }
                }

                return new LinkDetectionResult(false, code, null);
            }
            catch (Exception ex) when (attempt <= retryCount)
            {
                _logger.LogDebug("[DETECTOR] Loi lan {Attempt}/{Total} cho {Url}: {Message}", attempt, retryCount + 1, url, ex.Message);
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch
            {
                return new LinkDetectionResult(false, null, null);
            }
        }

        return new LinkDetectionResult(false, null, null);
    }
}
