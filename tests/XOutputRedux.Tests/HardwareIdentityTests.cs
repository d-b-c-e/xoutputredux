using XOutputRedux.HidHide;

namespace XOutputRedux.Tests;

/// <summary>
/// Tests for <see cref="HidHideDevice.HardwareIdentity"/> — the keep-list fallback
/// that lets an isolation profile survive a device being replugged or changing
/// XInput slot.
///
/// The cases here are real paths captured from the cabinet, not invented ones. The
/// bug that motivated this: an X-Arcade panel moved from IG_00/01/02 to IG_04/05
/// just from cycling its controller mode, and the saved profile — which stored full
/// instance paths — then matched nothing, so isolation either refused to start or
/// hid the panel the user meant to keep.
/// </summary>
[TestClass]
public class HardwareIdentityTests
{
    [TestMethod]
    public void HidInstancePath_ReducesToVidPid()
    {
        Assert.AreEqual("VID_045E&PID_028E",
            HidHideDevice.HardwareIdentity(@"HID\VID_045E&PID_028E&IG_04\d&20e2af56&0&0000"));
    }

    [TestMethod]
    public void XInputSlotMove_ProducesTheSameIdentity()
    {
        // The whole point: IG_ tracks the XInput slot and moves on its own.
        var before = HidHideDevice.HardwareIdentity(@"HID\VID_045E&PID_028E&IG_00\d&af8d77e&0&0000");
        var after  = HidHideDevice.HardwareIdentity(@"HID\VID_045E&PID_028E&IG_04\d&20e2af56&0&0000");
        Assert.AreEqual(before, after);
        Assert.AreEqual("VID_045E&PID_028E", after);
    }

    [TestMethod]
    public void DifferentPort_ProducesTheSameIdentity()
    {
        // Only the trailing instance segment differs — that is the USB port.
        var portA = HidHideDevice.HardwareIdentity(@"HID\VID_0483&PID_0531\a&509e5f3&0&0000");
        var portB = HidHideDevice.HardwareIdentity(@"HID\VID_0483&PID_0531\b&1c4f0aa1&0&0000");
        Assert.AreEqual(portA, portB);
        Assert.AreEqual("VID_0483&PID_0531", portB);
    }

    [TestMethod]
    public void CompositeDevice_KeepsItsInterfaceQualifier()
    {
        // MI_ identifies a *function* of a composite device and does not move, so it
        // must survive — otherwise a wheel base's pedals interface and its button box
        // would be indistinguishable.
        Assert.AreEqual("VID_346E&PID_0024&MI_02",
            HidHideDevice.HardwareIdentity(@"HID\VID_346E&PID_0024&MI_02\a&3e8e0e5&0&0000"));
    }

    [TestMethod]
    public void CompositeInterfaces_DoNotCollide()
    {
        var mi00 = HidHideDevice.HardwareIdentity(@"HID\VID_346E&PID_0024&MI_00\a&1111111&0&0000");
        var mi02 = HidHideDevice.HardwareIdentity(@"HID\VID_346E&PID_0024&MI_02\a&3e8e0e5&0&0000");
        Assert.AreNotEqual(mi00, mi02);
    }

    [TestMethod]
    public void UsbContainerPath_ReducesToVidPid()
    {
        Assert.AreEqual("VID_045E&PID_028E",
            HidHideDevice.HardwareIdentity(@"USB\VID_045E&PID_028E\B&1491B5EA&0&4"));
    }

    [TestMethod]
    public void SymbolicLink_ReducesToTheSameIdentity()
    {
        // The symbolic-link form is lower case and uses '#' as its separator, and it
        // carries a trailing interface-class GUID that must not leak into the identity.
        Assert.AreEqual("VID_045E&PID_028E",
            HidHideDevice.HardwareIdentity(
                @"\\?\hid#vid_045e&pid_028e&ig_04#d&20e2af56&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
    }

    [TestMethod]
    public void AllThreePathFormsOfOneDevice_Agree()
    {
        var hid  = HidHideDevice.HardwareIdentity(@"HID\VID_045E&PID_028E&IG_04\d&20e2af56&0&0000");
        var usb  = HidHideDevice.HardwareIdentity(@"USB\VID_045E&PID_028E\B&1491B5EA&0&4");
        var link = HidHideDevice.HardwareIdentity(
            @"\\?\hid#vid_045e&pid_028e&ig_04#d&20e2af56&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}");
        Assert.AreEqual(hid, usb);
        Assert.AreEqual(hid, link);
    }

    [TestMethod]
    public void RevisionToken_IsIgnored()
    {
        // REV_ changes with a firmware update; the device is still the same device.
        Assert.AreEqual("VID_346E&PID_0024",
            HidHideDevice.HardwareIdentity(@"USB\VID_346E&PID_0024&REV_0100\360031000E53303756393320"));
    }

    [TestMethod]
    public void PathWithoutVidPid_ReturnsNull()
    {
        // Root-enumerated virtual devices (vJoy, ViGEm pads) have no VID/PID to key on.
        // Returning null keeps them on exact-path matching instead of matching broadly.
        Assert.IsNull(HidHideDevice.HardwareIdentity(@"ROOT\HIDCLASS\0000"));
        Assert.IsNull(HidHideDevice.HardwareIdentity(@"HID\VID_ONLY_NO_PID\a&1&0&0000"));
    }

    [TestMethod]
    public void NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(HidHideDevice.HardwareIdentity(null));
        Assert.IsNull(HidHideDevice.HardwareIdentity(""));
        Assert.IsNull(HidHideDevice.HardwareIdentity("   "));
    }

    [TestMethod]
    public void IdentityIsCaseInsensitiveByNormalisation()
    {
        Assert.AreEqual(
            HidHideDevice.HardwareIdentity(@"HID\VID_045E&PID_028E\a&1&0&0000"),
            HidHideDevice.HardwareIdentity(@"hid\vid_045e&pid_028e\a&1&0&0000"));
    }

    [TestMethod]
    public void IdentityKeys_YieldsBothInterfaceAndContainerLevel()
    {
        // A profile may have saved either form, so a composite device has to answer to
        // both its MI_-qualified identity and its bare container identity.
        var d = new HidHideDevice
        {
            DeviceInstancePath = @"HID\VID_346E&PID_0024&MI_02\a&3e8e0e5&0&0000",
            BaseContainerDeviceInstancePath = @"USB\VID_346E&PID_0024\360031000E53303756393320"
        };

        var keys = HidHideDevice.IdentityKeys(d).ToList();
        CollectionAssert.Contains(keys, "VID_346E&PID_0024&MI_02");
        CollectionAssert.Contains(keys, "VID_346E&PID_0024");
    }

    [TestMethod]
    public void IdentityKeys_DeduplicatesWhenPathsAgree()
    {
        var d = new HidHideDevice
        {
            DeviceInstancePath = @"HID\VID_045E&PID_028E&IG_04\d&20e2af56&0&0000",
            BaseContainerDeviceInstancePath = @"USB\VID_045E&PID_028E\B&1491B5EA&0&4",
            SymbolicLink = @"\\?\hid#vid_045e&pid_028e&ig_04#d&20e2af56&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"
        };

        var keys = HidHideDevice.IdentityKeys(d).ToList();
        Assert.AreEqual(1, keys.Count, "all three paths describe one device, so one identity");
        Assert.AreEqual("VID_045E&PID_028E", keys[0]);
    }

    [TestMethod]
    public void IdentityKeys_SkipsPathsWithNoVidPid()
    {
        var d = new HidHideDevice
        {
            DeviceInstancePath = @"ROOT\HIDCLASS\0000",
            BaseContainerDeviceInstancePath = ""
        };
        Assert.AreEqual(0, HidHideDevice.IdentityKeys(d).Count());
    }
}
