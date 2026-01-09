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

    private void PollLoop()
    {
        try
        {
            while (_running && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!_inputReceiver.IsRunning)
                    {
                        // Receiver stopped - device likely disconnected
                        _running = false;
                        Disconnected?.Invoke(this, EventArgs.Empty);
                        break;
                    }

                    ProcessReports();
                }
                catch (Exception)
                {
                    // Device error - likely disconnected
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
    }

    private void ProcessReports()
    {
        var changedUsages = new Dictionary<Usage, DataValue>();

        // Process up to MaxReportsPerLoop reports to prevent queue backup
        for (int i = 0; i < MaxReportsPerLoop && _inputReceiver.TryRead(_inputBuffer, 0, out var report); i++)
        {
            if (_inputParser.TryParseReport(_inputBuffer, 0, report))
            {
                while (_inputParser.HasChanged)
                {
                    int changedIndex = _inputParser.GetNextChangedIndex();
                    var dataValue = _inputParser.GetValue(changedIndex);

                    var usages = dataValue.Usages.ToList();
                    if (usages.Count > 0)
                    {
                        changedUsages[(Usage)usages[0]] = dataValue;
                    }
                }
            }
        }

        // Update sources with changed values
        if (changedUsages.Count > 0)
        {
            foreach (var source in _sources)
            {
                double oldValue = source.Value;
                if (source.Refresh(changedUsages))
                {
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
