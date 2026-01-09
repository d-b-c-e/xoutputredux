using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;

namespace XOutputRenew.Input.RawInput;

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
    public event EventHandler? Disconnected;

    private readonly HidDevice _device;
    private readonly HidStream _hidStream;
    private readonly HidDeviceInputReceiver _inputReceiver;
    private readonly DeviceItemInputParser _inputParser;
    private readonly byte[] _inputBuffer;
    private readonly RawInputSource[] _sources;

    private Thread? _pollThread;
    private CancellationTokenSource? _cts;
    private bool _running;
    private bool _disposed;

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

        // Start receiving input
        _inputReceiver.Start(hidStream);

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
        _cts = new CancellationTokenSource();
        _pollThread = new Thread(PollLoop)
        {
            Name = $"RawInput-{Name}",
            IsBackground = true
        };
        _pollThread.Start();

        InputLogger.Log($"Started: {Name} (Sources: {_sources.Length}, ReceiverRunning: {_inputReceiver.IsRunning})");
    }

    public void Stop()
    {
        if (!_running) return;

        _running = false;
        _cts?.Cancel();

        // Wait for thread to finish (with timeout)
        _pollThread?.Join(500);
        _cts?.Dispose();
        _cts = null;
        _pollThread = null;
    }

    private int _pollCount;
    private DateTime _lastPollLog = DateTime.MinValue;

    private void PollLoop()
    {
        InputLogger.Log($"[{Name}] Poll loop started");
        try
        {
            while (_running && !_cts.Token.IsCancellationRequested)
            {
                _pollCount++;
                try
                {
                    if (!_inputReceiver.IsRunning)
                    {
                        InputLogger.Log($"[{Name}] Receiver stopped - disconnecting");
                        _running = false;
                        Disconnected?.Invoke(this, EventArgs.Empty);
                        break;
                    }

                    ProcessReports();

                    // Log poll activity periodically
                    if ((DateTime.Now - _lastPollLog).TotalSeconds >= 5)
                    {
                        _lastPollLog = DateTime.Now;
                        InputLogger.Log($"[{Name}] Poll loop alive: {_pollCount} iterations, {_reportCount} reports received");
                    }
                }
                catch (Exception ex)
                {
                    InputLogger.Log($"[{Name}] Poll error: {ex.Message}");
                    _running = false;
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    break;
                }

                Thread.Sleep(PollingIntervalMs);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        InputLogger.Log($"[{Name}] Poll loop ended");
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
                InputLogger.Log($"[{Name}] Parse/process error for report {report.ReportID}: {ex.Message}");
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
