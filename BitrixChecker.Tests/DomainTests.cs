using BitrixChecker.Models;
using BitrixChecker.Controllers;
using BitrixChecker.Configuration;
using BitrixChecker.Services.ScanEngine;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BitrixChecker.Tests;

public sealed class DomainTests
{
    [Theory]
    [InlineData("New")]
    [InlineData("Contacted")]
    [InlineData("Negotiating")]
    [InlineData("Successful")]
    [InlineData("Failed")]
    [InlineData("NotPotential")]
    public void ProcessingStatus_accepts_only_defined_values(string status) => Assert.Contains(status, ProcessingStatuses.All);

    [Fact]
    public void ProcessingStatuses_contains_all_workflow_states()
    {
        Assert.Equal(6, ProcessingStatuses.All.Count);
        Assert.Contains("New", ProcessingStatuses.All);
        Assert.Contains("Contacted", ProcessingStatuses.All);
        Assert.Contains("Negotiating", ProcessingStatuses.All);
        Assert.Contains("Successful", ProcessingStatuses.All);
        Assert.Contains("Failed", ProcessingStatuses.All);
        Assert.Contains("NotPotential", ProcessingStatuses.All);
    }

    [Fact]
    public void LinkResult_defaults_to_a_safe_inactive_state()
    {
        var result = new LinkResult();
        Assert.Equal(LinkStatuses.Inactive, result.Status);
        Assert.False(result.IsDeleted);
        Assert.NotNull(result.Processings);
        Assert.Empty(result.Processings);
    }

    [Fact]
    public void ProcessingHistory_defaults_are_safe()
    {
        var history = new ProcessingHistory();
        Assert.Equal(0, history.Id);
        Assert.Equal(0, history.LinkProcessingId);
        Assert.True(history.ChangedAt <= DateTime.UtcNow);
        Assert.Null(history.ChangeReason);
        Assert.Null(history.Note);
        Assert.Equal(string.Empty, history.Status);
    }

    [Fact]
    public void UpdateNoteRequest_and_UpdateProcessingRequest_are_mutable()
    {
        var noteReq = new UpdateNoteRequest { Note = "test note" };
        Assert.Equal("test note", noteReq.Note);

        var procReq = new UpdateProcessingRequest { Status = "Contacted", AssignedUserId = "user-1" };
        Assert.Equal("Contacted", procReq.Status);
        Assert.Equal("user-1", procReq.AssignedUserId);
    }

    [Fact]
    public void ApplicationUser_sets_display_name_correctly()
    {
        var user = new ApplicationUser { UserName = "sales1", DisplayName = "Nguyen Van A" };
        Assert.Equal("sales1", user.UserName);
        Assert.Equal("Nguyen Van A", user.DisplayName);
    }

    [Fact]
    public void AuditLog_properties_set_properly()
    {
        var log = new AuditLog
        {
            EntityName = "LinkResult",
            EntityId = "10",
            Action = "Recheck",
            Details = "Manual check",
            PerformedById = "admin-1"
        };
        Assert.Equal("LinkResult", log.EntityName);
        Assert.Equal("10", log.EntityId);
        Assert.Equal("Recheck", log.Action);
        Assert.Equal("Manual check", log.Details);
        Assert.Equal("admin-1", log.PerformedById);
    }

    [Fact]
    public void ScanEngineState_pause_and_resume_works()
    {
        ScanEngineState.IsSystemPaused = true;
        Assert.True(ScanEngineState.IsSystemPaused);
        ScanEngineState.IsSystemPaused = false;
        Assert.False(ScanEngineState.IsSystemPaused);
    }

    [Fact]
    public void ScanJob_initial_state_and_properties()
    {
        var job = new ScanJob
        {
            Name = "Scan bitrix24.vn",
            JobType = "Manual",
            Status = "Pending",
            TotalExpected = 100,
            TotalScanned = 0
        };
        Assert.Equal("Scan bitrix24.vn", job.Name);
        Assert.Equal("Pending", job.Status);
        Assert.Equal(100, job.TotalExpected);
        Assert.Equal(0, job.TotalScanned);
    }
}

public sealed class ScanEngineTests
{
    [Fact]
    public async Task SubdomainGenerator_generates_candidates_within_length_range()
    {
        var generator = new SubdomainGenerator(NullLogger<SubdomainGenerator>.Instance);
        var candidates = new List<string>();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var candidate in generator.GenerateAsync(3, 3, null, false, cancellationToken: cts.Token))
        {
            candidates.Add(candidate);
            if (candidates.Count >= 100) break;
        }

        Assert.NotEmpty(candidates);
        Assert.All(candidates, c => Assert.Equal(3, c.Length));
        Assert.All(candidates, c => Assert.Matches("^[a-z0-9]+$", c));
    }

    [Fact]
    public async Task SubdomainGenerator_normalizes_wordlist_entries()
    {
        var generator = new SubdomainGenerator(NullLogger<SubdomainGenerator>.Instance);
        var wordlist = new List<string> { "Việt Nam", "FPT-Software", "ABC@123" };
        var candidates = new List<string>();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var candidate in generator.GenerateAsync(3, 15, wordlist, true, cancellationToken: cts.Token))
        {
            candidates.Add(candidate);
            if (candidates.Count >= 100) break;
        }

        Assert.Contains(candidates, c => c == "vitnam");
        Assert.Contains(candidates, c => c == "fpt-software");
        Assert.Contains(candidates, c => c == "abc123");
    }

    [Fact]
    public async Task SubdomainGenerator_clamps_invalid_lengths_safely()
    {
        var generator = new SubdomainGenerator(NullLogger<SubdomainGenerator>.Instance);
        var candidates = new List<string>();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var candidate in generator.GenerateAsync(-5, 0, null, false, cancellationToken: cts.Token))
        {
            candidates.Add(candidate);
            if (candidates.Count >= 10) break;
        }

        Assert.NotEmpty(candidates);
        Assert.All(candidates, c => Assert.True(c.Length >= 1));
    }

    private static readonly string[] wordlist = ["a", "A", "a1"];

    [Fact]
    public async Task SubdomainGenerator_deduplicates_wordlist_and_respects_candidate_limit()
    {
        var generator = new SubdomainGenerator(NullLogger<SubdomainGenerator>.Instance);
        var candidates = new List<string>();

        await foreach (var candidate in generator.GenerateAsync(
            1, 2, wordlist, true, maxCandidates: 3))
        {
            candidates.Add(candidate);
        }

        Assert.Equal(3, candidates.Count);
        Assert.Equal(candidates.Count, candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ScanOptions_defaults_are_within_safe_limits()
    {
        var options = new ScanOptions();
        Assert.True(options.MinLength >= 1 && options.MinLength <= 63);
        Assert.True(options.MaxLength >= options.MinLength && options.MaxLength <= 63);
        Assert.True(options.RetryCount >= 0 && options.RetryCount <= 5);
        Assert.True(options.RetryDelayMs >= 0 && options.RetryDelayMs <= 10000);
        Assert.True(options.Parallelism >= 1 && options.Parallelism <= 200);
        Assert.True(options.BatchSize >= 1 && options.BatchSize <= 5000);
        Assert.True(options.MaxCandidates >= 1 && options.MaxCandidates <= 1_000_000);
        Assert.False(string.IsNullOrWhiteSpace(options.TargetBaseDomain));
    }

    [Fact]
    public void LinkDetectionResult_records_correct_state()
    {
        var active = new LinkDetectionResult(true, 200, "fingerprint-data");
        Assert.True(active.IsActive);
        Assert.Equal(200, active.HttpCode);
        Assert.Equal("fingerprint-data", active.Fingerprint);

        var inactive = new LinkDetectionResult(false, 404, null);
        Assert.False(inactive.IsActive);
        Assert.Equal(404, inactive.HttpCode);
        Assert.Null(inactive.Fingerprint);
    }

    [Fact]
    public void ScanRequest_validates_length_constraints()
    {
        var valid = new ScanRequest { MinLength = 5, MaxLength = 10, Quantity = 500 };
        Assert.True(valid.MinLength >= 1 && valid.MinLength <= 63);
        Assert.True(valid.MaxLength >= valid.MinLength && valid.MaxLength <= 63);
        Assert.Equal(500, valid.Quantity);

        var invalid = new ScanRequest { MinLength = 10, MaxLength = 5 };
        Assert.True(invalid.MaxLength < invalid.MinLength);
    }

    [Fact]
    public void LinkResult_supports_IsTracked_flag()
    {
        var result = new LinkResult { IsTracked = true };
        Assert.True(result.IsTracked);
        result.IsTracked = false;
        Assert.False(result.IsTracked);
    }

    [Fact]
    public void BulkRecheckRequest_initializes_with_empty_list()
    {
        var req = new BulkRecheckRequest();
        Assert.NotNull(req.LinkIds);
        Assert.Empty(req.LinkIds);

        var reqWithIds = new BulkRecheckRequest { LinkIds = [1, 2, 3] };
        Assert.Equal(3, reqWithIds.LinkIds.Count);
    }

    [Fact]
    public void Notification_model_defaults_are_correct()
    {
        var notif = new Notification { Message = "Test notice", Type = "Success" };
        Assert.Equal("Test notice", notif.Message);
        Assert.Equal("Success", notif.Type);
        Assert.False(notif.IsRead);
        Assert.True(notif.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task SubdomainGenerator_respects_maxCandidates_limit()
    {
        var gen = new SubdomainGenerator(NullLogger<SubdomainGenerator>.Instance);
        var candidates = new List<string>();
        await foreach (var item in gen.GenerateAsync(3, 3, null, false, maxCandidates: 15))
        {
            candidates.Add(item);
        }
        Assert.Equal(15, candidates.Count);
        Assert.Equal(candidates.Count, candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

/// <summary>Minimal null logger for unit tests.</summary>
internal sealed class NullLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public static readonly NullLogger<T> Instance = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
