namespace BitrixChecker.Configuration;

public sealed class ScanOptions
{
    public const string SectionName = "Scan";
    public int HttpTimeoutSeconds { get; init; } = 10;
    public int ConnectTimeoutSeconds { get; init; } = 5;
    public int WorkerCount { get; init; } = 6;
    public int MaxConnectionsPerServer { get; init; } = 100;
    public int Parallelism { get; init; } = 20;
    public int BatchSize { get; init; } = 500;
    public int MaxCandidates { get; init; } = 100_000;
    public string TargetBaseDomain { get; init; } = "bitrix24.vn";
    public string RecheckCron { get; init; } = "0 2 * * *";

    public int MinLength { get; init; } = 5;
    public int MaxLength { get; init; } = 15;
    public string? WordlistPath { get; init; }
    public bool UseWordlist { get; init; } = false;
    public int RetryCount { get; init; } = 2;
    public int RetryDelayMs { get; init; } = 1500;

    public string? ProxyListPath { get; init; }
    public bool UseProxy { get; init; } = false;
    public bool UseExponentialBackoff { get; init; } = true;
}
