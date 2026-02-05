using mozaAPI;
using static mozaAPI.mozaAPI;

namespace XOutputRedux.Moza.Helper;

/// <summary>
/// Helper app that applies Moza wheel settings via the SDK.
/// Runs in a separate process so that DirectInput axis calibration
/// updates correctly in the calling application.
///
/// The SDK connection must stay alive for settings to persist —
/// calling removeMozaSDK() causes Pit House to revert to its
/// stored defaults. The helper stays running until stdin closes
/// (when the parent process kills it or exits).
/// </summary>
internal class Program
{
    private static string _logPath = "";

    private static void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        Console.WriteLine(line);
        try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
    }

    static int Main(string[] args)
    {
        _logPath = Path.Combine(AppContext.BaseDirectory, "MozaHelper.log");

        try
        {
            var settings = ParseArgs(args);
            if (settings.Count == 0)
            {
                Log("Usage: MozaHelper.exe --rotation 270 --ffb 80 --torque 95 --reverse false --damping 15 --spring 20 --inertia 120 --speed-damping 30");
                return 1;
            }

            // Clean up any stale SDK state from a previous crash/force-kill
            Log("MozaHelper: cleaning stale SDK state...");
            try { removeMozaSDK(); } catch { }
            Thread.Sleep(500);

            Log("MozaHelper: initializing SDK...");
            installMozaSDK();
            Log("MozaHelper: SDK initialized");

            // Wait for device discovery (SDK needs time to connect to Pit House)
            Log("MozaHelper: waiting for device discovery...");
            var deviceReady = false;
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                Thread.Sleep(1000);
                try
                {
                    ERRORCODE error = ERRORCODE.NORMAL;
                    getMotorLimitAngle(ref error);
                    if (error == ERRORCODE.NORMAL)
                    {
                        deviceReady = true;
                        Log($"MozaHelper: device ready after {attempt}s");
                        break;
                    }
                }
                catch
                {
                    // Not ready yet
                }
                Log($"MozaHelper: attempt {attempt}/10 - device not ready");
            }

            if (!deviceReady)
            {
                Log("MozaHelper: device discovery timed out after 10s");
                removeMozaSDK();
                return 1;
            }

            // Query current rotation before applying changes.
            // This is the "reference rotation" — the rotation at which
            // the device maps its full HID axis range (0-65535).
            // Needed for axis auto-scaling when the target rotation differs.
            try
            {
                ERRORCODE refError = ERRORCODE.NORMAL;
                var refResult = getMotorLimitAngle(ref refError);
                if (refError == ERRORCODE.NORMAL && refResult != null)
                {
                    Log($"MozaHelper: getMotorLimitAngle returned hardware={refResult.Item1}, game={refResult.Item2}");
                    // Item1 = hardware max rotation, Item2 = current game limit
                    // After removeMozaSDK() cleanup, game limit may be 0 (unset).
                    // Use hardware limit as reference if game limit is 0.
                    var refRotation = refResult.Item2 > 0 ? refResult.Item2 : refResult.Item1;
                    Log($"MozaHelper: ref-rotation={refRotation}");
                }
            }
            catch
            {
                // Non-fatal — scaling will fall back to no adjustment
            }

            // Apply each setting (retry once on NODEVICES — can happen if
            // the SDK session isn't fully ready for writes yet)
            var failedCount = 0;
            var noDevicesCount = 0;
            foreach (var (key, value) in settings)
            {
                try
                {
                    ApplySetting(key, value);
                    Log($"MozaHelper: set {key} = {value}");
                }
                catch (Exception ex)
                {
                    failedCount++;
                    if (ex.Message.Contains("NODEVICES"))
                        noDevicesCount++;
                    Log($"MozaHelper: failed to set {key} = {value}: {ex.Message}");
                }
            }

            // If all failures were NODEVICES, reinitialize SDK and retry once
            if (failedCount > 0 && noDevicesCount == failedCount)
            {
                Log("MozaHelper: all setters returned NODEVICES, reinitializing SDK...");
                try { removeMozaSDK(); } catch { }
                Thread.Sleep(1000);
                installMozaSDK();
                Thread.Sleep(2000);

                foreach (var (key, value) in settings)
                {
                    try
                    {
                        ApplySetting(key, value);
                        Log($"MozaHelper: set {key} = {value} (retry)");
                    }
                    catch (Exception ex)
                    {
                        Log($"MozaHelper: failed to set {key} = {value} (retry): {ex.Message}");
                    }
                }
            }

            // Keep the SDK alive — removeMozaSDK() causes Pit House to
            // revert settings to defaults. Wait until stdin closes (parent
            // process exits or kills us) before cleaning up.
            Log("MozaHelper: settings applied, keeping SDK alive...");
            Console.Out.Flush();

            try
            {
                // Block until stdin is closed by the parent process
                Console.In.ReadToEnd();
            }
            catch
            {
                // stdin closed or broken pipe — time to exit
            }

            Log("MozaHelper: stdin closed, cleaning up SDK...");
            removeMozaSDK();
            Log("MozaHelper: done");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"MozaHelper: fatal error: {ex.Message}");
            try { removeMozaSDK(); } catch { }
            return 1;
        }
    }

    private static void ApplySetting(string key, string value)
    {
        ERRORCODE error;
        switch (key)
        {
            case "rotation":
                var degrees = Math.Clamp(int.Parse(value), 90, 2700);
                error = setMotorLimitAngle(degrees, degrees);
                ThrowIfError(error, "setMotorLimitAngle");
                break;

            case "ffb":
                var ffb = Math.Clamp(int.Parse(value), 0, 100);
                error = setMotorFfbStrength(ffb);
                ThrowIfError(error, "setMotorFfbStrength");
                break;

            case "torque":
                var torque = Math.Clamp(int.Parse(value), 50, 100);
                error = setMotorPeakTorque(torque);
                ThrowIfError(error, "setMotorPeakTorque");
                break;

            case "reverse":
                var rev = bool.Parse(value) ? 1 : 0;
                error = setMotorFfbReverse(rev);
                ThrowIfError(error, "setMotorFfbReverse");
                break;

            case "damping":
                var damp = Math.Clamp(int.Parse(value), 0, 100);
                error = setMotorNaturalDamper(damp);
                ThrowIfError(error, "setMotorNaturalDamper");
                break;

            case "spring":
                var spring = Math.Clamp(int.Parse(value), 0, 100);
                error = setMotorSpringStrength(spring);
                ThrowIfError(error, "setMotorSpringStrength");
                break;

            case "inertia":
                var inertia = Math.Clamp(int.Parse(value), 100, 500);
                error = setMotorNaturalInertia(inertia);
                ThrowIfError(error, "setMotorNaturalInertia");
                break;

            case "speed-damping":
                var sd = Math.Clamp(int.Parse(value), 0, 100);
                error = setMotorSpeedDamping(sd);
                ThrowIfError(error, "setMotorSpeedDamping");
                break;

            default:
                Log($"MozaHelper: unknown setting '{key}', skipping");
                break;
        }
    }

    private static void ThrowIfError(ERRORCODE error, string call)
    {
        if (error != ERRORCODE.NORMAL)
            throw new InvalidOperationException($"{call} returned {error}");
    }

    private static List<(string key, string value)> ParseArgs(string[] args)
    {
        var result = new List<(string, string)>();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                var val = args[i + 1];
                result.Add((key, val));
                i++; // skip value
            }
        }
        return result;
    }
}
