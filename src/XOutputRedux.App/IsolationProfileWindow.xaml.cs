using System.Windows;
using XOutputRedux.Core.HidHide;
using XOutputRedux.Core.Mapping;
using XOutputRedux.HidHide;

namespace XOutputRedux.App;

/// <summary>
/// Editor for device-isolation profiles: pick the gaming device(s) to KEEP
/// visible; everything else is hidden via HidHide while the profile runs.
/// Reuses the HidHide gaming-device enumeration that backs the HidHide Manager.
/// </summary>
public partial class IsolationProfileWindow : Window
{
    private readonly MappingProfile _profile;
    private readonly HidHideService _svc;
    private List<DeviceRow> _rows = new();

    /// <summary>
    /// True if the user saved changes (caller persists the profile).
    /// </summary>
    public bool WasSaved { get; private set; }

    public class DeviceRow
    {
        public bool Keep { get; set; }
        public string Product { get; set; } = "";
        public bool Present { get; set; }
        public string Path { get; set; } = "";
        public string PresentText => Present ? "Yes" : "no (remembered)";
    }

    public IsolationProfileWindow(MappingProfile profile, HidHideService hidHideService)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkModeHelper.EnableDarkTitleBar(this);

        _profile = profile;
        _svc = hidHideService;
        NameText.Text = profile.Name;

        if (!_svc.IsAvailable)
        {
            StatusText.Text = "HidHide is not installed - install it from the Setup tab first.";
        }

        LoadRows();
    }

    /// <summary>
    /// Builds the device rows. On initial load the keep-checks come from the
    /// profile's keep-list; on refresh <paramref name="priorRows"/> carries the
    /// user's in-session check state (the profile object itself is only
    /// mutated on Save, so Cancel never leaves stale edits behind).
    /// </summary>
    private void LoadRows(List<DeviceRow>? priorRows = null)
    {
        var keep = _profile.DeviceIsolation?.KeepDevices ?? new List<IsolationDevice>();
        var keepByPath = new Dictionary<string, IsolationDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in keep)
        {
            if (!string.IsNullOrWhiteSpace(k.DeviceInstancePath))
                keepByPath[k.DeviceInstancePath] = k;
        }

        var priorByPath = priorRows?
            .Where(r => !string.IsNullOrWhiteSpace(r.Path))
            .GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<DeviceRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var d in _svc.GetGamingDevices())
            {
                // Same identity the HidHide Manager shows and Apply() hides:
                // base container path preferred (covers the whole composite device),
                // falling back to the device instance path when the base container is
                // empty (e.g. vJoy/root devices) so they still appear in the picker.
                var path = HidHideDevice.EffectivePath(d) ?? "";
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;

                bool isKept = priorByPath != null && priorByPath.TryGetValue(path, out var prior)
                    ? prior.Keep
                    : keepByPath.ContainsKey(path)
                      || (d.DeviceInstancePath != null && keepByPath.ContainsKey(d.DeviceInstancePath));

                rows.Add(new DeviceRow
                {
                    Keep = isKept,
                    Product = d.Product ?? d.Description ?? "(unknown)",
                    Present = d.Present,
                    Path = path
                });
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error listing devices: " + ex.Message;
        }

        // Rows HidHide can't currently see (device unplugged): in-session rows
        // first (preserving their check state), then profile keep-list entries —
        // so editing a profile never silently drops absent devices.
        if (priorByPath != null)
        {
            foreach (var r in priorByPath.Values)
            {
                if (!seen.Add(r.Path)) continue;
                rows.Add(new DeviceRow { Keep = r.Keep, Product = r.Product, Present = false, Path = r.Path });
            }
        }

        foreach (var k in keep)
        {
            if (string.IsNullOrWhiteSpace(k.DeviceInstancePath)) continue;
            if (!seen.Add(k.DeviceInstancePath)) continue;

            rows.Add(new DeviceRow
            {
                Keep = true,
                Product = k.FriendlyName ?? "(remembered device)",
                Present = false,
                Path = k.DeviceInstancePath
            });
        }

        _rows = rows.OrderByDescending(r => r.Present).ThenBy(r => r.Product).ToList();
        DeviceList.ItemsSource = _rows;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        // Preserve any keep-checks made in this session across re-enumeration
        LoadRows(priorRows: _rows);
        StatusText.Text = "Device list refreshed.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var kept = _rows.Where(r => r.Keep && !string.IsNullOrWhiteSpace(r.Path)).ToList();
        if (kept.Count == 0)
        {
            StatusText.Text = "Check at least one device to keep visible.";
            return;
        }

        _profile.ProfileType = ProfileType.DeviceIsolation;
        _profile.DeviceIsolation = new DeviceIsolationSettings
        {
            Enabled = true,
            KeepDevices = kept
                .Select(r => new IsolationDevice { DeviceInstancePath = r.Path, FriendlyName = r.Product })
                .ToList()
        };
        _profile.ModifiedAt = DateTime.Now;

        WasSaved = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
