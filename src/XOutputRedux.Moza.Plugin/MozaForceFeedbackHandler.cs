using System.Diagnostics;
using XOutputRedux.Core.Plugins;

namespace XOutputRedux.Moza.Plugin;

/// <summary>
/// Routes force feedback values to the MozaHelper process via stdin commands.
/// The helper manages an ETSine effect on the wheel for vibration-style rumble.
/// </summary>
internal class MozaForceFeedbackHandler : IForceFeedbackHandler
{
    private readonly Process _helperProcess;
    private readonly object _lock = new();
    private bool _stopped;

    public MozaForceFeedbackHandler(Process helperProcess)
    {
        _helperProcess = helperProcess;
    }

    public void SendForceFeedback(double value)
    {
        lock (_lock)
        {
            if (_stopped || _helperProcess.HasExited)
                return;

            try
            {
                // Send FFB value as a command line to the helper's stdin.
                // Format: "ffb:<value>" where value is 0.000-1.000
                _helperProcess.StandardInput.WriteLine($"ffb:{value:F3}");
                _helperProcess.StandardInput.Flush();
            }
            catch
            {
                // Process may have exited — ignore
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_stopped)
                return;
            _stopped = true;

            try
            {
                if (!_helperProcess.HasExited)
                {
                    _helperProcess.StandardInput.WriteLine("ffb-stop");
                    _helperProcess.StandardInput.Flush();
                }
            }
            catch
            {
                // Process may have exited — ignore
            }
        }
    }
}
