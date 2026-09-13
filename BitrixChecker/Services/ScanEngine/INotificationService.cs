using BitrixChecker.Models;

namespace BitrixChecker.Services.ScanEngine;

/// <summary>Sends notifications for scan lifecycle events.</summary>
public interface INotificationService
{
    /// <summary>Notifies about newly discovered active links.</summary>
    Task NotifyNewActiveLinksAsync(IReadOnlyList<LinkResult> newLinks, CancellationToken cancellationToken = default);

    /// <summary>Notifies when previously active links become inactive.</summary>
    Task NotifyInactiveLinksAsync(IReadOnlyList<LinkResult> inactiveLinks, CancellationToken cancellationToken = default);

    /// <summary>Notifies when scan processing completes.</summary>
    Task NotifyScanCompletedAsync(int scanJobId, int activeCount, int inactiveCount, CancellationToken cancellationToken = default);
}
