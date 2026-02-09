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

    public bool IsInitialized => _initialized;

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

    public void SetDamping(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 0, 100);
        var error = setMotorNaturalDamper(value);
        ThrowIfError(error, "Failed to set damping");
    }

    public void SetSpringStrength(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 0, 100);
        var error = setMotorSpringStrength(value);
        ThrowIfError(error, "Failed to set spring strength");
    }

    public void SetNaturalInertia(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 100, 500);
        var error = setMotorNaturalInertia(value);
        ThrowIfError(error, "Failed to set natural inertia");
    }

    public void SetMaxTorque(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 50, 100);
        var error = setMotorPeakTorque(value);
        ThrowIfError(error, "Failed to set max torque");
    }

    public void SetSpeedDamping(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 0, 100);
        var error = setMotorSpeedDamping(value);
        ThrowIfError(error, "Failed to set speed damping");
    }

    public void SetNaturalFriction(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 0, 100);
        var error = setMotorNaturalFriction(value);
        ThrowIfError(error, "Failed to set natural friction");
    }

    public void SetSpeedDampingStartPoint(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 0, 100);
        var error = setMotorSpeedDampingStartPoint(value);
        ThrowIfError(error, "Failed to set speed damping start point");
    }

    public void SetHandsOffProtection(int value)
    {
        EnsureInitialized();
        value = Math.Clamp(value, 0, 100);
        var error = setMotorHandsOffProtection(value);
        ThrowIfError(error, "Failed to set hands-off protection");
    }

    public void SetFfbReverse(bool reversed)
    {
        EnsureInitialized();
        var error = setMotorFfbReverse(reversed ? 1 : 0);
        ThrowIfError(error, "Failed to set FFB reverse");
    }

    // --- Getters (read current values from wheel) ---

    public int GetFfbStrength()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorFfbStrength(ref error);
        ThrowIfError(error, "Failed to get FFB strength");
        return result;
    }

    public int GetWheelRotation()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorLimitAngle(ref error);
        ThrowIfError(error, "Failed to get wheel rotation");
        // Returns (hardwareLimit, gameLimit) - use gameLimit
        return result?.Item2 ?? 900;
    }

    public int GetDamping()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorNaturalDamper(ref error);
        ThrowIfError(error, "Failed to get damping");
        return result;
    }

    public int GetSpringStrength()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorSpringStrength(ref error);
        ThrowIfError(error, "Failed to get spring strength");
        return result;
    }

    public int GetNaturalInertia()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorNaturalInertia(ref error);
        ThrowIfError(error, "Failed to get natural inertia");
        return result;
    }

    public int GetMaxTorque()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorPeakTorque(ref error);
        ThrowIfError(error, "Failed to get max torque");
        return result;
    }

    public int GetSpeedDamping()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorSpeedDamping(ref error);
        ThrowIfError(error, "Failed to get speed damping");
        return result;
    }

    public int GetNaturalFriction()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorNaturalFriction(ref error);
        ThrowIfError(error, "Failed to get natural friction");
        return result;
    }

    public int GetSpeedDampingStartPoint()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorSpeedDampingStartPoint(ref error);
        ThrowIfError(error, "Failed to get speed damping start point");
        return result;
    }

    public int GetHandsOffProtection()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorHandsOffProtection(ref error);
        ThrowIfError(error, "Failed to get hands-off protection");
        return result;
    }

    public bool GetFfbReverse()
    {
        EnsureInitialized();
        ERRORCODE error = ERRORCODE.NORMAL;
        var result = getMotorFfbReverse(ref error);
        ThrowIfError(error, "Failed to get FFB reverse");
        return result != 0;
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
