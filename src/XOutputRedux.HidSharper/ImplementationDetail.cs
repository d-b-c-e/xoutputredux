#region License
/* Copyright 2017 James F. Bellinger <http://www.zer7.com/software/hidsharp>

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

using System;

namespace XOutputRedux.HidSharper
{
    /// <summary>
    /// Specifies the <see cref="Device"/>'s low-level implementation.
    /// </summary>
    public static class ImplementationDetail
    {
        /// <summary>
        /// The device is running on Windows.
        /// </summary>
        public static Guid Windows { get; private set; }

        /// <summary>
        /// The device is a HID device.
        /// </summary>
        public static Guid HidDevice { get; private set; }

        static ImplementationDetail()
        {
            Windows = new Guid("{3540D886-E329-419F-8033-1D7355D53A7E}");
            HidDevice = new Guid("{DFF209D7-131E-4958-8F47-C23DAC7B62DA}");
        }
    }
}
