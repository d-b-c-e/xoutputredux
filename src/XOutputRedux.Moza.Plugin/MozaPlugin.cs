using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using XOutputRedux.Core.Plugins;

namespace XOutputRedux.Moza.Plugin;

public class MozaPlugin : IXOutputPlugin
{
    public string Id => "moza";
    public string DisplayName => "Moza Wheel";

    /// <summary>
    /// Optional logging callback set by the host application.
    /// </summary>
    public static Action<string>? Log { get; set; }

    private MozaEditorTab? _editorTab;
    private Process? _helperProcess;
    private MozaForceFeedbackHandler? _ffbHandler;

    // Axis auto-scaling fields
    private int? _firstSeenRefRotation;
    private double? _axisScaleMin;
    private double? _axisScaleMax;

    public bool Initialize()
    {
        return true;
    }

    public object? CreateEditorTab(JsonObject? pluginData, bool readOnly)
    {
        // Use a short-lived SDK session to read current wheel values for slider defaults.
        // Reads are safe in-process — only writes affect DirectInput calibration.
        MozaDevice? reader = null;
        try
        {
            reader = new MozaDevice();
            if (reader.Initialize())
            {
                Log?.Invoke("Moza editor: reading current wheel values");
            }
            else
            {
                reader = null;
                Log?.Invoke("Moza editor: SDK init failed, using defaults");
            }
        }
        catch
        {
            reader?.Dispose();
            reader = null;
        }

        _editorTab = new MozaEditorTab(pluginData, readOnly, reader);
        var tab = _editorTab.CreateTab();

        // Release the SDK session immediately
        if (reader != null)
        {
            reader.Dispose();
            Log?.Invoke("Moza editor: SDK session released");
        }

        return tab;
    }

    public JsonObject? GetEditorData()
    {
        return _editorTab?.GetData();
    }

    public void OnProfileStart(JsonObject? pluginData)
    {
        if (pluginData == null)
            return;

        var enabled = pluginData["enabled"]?.GetValue<bool>() ?? false;
        if (!enabled)
            return;

        // Kill any previous helper that's still running
        StopHelper();

        Log?.Invoke("Moza OnProfileStart: applying settings via helper exe...");

        try
        {
            // Build command-line arguments from plugin data
            var args = BuildHelperArgs(pluginData);
            if (string.IsNullOrEmpty(args))
            {
                Log?.Invoke("Moza: no settings to apply");
                return;
            }

            // Locate MozaHelper.exe next to this plugin DLL
            var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var helperPath = Path.Combine(pluginDir!, "MozaHelper.exe");

            if (!File.Exists(helperPath))
            {
                Log?.Invoke($"Moza: helper exe not found at {helperPath}");
                return;
            }

            Log?.Invoke($"Moza: launching {helperPath} {args}");

            var psi = new ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = pluginDir!
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                Log?.Invoke("Moza: failed to start helper exe");
                return;
            }

            // Read output until we see the "settings applied" confirmation
            // or the process exits (failure). The helper stays alive after
            // applying settings to keep the SDK session open.
            var settingsApplied = false;
            var deadline = DateTime.UtcNow.AddSeconds(30);

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    Log?.Invoke($"MozaHelper stderr: {e.Data}");
            };
            process.BeginErrorReadLine();

            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    // Drain remaining output
                    var remaining = process.StandardOutput.ReadToEnd();
                    if (!string.IsNullOrEmpty(remaining))
                    {
                        foreach (var line in remaining.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            Log?.Invoke(line.TrimEnd('\r'));
                    }
                    Log?.Invoke($"Moza: helper exe exited early with code {process.ExitCode}");
                    process.Dispose();
                    return;
                }

                var outputLine = process.StandardOutput.ReadLine();
                if (outputLine != null)
                {
                    Log?.Invoke(outputLine);

                    // Parse reference rotation from helper output
                    const string refPrefix = "ref-rotation=";
                    var refIdx = outputLine.IndexOf(refPrefix);
                    if (refIdx >= 0 && int.TryParse(outputLine[(refIdx + refPrefix.Length)..], out var refRot))
                    {
                        _firstSeenRefRotation ??= refRot;
                    }

                    if (outputLine.Contains("settings applied"))
                    {
                        settingsApplied = true;
                        break;
                    }
                }
            }

            if (!settingsApplied)
            {
                Log?.Invoke("Moza: helper exe timed out waiting for settings, killing");
                try { process.Kill(); } catch { }
                process.Dispose();
                return;
            }

            // Calculate axis auto-scaling based on reference vs target rotation.
            // The reference rotation is the rotation at which the device maps its
            // full HID axis range (0-65535). When the target rotation is smaller,
            // the axis only uses a fraction of that range and needs scaling.
            _axisScaleMin = null;
            _axisScaleMax = null;
            var targetRotation = pluginData["wheelRotation"]?.GetValue<int>();
            if (targetRotation.HasValue && _firstSeenRefRotation.HasValue && _firstSeenRefRotation.Value > 0)
            {
                var ratio = (double)targetRotation.Value / _firstSeenRefRotation.Value;
                if (ratio < 1.0)
                {
                    _axisScaleMin = 0.5 - (0.5 * ratio);
                    _axisScaleMax = 0.5 + (0.5 * ratio);
                    Log?.Invoke($"Moza: axis auto-scale: ratio={ratio:F3}, range={_axisScaleMin:F3}-{_axisScaleMax:F3}");
                }
                else
                {
                    Log?.Invoke($"Moza: no axis scaling needed (target={targetRotation.Value} >= ref={_firstSeenRefRotation.Value})");
                }
            }

            // Helper is now keeping the SDK alive. Store the process so we
            // can kill it on profile stop (closing stdin triggers cleanup).
            _helperProcess = process;
            Log?.Invoke("Moza: helper running in background (SDK session alive)");

            // Create FFB handler if the Moza FFB enhancement is enabled.
            // This lets the ForceFeedbackService route rumble through the
            // Moza SDK (ETSine effect) instead of DirectInput ConstantForce.
            var ffbEnhance = pluginData["ffbEnhancement"]?.GetValue<bool>() ?? false;
            if (ffbEnhance)
            {
                _ffbHandler = new MozaForceFeedbackHandler(process);
                Log?.Invoke("Moza: FFB enhancement enabled — rumble will use ETSine effect");
            }
            else
            {
                _ffbHandler = null;
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Moza: failed to apply settings: {ex.Message}");
        }
    }

    private static string BuildHelperArgs(JsonObject data)
    {
        var sb = new StringBuilder();

        AppendIntArg(sb, data, "wheelRotation", "rotation");
        AppendIntArg(sb, data, "ffbStrength", "ffb");
        AppendIntArg(sb, data, "maxTorque", "torque");
        AppendIntArg(sb, data, "damping", "damping");
        AppendIntArg(sb, data, "springStrength", "spring");
        AppendIntArg(sb, data, "naturalInertia", "inertia");
        AppendIntArg(sb, data, "speedDamping", "speed-damping");
        AppendIntArg(sb, data, "naturalFriction", "friction");
        AppendIntArg(sb, data, "speedDampingStartPoint", "speed-damping-start");
        AppendIntArg(sb, data, "handsOffProtection", "hands-off");

        // FFB enhancement settings
        var ffbEnhance = data["ffbEnhancement"]?.GetValue<bool>();
        if (ffbEnhance == true)
        {
            sb.Append(" --ffb-enhance true");
            AppendIntArg(sb, data, "ffbFrequency", "ffb-frequency");
        }

        // Ambient effects settings
        var ambientEnabled = data["ambientEffects"]?.GetValue<bool>();
        if (ambientEnabled == true)
        {
            sb.Append(" --ambient true");
            AppendIntArg(sb, data, "ambientSpring", "ambient-spring");
            AppendIntArg(sb, data, "ambientFriction", "ambient-friction");
            AppendIntArg(sb, data, "ambientDamper", "ambient-damper");
        }

        var ffbReverse = data["ffbReverse"]?.GetValue<bool>();
        if (ffbReverse.HasValue)
            sb.Append($" --reverse {ffbReverse.Value.ToString().ToLowerInvariant()}");

        return sb.ToString().Trim();
    }

    private static void AppendIntArg(StringBuilder sb, JsonObject data, string jsonKey, string argKey)
    {
        var value = data[jsonKey]?.GetValue<int>();
        if (value.HasValue)
            sb.Append($" --{argKey} {value.Value}");
    }

    public void OnProfileStop()
    {
        _ffbHandler = null;
        StopHelper();
        _firstSeenRefRotation = null;
        _axisScaleMin = null;
        _axisScaleMax = null;
    }

    public IReadOnlyList<AxisRangeOverride>? GetAxisRangeOverrides()
    {
        if (_axisScaleMin == null || _axisScaleMax == null)
            return null;

        return new[]
        {
            new AxisRangeOverride("VID_346E&PID_0006", 0, _axisScaleMin.Value, _axisScaleMax.Value)
        };
    }

    public IForceFeedbackHandler? GetForceFeedbackHandler()
    {
        return _ffbHandler;
    }

    public void Dispose()
    {
        StopHelper();
    }

    private void StopHelper()
    {
        var process = _helperProcess;
        _helperProcess = null;

        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
            {
                // Close stdin — the helper blocks on ReadToEnd() and will
                // call removeMozaSDK() then exit cleanly.
                Log?.Invoke("Moza: closing helper stdin to trigger cleanup...");
                process.StandardInput.Close();

                if (!process.WaitForExit(5000))
                {
                    Log?.Invoke("Moza: helper did not exit in 5s, killing");
                    process.Kill();
                }
                else
                {
                    Log?.Invoke("Moza: helper exited cleanly");
                }
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Moza: error stopping helper: {ex.Message}");
            try { process.Kill(); } catch { }
        }
        finally
        {
            process.Dispose();
        }
    }
}
