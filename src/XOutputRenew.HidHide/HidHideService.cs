namespace XOutputRenew.HidHide;

/// <summary>
/// Service for managing HidHide device hiding.
/// </summary>
public class HidHideService : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Whether HidHide is installed and available.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Initializes the HidHide service.
    /// </summary>
    public bool Initialize()
    {
        try
        {
            // TODO: Check if HidHide is installed
            IsAvailable = false; // Will be true once implemented
            return IsAvailable;
        }
        catch
        {
            IsAvailable = false;
            return false;
        }
    }

    /// <summary>
    /// Hides a device by its hardware ID.
    /// </summary>
    public bool HideDevice(string hardwareId)
    {
        if (!IsAvailable) return false;

        // TODO: Implement device hiding via HidHide
        return false;
    }

    /// <summary>
    /// Unhides a device by its hardware ID.
    /// </summary>
    public bool UnhideDevice(string hardwareId)
    {
        if (!IsAvailable) return false;

        // TODO: Implement device unhiding via HidHide
        return false;
    }

    /// <summary>
    /// Adds an application to the whitelist (can still see hidden devices).
    /// </summary>
    public bool WhitelistApplication(string executablePath)
    {
        if (!IsAvailable) return false;

        // TODO: Implement application whitelisting
        return false;
    }

    /// <summary>
    /// Gets list of currently hidden device hardware IDs.
    /// </summary>
    public IEnumerable<string> GetHiddenDevices()
    {
        if (!IsAvailable) return [];

        // TODO: Implement getting hidden devices
        return [];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
