using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using XOutputRedux.HidHide;

namespace XOutputRedux.App;

/// <summary>
/// Standalone HidHide visibility/control panel — shows the cloak state, driver state,
/// which gaming devices HidHide can see (and which are hidden), and the whitelisted apps,
/// with per-device hide/unhide and an "unhide all" reset. Works independently of any
/// running profile, so a user can see and fix HidHide state directly.
/// </summary>
public partial class HidHideManagerWindow : Window
{
    private readonly HidHideService _svc;

    public HidHideManagerWindow(HidHideService svc)
    {
        InitializeComponent();
        _svc = svc;
        RefreshState();
    }

    private sealed class DeviceRow
    {
        public string Product { get; init; } = "";
        public bool Present { get; init; }
        public bool Hidden { get; init; }
        public string Path { get; init; } = "";
        public string PresentText => Present ? "Yes" : "no";
        public string HiddenText => Hidden ? "HIDDEN" : "";
    }

    private void RefreshState()
    {
        try
        {
            var hidden = new HashSet<string>(_svc.GetHiddenDevices(), StringComparer.OrdinalIgnoreCase);

            var rows = _svc.GetGamingDevices()
                .Select(d =>
                {
                    string path = HidHideDevice.EffectivePath(d) ?? "";
                    bool isHidden =
                        (d.DeviceInstancePath != null && hidden.Contains(d.DeviceInstancePath))
                        || (d.BaseContainerDeviceInstancePath != null && hidden.Contains(d.BaseContainerDeviceInstancePath))
                        || (d.SymbolicLink != null && hidden.Contains(d.SymbolicLink));
                    return new DeviceRow
                    {
                        Product = d.Product ?? d.Description ?? "(unknown)",
                        Present = d.Present,
                        Hidden = isHidden,
                        Path = path,
                    };
                })
                .OrderByDescending(r => r.Present)
                .ThenBy(r => r.Product)
                .ToList();
            DeviceList.ItemsSource = rows;

            bool? cloak = _svc.IsCloakingEnabled();
            CloakStateText.Text = cloak == null ? "unknown" : (cloak.Value ? "ON (devices hidden)" : "off");
            CloakToggleButton.Content = (cloak == true) ? "Turn OFF" : "Turn ON";
            DriverStateText.Text = _svc.IsDriverRunning() ? "running" : "stopped";
            WhitelistBox.ItemsSource = _svc.GetWhitelistedApplications().ToList();
            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
    }

    private DeviceRow? Selected => DeviceList.SelectedItem as DeviceRow;

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshState();

    private void CloakToggle_Click(object sender, RoutedEventArgs e)
    {
        bool? cloak = _svc.IsCloakingEnabled();
        bool ok = cloak == true ? _svc.DisableCloaking() : _svc.EnableCloaking();
        StatusText.Text = ok ? "Cloak toggled." : "Toggle failed — run XOutputRedux as administrator.";
        RefreshState();
    }

    private void HideSelected_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null || string.IsNullOrEmpty(s.Path)) { StatusText.Text = "Select a device first."; return; }
        StatusText.Text = _svc.HideDevice(s.Path) ? $"Hid {s.Product}." : "Hide failed — needs admin.";
        RefreshState();
    }

    private void UnhideSelected_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null || string.IsNullOrEmpty(s.Path)) { StatusText.Text = "Select a device first."; return; }
        StatusText.Text = _svc.UnhideDevice(s.Path) ? $"Unhid {s.Product}." : "Unhide failed — needs admin.";
        RefreshState();
    }

    private void UnhideAll_Click(object sender, RoutedEventArgs e)
    {
        int n = 0;
        foreach (var path in _svc.GetHiddenDevices().ToList())
            if (_svc.UnhideDevice(path)) n++;
        _svc.DisableCloaking();
        StatusText.Text = $"Unhid {n} device(s) and turned cloaking off.";
        RefreshState();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
