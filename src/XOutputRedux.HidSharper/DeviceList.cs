#region License
/* Copyright 2015-2016, 2018-2019 James F. Bellinger <http://www.zer7.com/software/hidsharp>

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
using System.Linq;
using System.Runtime.InteropServices;

namespace XOutputRedux.HidSharper
{
    /// <summary>
    /// Provides a list of all available HID devices.
    /// </summary>
    [ComVisible(true), Guid("80614F94-0742-4DE4-8AE9-DF9D55F870F2")]
    public abstract class DeviceList
    {
        /// <summary>
        /// Occurs when a device is connected or disconnected.
        /// </summary>
        public event EventHandler<DeviceListChangedEventArgs>? Changed;

        static DeviceList()
        {
            Local = new LocalDeviceList();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceList"/> class.
        /// </summary>
        protected DeviceList()
        {
        }

        /// <summary>
        /// Gets a list of all connected HID devices.
        /// </summary>
        /// <returns>The device list.</returns>
        public IEnumerable<HidDevice> GetHidDevices()
        {
            return GetAllDevices().OfType<HidDevice>();
        }

        /// <summary>
        /// Gets a list of connected HID devices, filtered by some criteria.
        /// </summary>
        /// <param name="vendorID">The vendor ID, or null to not filter by vendor ID.</param>
        /// <param name="productID">The product ID, or null to not filter by product ID.</param>
        /// <param name="releaseNumberBcd">The device release number in binary-coded decimal, or null to not filter by device release number.</param>
        /// <param name="serialNumber">The serial number, or null to not filter by serial number.</param>
        /// <returns>The filtered device list.</returns>
        public IEnumerable<HidDevice> GetHidDevices(int? vendorID = null, int? productID = null, int? releaseNumberBcd = null, string? serialNumber = null)
        {
            return GetAllDevices().OfType<HidDevice>().Where(d =>
                DeviceFilterHelper.MatchHidDevices(d, vendorID, productID, releaseNumberBcd, serialNumber));
        }

        /// <summary>
        /// Gets a list of all connected HID devices.
        /// </summary>
        /// <returns>The device list.</returns>
        public abstract IEnumerable<Device> GetAllDevices();

        /// <summary>
        /// Gets a list of connected devices, filtered by some criteria.
        /// </summary>
        /// <param name="filter">The filter criteria.</param>
        /// <returns>The filtered device list.</returns>
        public IEnumerable<Device> GetAllDevices(DeviceFilter filter)
        {
            Throw.If.Null(filter, "filter");
            return GetAllDevices().Where(device => filter(device));
        }

        /// <summary>
        /// Gets the first connected HID device that matches specified criteria.
        /// </summary>
        /// <param name="vendorID">The vendor ID, or null to not filter by vendor ID.</param>
        /// <param name="productID">The product ID, or null to not filter by product ID.</param>
        /// <param name="releaseNumberBcd">The device release number in binary-coded decimal, or null to not filter by device release number.</param>
        /// <param name="serialNumber">The serial number, or null to not filter by serial number.</param>
        /// <returns>The device, or null if none was found.</returns>
        public HidDevice? GetHidDeviceOrNull(int? vendorID = null, int? productID = null, int? releaseNumberBcd = null, string? serialNumber = null)
        {
            return GetHidDevices(vendorID, productID, releaseNumberBcd, serialNumber).FirstOrDefault();
        }

        public bool TryGetHidDevice(out HidDevice? device, int? vendorID = null, int? productID = null, int? releaseNumberBcd = null, string? serialNumber = null)
        {
            device = GetHidDeviceOrNull(vendorID, productID, releaseNumberBcd, serialNumber);
            return device != null;
        }

        /// <summary>
        /// Raises the <see cref="Changed"/> event.
        /// </summary>
        public void RaiseChanged()
        {
            Changed?.Invoke(this, new DeviceListChangedEventArgs());
        }

        /// <summary>
        /// <c>true</c> if drivers are presently being installed.
        /// </summary>
        public abstract bool AreDriversBeingInstalled { get; }

        /// <summary>
        /// The list of devices on this computer.
        /// </summary>
        public static DeviceList Local { get; private set; }
    }
}
