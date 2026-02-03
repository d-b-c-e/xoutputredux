using mozaAPI;
using static mozaAPI.mozaAPI;

namespace XOutputRedux.Moza.Plugin;

/// <summary>
/// Wrapper around the Moza SDK for wheel base control.
/// Trimmed from MozaHotkey.Core to include only the settings needed for profile integration.
/// </summary>
internal class MozaDevice : IDisposable
{
    private bool _initialized;
    private bool _disposed;

    public bool Initialize()
    {
        if (_initialized) return true;

        try
        {
            installMozaSDK();
            _initialized = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SetFfbStrength(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 0, 100);
        var error = setMotorFfbStrength(value);
        ThrowIfError(error, "Failed to set FFB strength");
    }

    public void SetWheelRotation(int degrees)
    {
        EnsureInitialized();
        degrees = Math.Clamp(degrees, 90, 2700);
        var error = setMotorLimitAngle(degrees, degrees);
        ThrowIfError(error, "Failed to set wheel rotation");
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("MozaDevice not initialized. Call Initialize() first.");
    }

    private static void ThrowIfError(ERRORCODE error, string message)
    {
        if (error != ERRORCODE.NORMAL)
            throw new InvalidOperationException($"{message}: {error}");
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_initialized)
        {
            try { removeMozaSDK(); }
            catch { }
        }

        _disposed = true;
        _initialized = false;
    }
}
