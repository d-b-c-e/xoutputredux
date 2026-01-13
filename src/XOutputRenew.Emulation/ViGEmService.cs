using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace XOutputRenew.Emulation;

/// <summary>
/// Service for managing ViGEm emulation.
/// Adapted from XOutput.Emulation.ViGEm.ViGEmEmulator
/// </summary>
public class ViGEmService : IDisposable
{
    private ViGEmClient? _client;
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
            _client = new ViGEmClient();
            IsAvailable = true;
            _initialized = true;
        }
        catch (VigemBusNotFoundException)
        {
            IsAvailable = false;
            _initialized = true;
        }
        catch (DllNotFoundException)
        {
            IsAvailable = false;
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
        if (!IsAvailable || _client == null)
            throw new InvalidOperationException("ViGEm is not available. Call Initialize() first and check IsAvailable.");

        var controller = _client.CreateXbox360Controller();
        return new XboxController(controller);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _client?.Dispose();
        _client = null;
        IsAvailable = false;

        GC.SuppressFinalize(this);
    }
}
