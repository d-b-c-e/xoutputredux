namespace XOutputRenew.Emulation;

/// <summary>
/// Service for managing ViGEm emulation.
/// </summary>
public class ViGEmService : IDisposable
{
    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Whether ViGEm is installed and available.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Initializes the ViGEm client.
    /// </summary>
    public bool Initialize()
    {
        if (_initialized) return IsAvailable;

        try
        {
            // TODO: Implement ViGEm client initialization
            IsAvailable = true;
            _initialized = true;
        }
        catch
        {
            IsAvailable = false;
            _initialized = true;
        }

        return IsAvailable;
    }

    /// <summary>
    /// Creates a new Xbox controller.
    /// </summary>
    public XboxController CreateXboxController()
    {
        if (!IsAvailable)
            throw new InvalidOperationException("ViGEm is not available");

        return new XboxController();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // TODO: Cleanup ViGEm client
        GC.SuppressFinalize(this);
    }
}
