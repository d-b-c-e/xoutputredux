using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;

namespace XOutputRedux.Input.RawInput;

/// <summary>
/// RawInput/HID device implementation using HidSharp.
/// Adapted from XOutput.App.Devices.Input.RawInput.RawInputDevice
/// </summary>
public class RawInputDevice : IInputDevice
{
    private const int MaxReportsPerLoop = 10;
    private const int PollingIntervalMs = 1;

    public string UniqueId { get; }
    public string Name { get; }
    public string? HardwareId { get; }
    public string? InterfacePath { get; }
    public InputMethod Method => InputMethod.RawInput;
    public bool IsConnected => !_disposed && _inputReceiver?.IsRunning == true;

    public IReadOnlyList<IInputSource> Sources => _sources;

    public event EventHandler<InputChangedEventArgs>? InputChanged;
#pragma warning disable CS0067 // Event is required by IInputDevice interface but HidSharp doesn't provide disconnect notifications
    public event EventHandler? Disconnected;
#pragma warning restore CS0067

    private readonly HidDevice _device;
    private readonly HidStream _hidStream;
    private readonly HidDeviceInputReceiver _inputReceiver;
    private readonly DeviceItemInputParser _inputParser;
    private readonly byte[] _inputBuffer;
    private readonly RawInputSource[] _sources;

    private bool _running;
    private bool _disposed;

    // Throttle parse error logging - only log each report ID once
    private readonly Dictionary<int, int> _parseErrorCounts = new();

    public RawInputDevice(
        HidDevice device,
        HidStream hidStream,
        ReportDescriptor reportDescriptor,
        DeviceItem deviceItem,
        string uniqueId,
        string? hardwareId)
    {
        _device = device;
        _hidStream = hidStream;
        UniqueId = uniqueId;
        HardwareId = hardwareId;
        InterfacePath = device.DevicePath;

        try
        {
            Name = device.GetProductName();
        }
        catch
        {
            Name = "Unknown HID Device";
        }

        // Set up input receiver and parser
        _inputBuffer = new byte[device.GetMaxInputReportLength()];
        _inputReceiver = reportDescriptor.CreateHidDeviceInputReceiver();
        _inputParser = deviceItem.CreateDeviceItemInputParser();

        // Enumerate sources from report descriptor
        var sources = reportDescriptor.InputReports
            .SelectMany(ir => ir.DataItems)
            .SelectMany(di => di.Usages.GetAllValues())
            .Select(u => (Usage)u)
            .SelectMany(RawInputSource.FromUsage)
            .ToArray();

        _sources = sources;
    }

    public void Start()
    {
        if (_running || _disposed) return;

        _running = true;

        // Use event-based approach for immediate response
        _inputReceiver.Received += InputReceiver_Received;

        // Only start if not already running (receiver persists across Start/Stop cycles)
        if (!_inputReceiver.IsRunning)
        {
            _inputReceiver.Start(_hidStream);
        }

        InputLogger.Log($"Started: {Name} (Sources: {_sources.Length}, ReceiverRunning: {_inputReceiver.IsRunning})");
    }

    public void Stop()
    {
        if (!_running) return;

        _running = false;
        _inputReceiver.Received -= InputReceiver_Received;
    }

    private void InputReceiver_Received(object? sender, EventArgs e)
    {
        if (!_running) return;

        try
        {
            ProcessReports();
        }
        catch (Exception ex)
        {
            InputLogger.Log($"[{Name}] Receive error: {ex.Message}");
        }
    }

    private int _reportCount;
    private DateTime _lastReportLog = DateTime.MinValue;

    private void ProcessReports()
    {
        var changedUsages = new Dictionary<Usage, DataValue>();

        // Process up to MaxReportsPerLoop reports to prevent queue backup
        int reportsRead = 0;
        for (int i = 0; i < MaxReportsPerLoop && _inputReceiver.TryRead(_inputBuffer, 0, out var report); i++)
        {
            reportsRead++;
            _reportCount++;

            InputLogger.Verbose($"[{Name}] Report #{_reportCount}: {report.ReportID}, {_inputBuffer.Length} bytes: {BitConverter.ToString(_inputBuffer, 0, Math.Min(16, _inputBuffer.Length))}");

            try
            {
                bool parsed = _inputParser.TryParseReport(_inputBuffer, 0, report);
                InputLogger.Verbose($"[{Name}] ParseResult={parsed}, HasChanged={_inputParser.HasChanged}");

                if (parsed)
                {
                    int changeCount = 0;
                    while (_inputParser.HasChanged)
                    {
                        changeCount++;
                        int changedIndex = _inputParser.GetNextChangedIndex();
                        var dataValue = _inputParser.GetValue(changedIndex);

                        var usages = dataValue.Usages.ToList();
                        if (usages.Count > 0)
                        {
                            var usage = (Usage)usages[0];
                            changedUsages[usage] = dataValue;
                            InputLogger.Verbose($"[{Name}] Changed[{changeCount}]: Usage={usage} (0x{(uint)usage:X}), Value={dataValue.GetLogicalValue()}");
                        }
                        else
                        {
                            InputLogger.Verbose($"[{Name}] Changed[{changeCount}]: No usages, Index={changedIndex}");
                        }
                    }
                    if (changeCount == 0)
                    {
                        InputLogger.Verbose($"[{Name}] Parsed OK but no changes detected");
                    }
                }
                else
                {
                    InputLogger.Verbose($"[{Name}] Failed to parse report ID {report.ReportID}");
                }
            }
            catch (Exception ex)
            {
                // Throttle error logging - only log first occurrence per report ID
                var reportId = report.ReportID;
                if (!_parseErrorCounts.TryGetValue(reportId, out var count))
                {
                    InputLogger.Log($"[{Name}] Parse error for report {reportId}: {ex.Message} (further errors for this report will be suppressed)");
                    _parseErrorCounts[reportId] = 1;
                }
                else
                {
                    _parseErrorCounts[reportId] = count + 1;
                }
                // Continue processing other reports - don't crash the poll loop
            }
        }

        // Log periodically if we're receiving reports
        if (reportsRead > 0 && (DateTime.Now - _lastReportLog).TotalSeconds >= 5)
        {
            _lastReportLog = DateTime.Now;
            InputLogger.Log($"[{Name}] Total reports received: {_reportCount}");
        }

        // Update sources with changed values
        if (changedUsages.Count > 0)
        {
            InputLogger.Verbose($"[{Name}] Processing {changedUsages.Count} changed usages across {_sources.Length} sources");

            foreach (var source in _sources)
            {
                double oldValue = source.Value;
                if (source.Refresh(changedUsages))
                {
                    InputLogger.Log($"[{Name}] InputChanged: {source.Name} = {source.Value:F3} (was {oldValue:F3})");
                    InputChanged?.Invoke(this, new InputChangedEventArgs
                    {
                        Source = source,
                        OldValue = oldValue,
                        NewValue = source.Value
                    });
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        // Log summary of suppressed parse errors
        if (_parseErrorCounts.Count > 0)
        {
            foreach (var (reportId, count) in _parseErrorCounts)
            {
                if (count > 1)
                {
                    InputLogger.Log($"[{Name}] Report {reportId} had {count} total parse errors (suppressed after first)");
                }
            }
        }

        try
        {
            _inputReceiver.Received -= null; // Clear any handlers
            _hidStream.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        GC.SuppressFinalize(this);
    }
}
