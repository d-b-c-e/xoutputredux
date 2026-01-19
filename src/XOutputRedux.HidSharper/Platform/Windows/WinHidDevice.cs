#region License
/* Copyright 2010-2015, 2017-2018 James F. Bellinger <http://www.zer7.com/software/hidsharp>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing,
   software distributed under the License is distributed on an
   "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
   KIND, either express or implied.  See the License for the
   specific language governing permissions and limitations
   under the License. */
#endregion

#pragma warning disable 618

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace XOutputRedux.HidSharper.Platform.Windows
{
    sealed partial class WinHidDevice : HidDevice
    {
        [Flags]
        enum GetInfoFlags
        {
            Manufacturer = 1,
            ProductName = 2,
            SerialNumber = 4,
            ReportInfo = 8
        }
        GetInfoFlags _getInfoFlags;
        readonly object _getInfoLock = new object();
        string _path = null!, _id = null!;
        string _manufacturer = string.Empty;
        string _productName = string.Empty;
        string _serialNumber = string.Empty;
        int _vid, _pid, _version;
        int _maxInput, _maxOutput, _maxFeature;
        byte[]? _reportDescriptor;

        WinHidDevice()
        {

        }

        internal static WinHidDevice? TryCreate(string path, string id)
        {
            var d = new WinHidDevice() { _path = path, _id = id };

            return d.TryOpenToGetInfo(handle =>
                {
                    NativeMethods.HIDD_ATTRIBUTES attributes = new NativeMethods.HIDD_ATTRIBUTES();
                    attributes.Size = Marshal.SizeOf(attributes);
                    if (!NativeMethods.HidD_GetAttributes(handle, ref attributes)) { return false; }

                    // Get VID, PID, version.
                    d._pid = attributes.ProductID;
                    d._vid = attributes.VendorID;
                    d._version = attributes.VersionNumber;
                    return true;
                }) ? d : null;
        }

        bool TryOpenToGetInfo(Func<IntPtr, bool> action)
        {
            return NativeMethods.TryOpenToGetInfo(_path, action);
        }

        protected override DeviceStream OpenDeviceDirectly(OpenConfiguration openConfig)
        {
            RequiresGetInfo(GetInfoFlags.ReportInfo);

            var stream = new WinHidStream(this);
            try { stream.Init(_path); return stream; }
            catch { stream.Close(); throw; }
        }

        void RequiresGetInfo(GetInfoFlags flags)
        {
            lock (_getInfoLock)
            {
                flags &= ~_getInfoFlags; // Remove flags we already have.
                if (flags == 0) { return; }

                if (!TryOpenToGetInfo(handle =>
                    {
                        if ((flags & GetInfoFlags.Manufacturer) != 0)
                        {
                            if (!TryGetDeviceString(handle, NativeMethods.HidD_GetManufacturerString, out _manufacturer)) { return false; }
                        }

                        if ((flags & GetInfoFlags.ProductName) != 0)
                        {
                            if (!TryGetDeviceString(handle, NativeMethods.HidD_GetProductString, out _productName)) { return false; }
                        }

                        if ((flags & GetInfoFlags.SerialNumber) != 0)
                        {
                            if (!TryGetDeviceString(handle, NativeMethods.HidD_GetSerialNumberString, out _serialNumber)) { return false; }
                        }

                        if ((flags & GetInfoFlags.ReportInfo) != 0)
                        {
                            IntPtr preparsed;
                            if (!NativeMethods.HidD_GetPreparsedData(handle, out preparsed)) { return false; }

                            try
                            {
                                NativeMethods.HIDP_CAPS caps;
                                int statusCaps = NativeMethods.HidP_GetCaps(preparsed, out caps);
                                if (statusCaps != NativeMethods.HIDP_STATUS_SUCCESS) { return false; }

                                _maxInput = caps.InputReportByteLength;
                                _maxOutput = caps.OutputReportByteLength;
                                _maxFeature = caps.FeatureReportByteLength;

                                try { _reportDescriptor = new ReportDescriptorReconstructor().Run(preparsed, caps); }
                                catch (NotImplementedException) { _reportDescriptor = null; }
                                catch { return false; }
                            }
                            finally
                            {
                                NativeMethods.HidD_FreePreparsedData(preparsed);
                            }
                        }

                        return true;
                    }))
                {
                    throw DeviceException.CreateIOException(this, "Failed to get info.");
                }

                _getInfoFlags |= flags;
            }
        }

        bool TryGetDeviceString(IntPtr handle, Func<IntPtr, char[], int, bool> callback, out string s)
        {
            char[] buffer = new char[128];
            if (!callback(handle, buffer, Marshal.SystemDefaultCharSize * buffer.Length))
            {
                s = string.Empty;
                return Marshal.GetLastWin32Error() == NativeMethods.ERROR_GEN_FAILURE;
            }
            s = NativeMethods.NTString(buffer); return true;
        }

        public override string GetManufacturer()
        {
            RequiresGetInfo(GetInfoFlags.Manufacturer);
            return _manufacturer;
        }

        public override string GetProductName()
        {
            RequiresGetInfo(GetInfoFlags.ProductName);
            return _productName;
        }

        public override string GetSerialNumber()
        {
            RequiresGetInfo(GetInfoFlags.SerialNumber);
            return _serialNumber;
        }

        public override int GetMaxInputReportLength()
        {
            RequiresGetInfo(GetInfoFlags.ReportInfo);
            return _maxInput;
        }

        public override int GetMaxOutputReportLength()
        {
            RequiresGetInfo(GetInfoFlags.ReportInfo);
            return _maxOutput;
        }

        public override int GetMaxFeatureReportLength()
        {
            RequiresGetInfo(GetInfoFlags.ReportInfo);
            return _maxFeature;
        }

        public override byte[] GetRawReportDescriptor()
        {
            RequiresGetInfo(GetInfoFlags.ReportInfo);
            var descriptor = _reportDescriptor;
            if (descriptor == null) { throw new NotSupportedException("Unable to reconstruct the report descriptor."); } // TODO: Extend the reconstruction functionality over time...
            return (byte[])descriptor.Clone();
        }

        public override string GetFileSystemName()
        {
            return DevicePath;
        }

        public override bool HasImplementationDetail(Guid detail)
        {
            return base.HasImplementationDetail(detail) || detail == ImplementationDetail.Windows;
        }

        public override string DevicePath
        {
            get { return _path; }
        }

        public override int VendorID
        {
            get { return _vid; }
        }

        public override int ProductID
        {
            get { return _pid; }
        }

        public override int ReleaseNumberBcd
        {
            get { return _version; }
        }
    }
}
