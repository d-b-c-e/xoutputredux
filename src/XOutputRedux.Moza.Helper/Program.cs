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
///
/// When FFB enhancement is enabled, the helper reads "ffb:&lt;value&gt;"
/// commands from stdin and drives an ETSine effect for vibration-style
/// rumble instead of DirectInput ConstantForce.
/// </summary>
internal class Program
{
    private static string _logPath = "";

    // FFB enhancement state
    private static bool _ffbEnhanceEnabled;
    private static int _ffbFrequencyMs = 50; // sine period in ms (default ~20Hz vibration)
    private static ETSine? _sineEffect;
    private static bool _sineStarted;

    // Ambient effects state
    private static bool _ambientEnabled;
    private static int _ambientSpringPct = 30;
    private static int _ambientFrictionPct = 20;
    private static int _ambientDamperPct = 15;
    private static ETSpring? _ambientSpring;
    private static ETFriction? _ambientFriction;
    private static ETDamper? _ambientDamper;

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
                Log("Usage: MozaHelper.exe --rotation 270 --ffb 80 --torque 95 --reverse false --damping 15 --spring 20 --inertia 120 --speed-damping 30 --friction 10 --speed-damping-start 20 --hands-off 50 --ffb-enhance true --ffb-frequency 50");
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
            //
            // After removeMozaSDK()/installMozaSDK(), the SDK may need
            // extra time to sync rotation values even though the device
            // reports as "ready". Retry a few times with delays.
            try
            {
                var refRotation = 0;
                for (var refAttempt = 1; refAttempt <= 5; refAttempt++)
                {
                    ERRORCODE refError = ERRORCODE.NORMAL;
                    var refResult = getMotorLimitAngle(ref refError);
                    if (refError == ERRORCODE.NORMAL && refResult != null)
                    {
                        Log($"MozaHelper: getMotorLimitAngle returned hardware={refResult.Item1}, game={refResult.Item2}");
                        // Item1 = hardware max rotation, Item2 = current game limit
                        // After removeMozaSDK() cleanup, game limit may be 0 (unset).
                        // Use hardware limit as reference if game limit is 0.
                        refRotation = refResult.Item2 > 0 ? refResult.Item2 : refResult.Item1;
                    }

                    if (refRotation > 0)
                        break;

                    Log($"MozaHelper: ref-rotation query attempt {refAttempt}/5 returned 0, retrying...");
                    Thread.Sleep(1000);
                }
                Log($"MozaHelper: ref-rotation={refRotation}");
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

            // Initialize ETSine effect if FFB enhancement is enabled
            if (_ffbEnhanceEnabled)
            {
                InitializeSineEffect();
            }

            // Initialize ambient effects if enabled
            if (_ambientEnabled)
            {
                InitializeAmbientEffects();
            }

            // Keep the SDK alive — removeMozaSDK() causes Pit House to
            // revert settings to defaults. Read lines from stdin to process
            // FFB commands (or wait for stdin close when FFB is not enabled).
            Log("MozaHelper: settings applied, keeping SDK alive...");
            Console.Out.Flush();

            try
            {
                // Read stdin line-by-line. The parent sends:
                //   "ffb:<value>"  — update sine effect magnitude (0.000-1.000)
                //   "ffb-stop"     — stop the sine effect
                //   EOF (close)    — time to exit
                string? line;
                while ((line = Console.In.ReadLine()) != null)
                {
                    if (line.StartsWith("ffb:"))
                    {
                        if (double.TryParse(line[4..], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var ffbValue))
                        {
                            UpdateSineEffect(ffbValue);
                        }
                    }
                    else if (line == "ffb-stop")
                    {
                        StopSineEffect();
                    }
                }
            }
            catch
            {
                // stdin closed or broken pipe — time to exit
            }

            StopSineEffect();
            StopAmbientEffects();
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

    private static void InitializeSineEffect()
    {
        try
        {
            ERRORCODE error = ERRORCODE.NORMAL;
            _sineEffect = createWheelbaseETSine(IntPtr.Zero, ref error);
            if (error != ERRORCODE.NORMAL || _sineEffect == null)
            {
                Log($"MozaHelper: failed to create ETSine effect: {error}");
                _sineEffect = null;
                return;
            }

            // Configure the sine wave for rumble-style vibration
            _sineEffect.setMagnitude(0);                           // Start silent
            _sineEffect.setPeriod((ulong)_ffbFrequencyMs * 1000);  // Period in microseconds
            _sineEffect.setOffset(0);                              // Centered oscillation
            _sineEffect.setPhase(0);                               // Start at zero-crossing
            _sineEffect.setDuration(0);                            // Infinite duration
            _sineEffect.setGain(Effect.DI_FFNOMINALMAX);           // Full gain (scaled by FFB Strength)

            Log($"MozaHelper: ETSine effect created (period={_ffbFrequencyMs}ms)");
        }
        catch (Exception ex)
        {
            Log($"MozaHelper: failed to initialize sine effect: {ex.Message}");
            _sineEffect = null;
        }
    }

    private static void UpdateSineEffect(double value)
    {
        if (_sineEffect == null) return;

        try
        {
            // Scale magnitude: 0.0-1.0 → 0 to DI_FFNOMINALMAX (10000)
            var magnitude = (ulong)(value * Effect.DI_FFNOMINALMAX);
            _sineEffect.setMagnitude(magnitude);

            if (!_sineStarted && value > 0.001)
            {
                _sineEffect.start();
                _sineStarted = true;
            }
            else if (_sineStarted && value < 0.001)
            {
                _sineEffect.stop();
                _sineStarted = false;
            }
        }
        catch (Exception ex)
        {
            Log($"MozaHelper: sine effect update failed: {ex.Message}");
        }
    }

    private static void StopSineEffect()
    {
        if (_sineEffect == null) return;

        try
        {
            if (_sineStarted)
            {
                _sineEffect.stop();
                _sineStarted = false;
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private static void InitializeAmbientEffects()
    {
        try
        {
            ERRORCODE error;

            // Create and start ambient spring effect
            if (_ambientSpringPct > 0)
            {
                error = ERRORCODE.NORMAL;
                _ambientSpring = createWheelbaseETSpring(IntPtr.Zero, ref error);
                if (error == ERRORCODE.NORMAL && _ambientSpring != null)
                {
                    var coeff = (long)(_ambientSpringPct / 100.0 * (long)Effect.DI_FFNOMINALMAX);
                    _ambientSpring.setPositiveCoefficient(coeff);
                    _ambientSpring.setNegativeCoefficient(coeff);
                    _ambientSpring.setPositiveSaturation(Effect.DI_FFNOMINALMAX);
                    _ambientSpring.setNegativeSaturation(Effect.DI_FFNOMINALMAX);
                    _ambientSpring.setOffset(0);        // Center at wheel center
                    _ambientSpring.setDeadBand(0);       // No dead zone
                    _ambientSpring.setDuration(0);       // Infinite
                    _ambientSpring.setGain(Effect.DI_FFNOMINALMAX);
                    _ambientSpring.start();
                    Log($"MozaHelper: ambient spring started (coefficient={coeff})");
                }
                else
                {
                    Log($"MozaHelper: failed to create ambient spring: {error}");
                    _ambientSpring = null;
                }
            }

            // Create and start ambient friction effect
            if (_ambientFrictionPct > 0)
            {
                error = ERRORCODE.NORMAL;
                _ambientFriction = createWheelbaseETFriction(IntPtr.Zero, ref error);
                if (error == ERRORCODE.NORMAL && _ambientFriction != null)
                {
                    var coeff = (long)(_ambientFrictionPct / 100.0 * (long)Effect.DI_FFNOMINALMAX);
                    _ambientFriction.setPositiveCoefficient(coeff);
                    _ambientFriction.setNegativeCoefficient(coeff);
                    _ambientFriction.setPositiveSaturation(Effect.DI_FFNOMINALMAX);
                    _ambientFriction.setNegativeSaturation(Effect.DI_FFNOMINALMAX);
                    _ambientFriction.setOffset(0);
                    _ambientFriction.setDeadBand(0);
                    _ambientFriction.setDuration(0);
                    _ambientFriction.setGain(Effect.DI_FFNOMINALMAX);
                    _ambientFriction.start();
                    Log($"MozaHelper: ambient friction started (coefficient={coeff})");
                }
                else
                {
                    Log($"MozaHelper: failed to create ambient friction: {error}");
                    _ambientFriction = null;
                }
            }

            // Create and start ambient damper effect
            if (_ambientDamperPct > 0)
            {
                error = ERRORCODE.NORMAL;
                _ambientDamper = createWheelbaseETDamper(IntPtr.Zero, ref error);
                if (error == ERRORCODE.NORMAL && _ambientDamper != null)
                {
                    var coeff = (long)(_ambientDamperPct / 100.0 * (long)Effect.DI_FFNOMINALMAX);
                    _ambientDamper.setPositiveCoefficient(coeff);
                    _ambientDamper.setNegativeCoefficient(coeff);
                    _ambientDamper.setPositiveSaturation(Effect.DI_FFNOMINALMAX);
                    _ambientDamper.setNegativeSaturation(Effect.DI_FFNOMINALMAX);
                    _ambientDamper.setOffset(0);
                    _ambientDamper.setDeadBand(0);
                    _ambientDamper.setDuration(0);
                    _ambientDamper.setGain(Effect.DI_FFNOMINALMAX);
                    _ambientDamper.start();
                    Log($"MozaHelper: ambient damper started (coefficient={coeff})");
                }
                else
                {
                    Log($"MozaHelper: failed to create ambient damper: {error}");
                    _ambientDamper = null;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"MozaHelper: failed to initialize ambient effects: {ex.Message}");
        }
    }

    private static void StopAmbientEffects()
    {
        try { _ambientSpring?.stop(); } catch { }
        try { _ambientFriction?.stop(); } catch { }
        try { _ambientDamper?.stop(); } catch { }
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

            case "friction":
                var friction = Math.Clamp(int.Parse(value), 0, 100);
                error = setMotorNaturalFriction(friction);
                ThrowIfError(error, "setMotorNaturalFriction");
                break;

            case "speed-damping-start":
                var sdStart = Math.Clamp(int.Parse(value), 0, 100);
                error = setMotorSpeedDampingStartPoint(sdStart);
                ThrowIfError(error, "setMotorSpeedDampingStartPoint");
                break;

            case "hands-off":
                var handsOff = Math.Clamp(int.Parse(value), 0, 100);
                error = setMotorHandsOffProtection(handsOff);
                ThrowIfError(error, "setMotorHandsOffProtection");
                break;

            case "ffb-enhance":
                _ffbEnhanceEnabled = bool.TryParse(value, out var enhance) && enhance;
                break;

            case "ffb-frequency":
                _ffbFrequencyMs = Math.Clamp(int.Parse(value), 10, 200);
                break;

            case "ambient":
                _ambientEnabled = bool.TryParse(value, out var amb) && amb;
                break;

            case "ambient-spring":
                _ambientSpringPct = Math.Clamp(int.Parse(value), 0, 100);
                break;

            case "ambient-friction":
                _ambientFrictionPct = Math.Clamp(int.Parse(value), 0, 100);
                break;

            case "ambient-damper":
                _ambientDamperPct = Math.Clamp(int.Parse(value), 0, 100);
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
