using BitrixChecker.Data;
using BitrixChecker.Models;

namespace BitrixChecker.Services.ScanEngine;

public sealed class NotificationService(ILogger<NotificationService> logger, IServiceScopeFactory scopeFactory) : INotificationService
{
    private readonly ILogger<NotificationService> _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task NotifyNewActiveLinksAsync(IReadOnlyList<LinkResult> newLinks, CancellationToken cancellationToken = default)
    {
        if (newLinks.Count == 0) return;
        _logger.LogWarning("[NOTIFICATION] Phat hien {Count} link ACTIVE moi.", newLinks.Count);
        var message = $"Phát hiện {newLinks.Count} link Bitrix24 ACTIVE mới.";
        await SaveNotificationAsync(message, "Success", cancellationToken);
    }

    public async Task NotifyInactiveLinksAsync(IReadOnlyList<LinkResult> inactiveLinks, CancellationToken cancellationToken = default)
    {
        if (inactiveLinks.Count == 0) return;
        _logger.LogWarning("[NOTIFICATION] {Count} link ACTIVE bi mat.", inactiveLinks.Count);
        var message = $"{inactiveLinks.Count} link Bitrix24 đã chuyển sang INACTIVE.";
        await SaveNotificationAsync(message, "Warning", cancellationToken);
    }

    public async Task NotifyScanCompletedAsync(int scanJobId, int activeCount, int inactiveCount, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[NOTIFICATION] Scan job #{JobId} hoan thanh. Active: {Active}, Inactive: {Inactive}",
            scanJobId, activeCount, inactiveCount);
        var message = $"Scan #{scanJobId} hoàn tất: {activeCount} active, {inactiveCount} inactive.";
        await SaveNotificationAsync(message, "Info", cancellationToken);
    }

    private async Task SaveNotificationAsync(string message, string type, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(new Notification { Message = message, Type = type });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save notification: {Message}", message);
        }
    }
}
