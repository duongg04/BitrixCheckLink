namespace BitrixChecker.Services.ScanEngine;

/// <summary>
/// Global runtime control state for the scan engine.
/// </summary>
public static class ScanEngineState
{
    private static int _isSystemPaused;

    /// <summary>
    /// Gets or sets whether scanning is paused system-wide.
    /// </summary>
    public static bool IsSystemPaused
    {
        get => Volatile.Read(ref _isSystemPaused) == 1;
        set => Volatile.Write(ref _isSystemPaused, value ? 1 : 0);
    }
}
