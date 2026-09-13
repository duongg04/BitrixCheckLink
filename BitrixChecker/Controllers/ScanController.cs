using System.Security.Claims;
using BitrixChecker.Configuration;
using BitrixChecker.Data;
using BitrixChecker.Models;
using BitrixChecker.Services.ScanEngine;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BitrixChecker.Controllers;

[Route("api/scan")]
[ApiController]
[Authorize(Roles = "Admin")]
public class ScanController(AppDbContext context, IOptions<ScanOptions> scanOptions) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ScanOptions _scanOptions = scanOptions.Value;

    [HttpPost("generate")]
    [EnableRateLimiting("scan")]
    public IActionResult Generate([FromBody] ScanRequest req)
    {
        if (req == null) return BadRequest("Dữ liệu rỗng.");

        // Validate scan bounds
        if (req.MinLength < 1 || req.MinLength > 63)
            return BadRequest("MinLength phải từ 1 đến 63.");
        if (req.MaxLength < req.MinLength || req.MaxLength > 63)
            return BadRequest("MaxLength phải >= MinLength và <= 63.");
        if (req.Parallelism is < 1 or > 200)
            return BadRequest("Parallelism phải từ 1 đến 200.");
        if (req.RetryCount is < 0 or > 5)
            return BadRequest("RetryCount phải từ 0 đến 5.");
        if (req.RetryDelayMs is < 0 or > 10000)
            return BadRequest("RetryDelayMs phải từ 0 đến 10000.");
        if (req.Wordlist is { Count: > 100_000 })
            return BadRequest("Wordlist không được vượt quá 100000 mục.");
        if (req.Quantity.HasValue && (req.Quantity.Value < 1 || req.Quantity.Value > _scanOptions.MaxCandidates))
            return BadRequest($"Số lượng quét (Quantity) phải từ 1 đến {_scanOptions.MaxCandidates:N0}.");

        var effectiveLimit = req.Quantity ?? _scanOptions.MaxCandidates;

        if (!req.UseWordlist && !req.Quantity.HasValue && EstimateSystematicCandidates(req.MinLength, req.MaxLength) > _scanOptions.MaxCandidates)
            return BadRequest($"Khoảng độ dài này tạo quá nhiều candidate. Giới hạn hiện tại là {_scanOptions.MaxCandidates:N0}.");

        // Record audit
        var performedBy = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = "ScanJob",
            Action = "Generate",
            Details = $"Min={req.MinLength},Max={req.MaxLength},Quantity={effectiveLimit},Wordlist={req.Wordlist?.Count ?? 0},UseWordlist={req.UseWordlist},Parallelism={req.Parallelism}",
            PerformedById = performedBy
        });
        _context.SaveChanges();

        // Enqueue GenerateScanJob with configurable parameters
        BackgroundJob.Enqueue<ScanJobs>(x => x.GenerateScanJob(
            req.MinLength,
            req.MaxLength,
            req.Wordlist,
            req.UseWordlist,
            performedBy,
            effectiveLimit));

        return Ok(new { message = $"Đã kích hoạt quét từ {req.MinLength} đến {req.MaxLength} ký tự (tối đa {effectiveLimit:N0} subdomain)." });
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress()
    {
        var job = await _context.ScanJobs
            .Where(x => x.JobType == "Manual")
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.TotalExpected,
                x.TotalScanned,
                x.StartedAt,
                x.CompletedAt
            })
            .FirstOrDefaultAsync();

        return Ok(job);
    }

    private static long EstimateSystematicCandidates(int minLength, int maxLength)
    {
        long total = 0;
        long power = 1;
        for (var length = 1; length <= maxLength; length++)
        {
            power = power > 10_000_000 ? 10_000_001 : power * 36;
            if (length >= minLength)
            {
                total += power;
                if (total > 10_000_000) return total;
            }
        }
        return total;
    }
}

public class ScanRequest
{
    public int MinLength { get; set; }
    public int MaxLength { get; set; }
    public int? Quantity { get; set; }
    public List<string>? Wordlist { get; set; }
    public bool UseWordlist { get; set; } = false;
    public int Parallelism { get; set; } = 20;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 1500;
}
