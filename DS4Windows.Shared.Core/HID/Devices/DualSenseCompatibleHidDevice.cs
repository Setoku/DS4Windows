﻿using System;
using Microsoft.Extensions.Logging;

namespace DS4Windows.Shared.Core.HID.Devices
{
    public class DualSenseCompatibleHidDevice : CompatibleHidDevice
    {
        private const byte SerialFeatureId = 9;

        public DualSenseCompatibleHidDevice(InputDeviceType deviceType, HidDevice source,
            CompatibleHidDeviceFeatureSet featureSet, IServiceProvider serviceProvider) : base(deviceType, source,
            featureSet, serviceProvider)
        {
        }

        public sealed override void PopulateSerial()
        {
            try
            {
                OpenDevice();
                Serial = ReadSerial(SerialFeatureId);

                Logger.LogInformation("Got serial {Serial} for {Device}", Serial, this);
            }
            finally
            {
                CloseDevice();
            }
        }
    }
}