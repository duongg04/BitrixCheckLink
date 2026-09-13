using System.Net;
using System.Net.Http;
using System.Text;
using BitrixChecker.Configuration;
using BitrixChecker.Models;
using BitrixChecker.Services.ScanEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BitrixChecker.Tests;

public sealed class LinkDetectorTests
{
    private static LinkDetector CreateDetector(IReadOnlyList<(string sub, string body, int code)> mockResponses)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("BitrixClient")
            .ConfigurePrimaryHttpMessageHandler(() => new MockHttpHandler(mockResponses));
        services.AddSingleton(Options.Create(new ScanOptions
        {
            HttpTimeoutSeconds = 10,
            ConnectTimeoutSeconds = 5,
            RetryCount = 0,
            RetryDelayMs = 0,
            TargetBaseDomain = "bitrix24.vn"
        }));
        var provider = services.BuildServiceProvider();
        return new LinkDetector(provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IOptions<ScanOptions>>(),
            NullLogger<LinkDetector>.Instance);
    }

    private static LinkDetector CreateDetectorWithLocation(IReadOnlyList<(string sub, string body, int code, string? location)> responses)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("BitrixClient")
            .ConfigurePrimaryHttpMessageHandler(() => new MockHttpHandler(responses));
        services.AddSingleton(Options.Create(new ScanOptions
        {
            HttpTimeoutSeconds = 10,
            ConnectTimeoutSeconds = 5,
            RetryCount = 0,
            RetryDelayMs = 0,
            TargetBaseDomain = "bitrix24.vn"
        }));
        var provider = services.BuildServiceProvider();
        return new LinkDetector(provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IOptions<ScanOptions>>(),
            NullLogger<LinkDetector>.Instance);
    }

    [Fact]
    public async Task DetectAsync_returns_active_for_302_redirect_to_bitrix24()
    {
        var detector = CreateDetectorWithLocation([
            ("abc", "", 302, (string?)"https://abc.bitrix24.vn/auth/")
        ]);
        var result = await detector.DetectAsync("abc", "bitrix24.vn");
        Assert.True(result.IsActive);
        Assert.Equal(302, result.HttpCode);
    }

    [Fact]
    public async Task DetectAsync_returns_active_for_200_with_login_form()
    {
        var detector = CreateDetector([
            ("abc", "<form name=\"USER_LOGIN\">login-item</form>", 200)
        ]);
        var result = await detector.DetectAsync("abc", "bitrix24.vn");
        Assert.True(result.IsActive);
        Assert.Equal(200, result.HttpCode);
        Assert.NotNull(result.Fingerprint);
    }

    [Fact]
    public async Task DetectAsync_returns_inactive_for_404()
    {
        var detector = CreateDetector([
            ("abc", "<html>Not Found</html>", 404)
        ]);
        var result = await detector.DetectAsync("abc", "bitrix24.vn");
        Assert.False(result.IsActive);
        Assert.Equal(404, result.HttpCode);
    }

    [Fact]
    public async Task DetectAsync_returns_inactive_for_200_without_bitrix_indicators()
    {
        var detector = CreateDetector([
            ("xyz", "<html>Generic page</html>", 200)
        ]);
        var result = await detector.DetectAsync("xyz", "bitrix24.vn");
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task DetectAsync_returns_inactive_for_200_with_create_button()
    {
        var detector = CreateDetector([
            ("new", "<html>Create new Bitrix24</html>", 200)
        ]);
        var result = await detector.DetectAsync("new", "bitrix24.vn");
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task DetectAsync_returns_inactive_for_302_without_bitrix_redirect()
    {
        var detector = CreateDetectorWithLocation([
            ("abc", "", 302, (string?)"https://other-site.com/page")
        ]);
        var result = await detector.DetectAsync("abc", "bitrix24.vn");
        Assert.False(result.IsActive);
        Assert.Equal(302, result.HttpCode);
    }
}

public sealed class StatusTransitionTests
{
    [Fact]
    public void LinkResult_can_transition_from_active_to_inactive()
    {
        var result = new LinkResult { Subdomain = "test", FullUrl = "https://test.bitrix24.vn", Status = LinkStatuses.Active };
        result.Status = LinkStatuses.Inactive;
        result.LastChecked = DateTime.UtcNow;
        Assert.Equal(LinkStatuses.Inactive, result.Status);
    }

    [Fact]
    public void LinkResult_can_transition_from_inactive_to_active()
    {
        var result = new LinkResult { Subdomain = "test", FullUrl = "https://test.bitrix24.vn", Status = LinkStatuses.Inactive };
        result.Status = LinkStatuses.Active;
        result.HttpCode = 200;
        result.LastChecked = DateTime.UtcNow;
        Assert.Equal(LinkStatuses.Active, result.Status);
        Assert.Equal(200, result.HttpCode);
    }

    [Fact]
    public void ProcessingStatus_can_transition_New_to_Contacted()
    {
        var processing = new LinkProcessing { LinkResultId = 1, Status = "New" };
        processing.Status = "Contacted";
        processing.UpdatedAt = DateTime.UtcNow;
        Assert.Equal("Contacted", processing.Status);
    }

    [Fact]
    public void ProcessingStatus_can_transition_Contacted_to_Negotiating()
    {
        var processing = new LinkProcessing { LinkResultId = 1, Status = "Contacted" };
        processing.Status = "Negotiating";
        processing.UpdatedAt = DateTime.UtcNow;
        Assert.Equal("Negotiating", processing.Status);
    }

    [Fact]
    public void ProcessingStatus_can_transition_Negotiating_to_Successful()
    {
        var processing = new LinkProcessing { LinkResultId = 1, Status = "Negotiating" };
        processing.Status = "Successful";
        processing.UpdatedAt = DateTime.UtcNow;
        Assert.Equal("Successful", processing.Status);
    }

    [Fact]
    public void ProcessingStatus_can_transition_Negotiating_to_Failed()
    {
        var processing = new LinkProcessing { LinkResultId = 1, Status = "Negotiating" };
        processing.Status = "Failed";
        processing.UpdatedAt = DateTime.UtcNow;
        Assert.Equal("Failed", processing.Status);
    }

    [Fact]
    public void ProcessingStatus_can_transition_any_to_NotPotential()
    {
        var processing = new LinkProcessing { LinkResultId = 1, Status = "New" };
        processing.Status = "NotPotential";
        processing.UpdatedAt = DateTime.UtcNow;
        Assert.Equal("NotPotential", processing.Status);
    }

    [Fact]
    public void ProcessingHistory_records_every_status_change()
    {
        var processing = new LinkProcessing { LinkResultId = 1, Status = "New" };
        var history1 = new ProcessingHistory
        {
            LinkProcessingId = processing.Id,
            Status = processing.Status,
            ChangedAt = DateTime.UtcNow,
            ChangeReason = "Initial state"
        };
        processing.Status = "Contacted";
        var history2 = new ProcessingHistory
        {
            LinkProcessingId = processing.Id,
            Status = processing.Status,
            ChangedAt = DateTime.UtcNow,
            ChangeReason = "Customer contacted"
        };

        Assert.Equal("New", history1.Status);
        Assert.Equal("Contacted", history2.Status);
        Assert.Equal("Initial state", history1.ChangeReason);
        Assert.Equal("Customer contacted", history2.ChangeReason);
    }
}

/// <summary>Mock HttpHandler for unit testing LinkDetector without real HTTP calls.</summary>
internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (int code, string? location, string body)> _responses;

    public MockHttpHandler(IReadOnlyList<(string sub, string body, int code)> responses)
    {
        _responses = [];
        foreach (var (sub, body, code) in responses)
            _responses[sub] = (code, null, body);
    }

    public MockHttpHandler(IReadOnlyList<(string sub, string body, int code, string? location)> responses)
    {
        _responses = [];
        foreach (var (sub, body, code, location) in responses)
            _responses[sub] = (code, location, body);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var subdomain = ExtractSubdomain(request.RequestUri?.ToString() ?? "");
        if (_responses.TryGetValue(subdomain, out var response))
        {
            var message = new HttpResponseMessage((HttpStatusCode)response.code)
            {
                Content = new StringContent(response.body, Encoding.UTF8, "text/html")
            };
            if (response.location != null)
                message.Headers.Location = new Uri(response.location);
            return Task.FromResult(message);
        }

        var notFound = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("<html>Not Found</html>", Encoding.UTF8, "text/html")
        };
        return Task.FromResult(notFound);
    }

    private static string ExtractSubdomain(string url)
    {
        var uri = new Uri(url);
        var host = uri.Host;
        var parts = host.Split('.');
        return parts.Length > 0 ? parts[0] : host;
    }
}
