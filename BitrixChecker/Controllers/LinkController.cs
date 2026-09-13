using ClosedXML.Excel;
using System.Security.Claims;
using System.Text;
using BitrixChecker.Configuration;
using BitrixChecker.Data;
using BitrixChecker.Models;
using BitrixChecker.Services;
using BitrixChecker.Services.ScanEngine;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BitrixChecker.Controllers;

[Route("api/link")]
[ApiController]
[Authorize(Policy = "AdminOrUser")]
public sealed class LinkController : ControllerBase
{
    private const int PageSize = 50;
    private readonly AppDbContext _context;
    private readonly ScanOptions _scanOptions;

    public LinkController(AppDbContext context, IOptions<ScanOptions> scanOptions)
    {
        _context = context;
        _scanOptions = scanOptions.Value;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var active = await _context.LinkResults.CountAsync(x => !x.IsDeleted && x.Status == LinkStatuses.Active);
        if (!User.IsInRole("Admin")) return Ok(new { Active = active });

        return Ok(new
        {
            Total = await _context.LinkResults.CountAsync(x => !x.IsDeleted),
            Active = active,
            Inactive = await _context.LinkResults.CountAsync(x => !x.IsDeleted && x.Status == LinkStatuses.Inactive),
            Processed = await _context.LinkResults.CountAsync(x => !x.IsDeleted && x.Status == LinkStatuses.Active && x.Processings.Any(p => p.Status != "New")),
            ProcessedTotal = await _context.LinkProcessings.CountAsync(),
            IsPaused = ScanEngineState.IsSystemPaused
        });
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status = LinkStatuses.Active,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] string? assigneeId = null,
        [FromQuery] string? sortBy = "Subdomain",
        [FromQuery] string? sortDir = "asc",
        [FromQuery] string? processingStatus = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        page = Math.Max(1, page);
        status = string.IsNullOrWhiteSpace(status) ? LinkStatuses.Active : status.Trim().ToUpperInvariant();
        if (!User.IsInRole("Admin") && status is not (LinkStatuses.Active or "PROCESSED")) return Forbid();
        if (status is not ("ALL" or "ACTIVE" or "INACTIVE" or "PROCESSED")) return BadRequest(new { message = "Status must be ACTIVE, INACTIVE, PROCESSED, or ALL." });

                var query = _context.LinkResults.AsNoTracking().Where(x => !x.IsDeleted);

        if (status == LinkStatuses.Active)
        {
            query = query.Where(x => x.Status == LinkStatuses.Active);
        }
        else if (status == LinkStatuses.Inactive)
        {
            query = query.Where(x => x.Status == LinkStatuses.Inactive);
        }
        else if (status == "PROCESSED")
        {
            // Active links whose latest processing record has progressed beyond "New"
            query = query.Where(x => x.Status == LinkStatuses.Active &&
                x.Processings.Any(p => p.Status != "New"));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLowerInvariant().Trim();
            query = query.Where(x => x.Subdomain.ToLower().Contains(searchTerm) ||
                                     x.FullUrl.ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(assigneeId) && User.IsInRole("Admin"))
        {
            query = query.Where(x => x.Processings.Any(p => p.AssignedUserId == assigneeId));
        }

        // Date range filter (matches CreatedAt or LastChecked)
        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(x => x.CreatedAt >= from || x.LastChecked >= from);
        }
        if (dateTo.HasValue)
        {
            var to = dateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.CreatedAt <= to || x.LastChecked <= to);
        }

        // Filter by processing status (Sales Team filter)
        if (!string.IsNullOrWhiteSpace(processingStatus) &&
            ProcessingStatuses.All.Contains(processingStatus.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var matchedStatus = ProcessingStatuses.All.FirstOrDefault(s =>
                s.Equals(processingStatus.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matchedStatus))
            {
                query = query.Where(x => x.Processings.Any(p => p.Status == matchedStatus));
            }
        }

        // Sort (Id tiebreaker: Subdomain has no unique index, so ties would make
        // pagination unstable - rows repeating/vanishing across pages).
        var sortField = sortBy?.ToLowerInvariant() ?? "subdomain";
        var descending = sortDir?.ToLowerInvariant() == "desc";
        IOrderedQueryable<LinkResult> ordered = sortField switch
        {
            "id" => descending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id),
            "lastchecked" => descending ? query.OrderByDescending(x => x.LastChecked) : query.OrderBy(x => x.LastChecked),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "httpcode" => descending ? query.OrderByDescending(x => x.HttpCode) : query.OrderBy(x => x.HttpCode),
            _ => descending ? query.OrderByDescending(x => x.Subdomain) : query.OrderBy(x => x.Subdomain)
        };
        query = ordered.ThenBy(x => x.Id);

        var data = await query.Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => new
            {
                x.Id, x.Subdomain, x.FullUrl, x.Status, x.HttpCode, x.CreatedAt, x.LastChecked, x.IsTracked,
                Note = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.Note).FirstOrDefault(),
                ProcessingStatus = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.Status).FirstOrDefault(),
                AssignedUserId = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.AssignedUserId).FirstOrDefault(),
                AssignedUserName = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.AssignedUser != null ? p.AssignedUser.DisplayName : p.AssignedUserId).FirstOrDefault()
            })
            .ToListAsync();

                var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)total / PageSize);
        return Ok(new { data, total, page, pageSize = PageSize, totalPages });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var link = await _context.LinkResults
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id == id)
            .Select(x => new
            {
                x.Id, x.Subdomain, x.FullUrl, x.Status, x.HttpCode, x.CreatedAt, x.LastChecked, x.IsTracked,
                Note = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.Note).FirstOrDefault(),
                ProcessingStatus = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.Status).FirstOrDefault(),
                AssignedUserId = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.AssignedUserId).FirstOrDefault(),
                AssignedUserName = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => p.AssignedUser != null ? p.AssignedUser.DisplayName : p.AssignedUserId).FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (link is null) return NotFound();
        return Ok(link);
    }

    [HttpPost("recheck/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecheckLink(int id)
    {
        var link = await _context.LinkResults.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (link is null) return NotFound();

        await AddAuditAsync("LinkResult", id.ToString(), "Recheck", $"Manual recheck requested by {UserId()}");
        await _context.SaveChangesAsync();

        BackgroundJob.Enqueue<ScanJobs>(x => x.CheckSingleLink(link.Subdomain, _scanOptions.TargetBaseDomain, link.Id));

        return Ok(new { message = $"Đã gửi lệnh re-check cho {link.Subdomain}", linkId = id });
    }

    [HttpPost("recheck-bulk")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecheckBulk([FromBody] BulkRecheckRequest request)
    {
        if (request?.LinkIds == null || request.LinkIds.Count == 0)
            return BadRequest(new { message = "Danh sách link không được rỗng." });
        if (request.LinkIds.Count > 200)
            return BadRequest(new { message = "Tối đa 200 link mỗi lần." });

        var links = await _context.LinkResults
            .Where(x => request.LinkIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync();
        if (links.Count == 0) return NotFound(new { message = "Không tìm thấy link nào hợp lệ." });

        foreach (var link in links)
        {
            BackgroundJob.Enqueue<ScanJobs>(x => x.CheckSingleLink(link.Subdomain, _scanOptions.TargetBaseDomain, link.Id));
        }
        await AddAuditAsync("LinkResult", null, "BulkRecheck", $"Count={links.Count}");
        await _context.SaveChangesAsync();
        return Ok(new { message = $"Đã gửi lệnh re-check cho {links.Count} link.", count = links.Count });
    }

    [HttpPost("track/{id:int}")]
    public async Task<IActionResult> ToggleTrack(int id)
    {
        var link = await _context.LinkResults.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (link is null) return NotFound();
        link.IsTracked = !link.IsTracked;
        await AddAuditAsync("LinkResult", id.ToString(), "ToggleTrack", $"IsTracked={link.IsTracked}");
        await _context.SaveChangesAsync();
        return Ok(new { message = link.IsTracked ? "Đã bật theo dõi." : "Đã tắt theo dõi.", isTracked = link.IsTracked });
    }

    [HttpGet("processed")]
    public async Task<IActionResult> GetProcessed(
        [FromQuery] string? assigneeId = null,
        [FromQuery] string? processingStatus = null,
        [FromQuery] int page = 1,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        var query = _context.LinkResults.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == LinkStatuses.Active &&
                        x.Processings.Any(p => p.Status != "New"));

        // Filter by assignee
        if (!string.IsNullOrWhiteSpace(assigneeId))
        {
            query = query.Where(x => x.Processings.Any(p => p.AssignedUserId == assigneeId));
        }

        // Filter by processing status (Sales Team filter)
        if (!string.IsNullOrWhiteSpace(processingStatus) &&
            ProcessingStatuses.All.Contains(processingStatus.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            // Normalize the status for comparison
            var statusToFind = processingStatus.Trim();
            // Find the matching status from the valid set
            var matchedStatus = ProcessingStatuses.All.FirstOrDefault(s =>
                s.Equals(statusToFind, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matchedStatus))
            {
                query = query.Where(x => x.Processings.Any(p => p.Status == matchedStatus));
            }
        }

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLowerInvariant().Trim();
            query = query.Where(x => x.Subdomain.ToLower().Contains(searchTerm));
        }

        var data = await query.OrderBy(x => x.Subdomain).ThenBy(x => x.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => new
            {
                x.Id, x.Subdomain, x.FullUrl, x.Status, x.HttpCode, x.CreatedAt, x.LastChecked,
                Processing = x.Processings.OrderByDescending(p => p.UpdatedAt).Select(p => new
                {
                    p.Id, p.Note, p.Status, p.AssignedUserId,
                    AssignedUserName = p.AssignedUser != null ? p.AssignedUser.DisplayName : p.AssignedUserId,
                    p.UpdatedAt
                }).FirstOrDefault()
            })
            .ToListAsync();

        var total = await query.CountAsync();
        return Ok(new { data, total, page, pageSize = PageSize });
    }

    [HttpGet("processing-history/{linkResultId:int}")]
    public async Task<IActionResult> GetProcessingHistory(int linkResultId)
    {
        if (!await _context.LinkResults.AnyAsync(x => x.Id == linkResultId && !x.IsDeleted))
            return NotFound();

                var histories = await _context.ProcessingHistories
            .Where(h => h.LinkProcessing != null && h.LinkProcessing.LinkResultId == linkResultId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new
            {
                h.Id,
                h.Status,
                h.Note,
                AssignedUserId = h.AssignedUserId,
                AssignedUserName = h.AssignedUser != null ? h.AssignedUser.DisplayName : h.AssignedUserId,
                h.ChangedAt,
                ChangedByName = h.ChangedByUser != null ? h.ChangedByUser.DisplayName : h.ChangedByUserId,
                h.ChangeReason
            })
            .ToListAsync();

        return Ok(histories);
    }

    [HttpPut("processing/{linkResultId:int}/note")]
    public async Task<IActionResult> UpdateNote(int linkResultId, [FromBody] UpdateNoteRequest request)
    {
        if (request.Note?.Length > 1000) return BadRequest(new { message = "Note must not exceed 1000 characters." });
        var link = await _context.LinkResults.FirstOrDefaultAsync(x => x.Id == linkResultId && !x.IsDeleted);
        if (link is null) return NotFound();
        if (!User.IsInRole("Admin") && link.Status != LinkStatuses.Active) return Forbid();

        var processing = await _context.LinkProcessings.OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(x => x.LinkResultId == linkResultId);
        if (processing is null)
        {
            processing = new LinkProcessing { LinkResultId = linkResultId, AssignedUserId = UserId(), Status = "New" };
            _context.LinkProcessings.Add(processing);
            await _context.SaveChangesAsync();

            // Log initial processing (single history entry per user action).
            await AddAuditAsync("LinkProcessing", processing.Id.ToString(), "Create", "Processing record created.");
            await AddHistoryAsync(processing, "New", request.Note, UserId(), "Initial note");
        }
        else
        {
            var oldStatus = processing.Status;
            var oldAssign = processing.AssignedUserId;
            await AddAuditAsync("LinkProcessing", linkResultId.ToString(), "UpdateNote", "Sales note updated.");
            await AddHistoryAsync(processing, oldStatus, request.Note, UserId(),
                $"Note updated. Status={oldStatus};AssignedTo={oldAssign ?? "none"}");
        }

        processing.Note = request.Note?.Trim();
        processing.UpdatedAt = DateTime.UtcNow;
        processing.UpdatedByUserId = UserId();
        await _context.SaveChangesAsync();
        return Ok(new { message = "Processing note updated." });
    }

    [HttpPut("processing/{linkResultId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateProcessing(int linkResultId, [FromBody] UpdateProcessingRequest request)
    {
        if (request.Note?.Length > 1000) return BadRequest(new { message = "Note must not exceed 1000 characters." });
        if (request.Status is null || !ProcessingStatuses.All.Contains(request.Status)) return BadRequest(new { message = "Invalid processing status." });
        if (!await _context.LinkResults.AnyAsync(x => x.Id == linkResultId && !x.IsDeleted)) return NotFound();
        var newAssigneeId = string.IsNullOrWhiteSpace(request.AssignedUserId) ? null : request.AssignedUserId.Trim();
        // Validate the assignee exists BEFORE saving; otherwise an unknown id causes an
        // unhandled FK violation (HTTP 500) instead of a clean 400 response.
        if (newAssigneeId is not null &&
            !await _context.Users.AnyAsync(u => u.Id == newAssigneeId))
        {
            return BadRequest(new { message = "Assigned user does not exist." });
        }

        var processing = await _context.LinkProcessings.OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(x => x.LinkResultId == linkResultId);
        if (processing is null)
        {
            processing = new LinkProcessing { LinkResultId = linkResultId };
            _context.LinkProcessings.Add(processing);
            // Persist first so processing.Id exists before history records reference it (avoids FK violation).
            await _context.SaveChangesAsync();
        }

        var oldStatus = processing.Status;
        var oldAssign = processing.AssignedUserId;

        processing.Note = request.Note?.Trim();
        processing.Status = request.Status;
        processing.AssignedUserId = newAssigneeId;
        processing.UpdatedAt = DateTime.UtcNow;
        processing.UpdatedByUserId = UserId();
        await AddAuditAsync("LinkProcessing", linkResultId.ToString(), "AdminUpdate", $"Status={processing.Status};AssignedUserId={processing.AssignedUserId}");
        await AddHistoryAsync(processing, oldStatus, request.Note, UserId(),
            $"Admin updated. OldStatus={oldStatus};OldAssign={oldAssign ?? "none"};NewStatus={processing.Status}");
        await _context.SaveChangesAsync();
        return Ok(new { message = "Processing updated." });
    }

    [HttpPost("pause")]
    [Authorize(Roles = "Admin")]
    public IActionResult TogglePause([FromQuery] bool pause) { ScanEngineState.IsSystemPaused = pause; return Ok(new { isPaused = pause }); }

    [HttpDelete("inactive")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ArchiveInactive()
    {
        var records = await _context.LinkResults.Where(x => !x.IsDeleted && x.Status == LinkStatuses.Inactive).ToListAsync();
        foreach (var result in records) result.IsDeleted = true;
        await AddAuditAsync("LinkResult", null, "ArchiveInactive", $"Count={records.Count}");
        await _context.SaveChangesAsync();
        return Ok(new { archived = records.Count });
    }

    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? status = LinkStatuses.Active,
        [FromQuery] string? assigneeId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var query = _context.LinkResults.AsNoTracking().Where(x => !x.IsDeleted);

        status = string.IsNullOrWhiteSpace(status) ? LinkStatuses.Active : status.Trim().ToUpperInvariant();
        if (status == LinkStatuses.Active)
            query = query.Where(x => x.Status == LinkStatuses.Active);
        else if (status == LinkStatuses.Inactive)
            query = query.Where(x => x.Status == LinkStatuses.Inactive);
        else if (status == "PROCESSED")
            query = query.Where(x => x.Status == LinkStatuses.Active &&
                x.Processings.Any(p => p.Status != "New"));

        if (!string.IsNullOrWhiteSpace(assigneeId))
            query = query.Where(x => x.Processings.Any(p => p.AssignedUserId == assigneeId));

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(x => x.CreatedAt >= from || x.LastChecked >= from);
        }
        if (dateTo.HasValue)
        {
            var to = dateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.CreatedAt <= to || x.LastChecked <= to);
        }

        var links = await query.Include(x => x.Processings).OrderBy(x => x.Id).ToListAsync();
        var csv = new StringBuilder("\uFEFFId,Subdomain,FullUrl,Status,HttpCode,LastChecked,ProcessingStatus,AssignedTo\r\n");
        foreach (var link in links)
        {
            var processing = link.Processings?.OrderByDescending(p => p.UpdatedAt).FirstOrDefault();
            csv.AppendLine($"{link.Id},{Escape(link.Subdomain)},{Escape(link.FullUrl)},{link.Status},{link.HttpCode},{link.LastChecked:O},{(processing?.Status ?? "N/A")},{Escape(processing?.AssignedUserId)}");
        }
        var fileName = $"bitrix_links_{status.ToLower()}_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    [HttpGet("export-xlsx")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] string? status = LinkStatuses.Active,
        [FromQuery] string? assigneeId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var query = _context.LinkResults.AsNoTracking().Where(x => !x.IsDeleted);

        status = string.IsNullOrWhiteSpace(status) ? LinkStatuses.Active : status.Trim().ToUpperInvariant();
        if (status == LinkStatuses.Active)
            query = query.Where(x => x.Status == LinkStatuses.Active);
        else if (status == LinkStatuses.Inactive)
            query = query.Where(x => x.Status == LinkStatuses.Inactive);
        else if (status == "PROCESSED")
            query = query.Where(x => x.Status == LinkStatuses.Active &&
                x.Processings.Any(p => p.Status != "New"));

        if (!string.IsNullOrWhiteSpace(assigneeId))
            query = query.Where(x => x.Processings.Any(p => p.AssignedUserId == assigneeId));

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(x => x.CreatedAt >= from || x.LastChecked >= from);
        }
        if (dateTo.HasValue)
        {
            var to = dateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.CreatedAt <= to || x.LastChecked <= to);
        }

        var links = await query.Include(x => x.Processings).OrderBy(x => x.Id).ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Links");
        
        // Header
        worksheet.Cell(1, 1).Value = "Id";
        worksheet.Cell(1, 2).Value = "Subdomain";
        worksheet.Cell(1, 3).Value = "FullUrl";
        worksheet.Cell(1, 4).Value = "Status";
        worksheet.Cell(1, 5).Value = "HttpCode";
        worksheet.Cell(1, 6).Value = "LastChecked";
        worksheet.Cell(1, 7).Value = "ProcessingStatus";
        worksheet.Cell(1, 8).Value = "AssignedTo";

        // Data
        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];
            var processing = link.Processings?.OrderByDescending(p => p.UpdatedAt).FirstOrDefault();
            int row = i + 2;
            
            worksheet.Cell(row, 1).Value = link.Id;
            worksheet.Cell(row, 2).Value = link.Subdomain;
            worksheet.Cell(row, 3).Value = link.FullUrl;
            worksheet.Cell(row, 4).Value = link.Status;
            worksheet.Cell(row, 5).Value = link.HttpCode ?? 0;
            worksheet.Cell(row, 6).Value = link.LastChecked;
            worksheet.Cell(row, 7).Value = processing?.Status ?? "N/A";
            worksheet.Cell(row, 8).Value = processing?.AssignedUserId ?? "";
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        var fileName = $"bitrix_links_{status.ToLower()}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private string? UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Task AddAuditAsync(string entityName, string? entityId, string action, string details)
    {
        _context.AuditLogs.Add(new AuditLog { EntityName = entityName, EntityId = entityId, Action = action, Details = details, PerformedById = UserId() });
        return Task.CompletedTask;
    }

    private Task AddHistoryAsync(LinkProcessing processing, string oldStatus, string? note, string? userId, string? reason)
    {
        _context.ProcessingHistories.Add(new ProcessingHistory
        {
            LinkProcessingId = processing.Id,
            Status = oldStatus,
            Note = note,
            AssignedUserId = processing.AssignedUserId,
            ChangedByUserId = userId,
            ChangeReason = reason
        });
        return Task.CompletedTask;
    }
    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}

public sealed class UpdateNoteRequest { public string? Note { get; init; } }
public sealed class UpdateProcessingRequest { public string? Note { get; init; } public string? Status { get; init; } public string? AssignedUserId { get; set; } }
public static class ProcessingStatuses { public static readonly HashSet<string> All = new(StringComparer.Ordinal) { "New", "Contacted", "Negotiating", "Successful", "Failed", "NotPotential" }; }
public sealed class BulkRecheckRequest { public List<int> LinkIds { get; init; } = []; }
