using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BitrixChecker.Middleware;

/// <summary>Middleware for adding correlation ID to each request for traceability.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<CorrelationIdMiddleware> _logger = logger;
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        }

        context.Response.Headers[HeaderName] = correlationId;
        using var scope = _logger.BeginScope(new Dictionary<string, object> { [HeaderName] = correlationId });
        await _next(context);
    }
}
