#region License
/* Copyright 2012-2013 James F. Bellinger <http://www.zer7.com/software/hidsharp>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing,
   software distributed under the License is distributed on an
   "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
   KIND, either express or implied.  See the License for the
   specific language governing permissions and limitations
   under the License.

   Modified for HidSharper - BLE and Serial support removed. */
#endregion

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using XOutputRedux.HidSharper.Utility;

namespace XOutputRedux.HidSharper.Platform.Windows
{
    sealed class WinHidManager : HidManager
    {
        #region Device Paths
        sealed class HidDevicePath
        {
            public override bool Equals(object? obj)
            {
                var path = obj as HidDevicePath;
                return path != null && DevicePath == path.DevicePath && DeviceID == path.DeviceID;
            }

            public override int GetHashCode()
            {
                return DevicePath.GetHashCode();
            }

            public override string ToString()
            {
                return DevicePath;
            }

            public string DevicePath = string.Empty;
            public string DeviceID = string.Empty;
        }
        #endregion

        bool _isSupported;

        static Thread? _notifyThread;
        static bool _notifyThreadShouldNotify;
        static bool _notifyThreadShuttingDown;

        static object? _hidNotifyObject;

        static object[]? _hidDeviceKeysCache;
        static object? _hidDeviceKeysCacheNotifyObject;

        public WinHidManager()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var version = new NativeMethods.OSVERSIONINFO();
                version.OSVersionInfoSize = Marshal.SizeOf(typeof(NativeMethods.OSVERSIONINFO));

                try
                {
                    if (NativeMethods.GetVersionEx(ref version) && version.PlatformID == 2)
                    {
                        _isSupported = true;
                    }
                }
                catch
                {
                    // Apparently we have no P/Invoke access.
                }
            }
        }

        protected override void Run(Action readyCallback)
        {
            const string className = "HidSharpDeviceMonitor";

            NativeMethods.WindowProc windowProc = DeviceMonitorWindowProc;
            var wc = new NativeMethods.WNDCLASS() { ClassName = className, WindowProc = windowProc };
            RunAssert(0 != NativeMethods.RegisterClass(ref wc), "HidSharp RegisterClass failed.");

            var hwnd = NativeMethods.CreateWindowEx(0, className, className, 0,
                                                    NativeMethods.CW_USEDEFAULT, NativeMethods.CW_USEDEFAULT, NativeMethods.CW_USEDEFAULT, NativeMethods.CW_USEDEFAULT,
                                                    NativeMethods.HWND_MESSAGE,
                                                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            RunAssert(hwnd != IntPtr.Zero, "HidSharp CreateWindow failed.");

            var hidNotifyHandle = RegisterDeviceNotification(hwnd, NativeMethods.HidD_GetHidGuid());

            _hidNotifyObject = new object();
            _notifyThread = new Thread(DeviceMonitorEventThread) { IsBackground = true, Name = "HidSharp RaiseChanged" };
            _notifyThread.Start();

            readyCallback();

            NativeMethods.MSG msg;
            while (true)
            {
                int result = NativeMethods.GetMessage(out msg, hwnd, 0, 0);
                if (result == 0 || result == -1) { break; }

                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            lock (_notifyThread) { _notifyThreadShuttingDown = true; Monitor.Pulse(_notifyThread); }
            _notifyThread.Join();

            UnregisterDeviceNotification(hidNotifyHandle);

            RunAssert(NativeMethods.DestroyWindow(hwnd), "HidSharp DestroyWindow failed.");
            RunAssert(NativeMethods.UnregisterClass(className, IntPtr.Zero), "HidSharp UnregisterClass failed.");
            GC.KeepAlive(windowProc);
        }

        static IntPtr RegisterDeviceNotification(IntPtr hwnd, Guid guid)
        {
            var notifyFilter = new NativeMethods.DEV_BROADCAST_DEVICEINTERFACE()
            {
                Size = Marshal.SizeOf(typeof(NativeMethods.DEV_BROADCAST_DEVICEINTERFACE)),
                ClassGuid = guid,
                DeviceType = NativeMethods.DBT_DEVTYP_DEVICEINTERFACE
            };
            var notifyHandle = NativeMethods.RegisterDeviceNotification(hwnd, ref notifyFilter, 0);
            RunAssert(notifyHandle != IntPtr.Zero, "HidSharp RegisterDeviceNotification failed.");
            return notifyHandle;
        }

        static void UnregisterDeviceNotification(IntPtr handle)
        {
            RunAssert(NativeMethods.UnregisterDeviceNotification(handle), "HidSharp UnregisterDeviceNotification failed.");
        }

        unsafe static IntPtr DeviceMonitorWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == NativeMethods.WM_DEVICECHANGE)
            {
                var ev = (NativeMethods.WM_DEVICECHANGE_wParam)(int)(long)wParam;
                HidSharpDiagnostics.Trace("Received a device change event, {0}.", ev);

                var eventArgs = (NativeMethods.DEV_BROADCAST_HDR*)(void*)lParam;

                if (ev == NativeMethods.WM_DEVICECHANGE_wParam.DBT_DEVICEARRIVAL || ev == NativeMethods.WM_DEVICECHANGE_wParam.DBT_DEVICEREMOVECOMPLETE)
                {
                    if (eventArgs->DeviceType == NativeMethods.DBT_DEVTYP_DEVICEINTERFACE)
                    {
                        var diEventArgs = (NativeMethods.DEV_BROADCAST_DEVICEINTERFACE*)eventArgs;

                        if (diEventArgs->ClassGuid == NativeMethods.HidD_GetHidGuid())
                        {
                            DeviceListDidChange(ref _hidNotifyObject);
                        }
                    }
                }

                return (IntPtr)1;
            }

            return NativeMethods.DefWindowProc(window, message, wParam, lParam);
        }

        static void DeviceListDidChange(ref object? notifyObject)
        {
            if (_notifyThread == null) return;
            lock (_notifyThread)
            {
                notifyObject = new object();
                _notifyThreadShouldNotify = true;
                Monitor.Pulse(_notifyThread);
            }
        }

        static void DeviceMonitorEventThread()
        {
            if (_notifyThread == null) return;
            lock (_notifyThread)
            {
                while (true)
                {
                    if (_notifyThreadShuttingDown)
                    {
                        break;
                    }
                    else if (_notifyThreadShouldNotify)
                    {
                        _notifyThreadShouldNotify = false;

                        Monitor.Exit(_notifyThread);
                        try
                        {
                            DeviceList.Local.RaiseChanged();
                        }
                        finally
                        {
                            Monitor.Enter(_notifyThread);
                        }
                    }
                    else
                    {
                        Monitor.Wait(_notifyThread);
                    }
                }
            }
        }

        protected override object[] GetHidDeviceKeys()
        {
            if (_notifyThread == null)
                return Array.Empty<object>();

            object? notifyObject;
            lock (_notifyThread)
            {
                notifyObject = _hidNotifyObject;
                if (notifyObject == _hidDeviceKeysCacheNotifyObject && _hidDeviceKeysCache != null)
                {
                    return _hidDeviceKeysCache;
                }
            }

            var paths = new List<object>();

            var hidGuid = NativeMethods.HidD_GetHidGuid();
            NativeMethods.EnumerateDeviceInterfaces(hidGuid, (_, __, ___, deviceID, devicePath) =>
                {
                    paths.Add(new HidDevicePath()
                    {
                        DeviceID = deviceID!,
                        DevicePath = devicePath!
                    });
                });

            var keys = paths.ToArray();
            lock (_notifyThread)
            {
                _hidDeviceKeysCacheNotifyObject = notifyObject;
                _hidDeviceKeysCache = keys;
            }
            return keys;
        }

        protected override bool TryCreateHidDevice(object key, out Device? device)
        {
            var path = (HidDevicePath)key;
            device = WinHidDevice.TryCreate(path.DevicePath);
            return device != null;
        }

        public override bool AreDriversBeingInstalled
        {
            get
            {
                try
                {
                    return NativeMethods.WAIT_TIMEOUT == NativeMethods.CMP_WaitNoPendingInstallEvents(0);
                }
                catch
                {
                    return false;
                }
            }
        }

        public override string FriendlyName
        {
            get { return "Windows HID"; }
        }

        public override bool IsSupported
        {
            get { return _isSupported; }
        }
    }
}
