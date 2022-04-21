﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DS4Windows.Shared.Common.Types;
using DS4Windows.Shared.Emulator.ViGEmGen1.Types;
using DS4Windows.Shared.Emulator.ViGEmGen1.Types.Legacy;
using Nefarius.ViGEm.Client;
using OutSlotDevice = DS4Windows.Shared.Emulator.ViGEmGen1.Types.OutSlotDevice;

namespace DS4Windows.Shared.Devices.Services
{
    public interface IOutputSlotManager
    {
        ViGEmClient Client { get; }

        IList<OutSlotDevice> OutputSlots { get; }

        bool RunningQueue { get; }

        event OutputSlotManager.SlotAssignedDelegate SlotAssigned;

        event OutputSlotManager.SlotUnassignedDelegate SlotUnassigned;

        event EventHandler ViGEmFailure;

        void Stop(bool immediate = false);

        void ShutDown();

        OutDevice AllocateController(OutputDeviceType contType);

        void DeferredPlugin(OutDevice outputDevice, int inIdx, OutDevice[] outdevs, OutputDeviceType contType);

        void DeferredRemoval(OutDevice outputDevice, int inIdx,
            OutDevice[] outdevs, bool immediate = false);

        OutSlotDevice FindOpenSlot();

        OutSlotDevice GetOutSlotDevice(OutDevice outputDevice);

        OutSlotDevice FindExistUnboundSlotType(OutputDeviceType contType);

        void UnplugRemainingControllers(bool immediate = false);
    }

    public class OutputSlotManager : IOutputSlotManager
    {
        public static int CURRENT_DS4_CONTROLLER_LIMIT = 8;

        public delegate void SlotAssignedDelegate(OutputSlotManager sender,
            int slotNum, OutSlotDevice outSlotDev);

        public delegate void SlotUnassignedDelegate(OutputSlotManager sender,
            int slotNum, OutSlotDevice outSlotDev);

        public const int DELAY_TIME = 500; // measured in ms

        private readonly Dictionary<int, OutDevice> deviceDict = new();
        private readonly int lastSlotIndex;
        private readonly OutDevice[] outputDevices = new OutDevice[CURRENT_DS4_CONTROLLER_LIMIT];
        private readonly ReaderWriterLockSlim queueLocker;
        private readonly Dictionary<OutDevice, int> revDeviceDict = new();

        private int queuedTasks;

        public OutputSlotManager(ViGEmClient client)
        {
            Client = client;

            OutputSlots = new OutSlotDevice[CURRENT_DS4_CONTROLLER_LIMIT];
            for (var i = 0; i < CURRENT_DS4_CONTROLLER_LIMIT; i++) OutputSlots[i] = new OutSlotDevice(i);

            lastSlotIndex = OutputSlots.Count > 0 ? OutputSlots.Count - 1 : 0;

            queueLocker = new ReaderWriterLockSlim();
        }

        public int NumAttachedDevices
        {
            get { return OutputSlots.Count(tmp => tmp.CurrentAttachedStatus == OutSlotDevice.AttachedStatus.Attached); }
        }

        public ViGEmClient Client { get; }

        public bool RunningQueue => queuedTasks > 0;
        public IList<OutSlotDevice> OutputSlots { get; }

        public event SlotAssignedDelegate SlotAssigned;
        public event SlotUnassignedDelegate SlotUnassigned;

        public event EventHandler ViGEmFailure;

        public void ShutDown()
        {
        }

        public void Stop(bool immediate = false)
        {
            UnplugRemainingControllers(immediate);
            while (RunningQueue) Thread.SpinWait(500);

            deviceDict.Clear();
            revDeviceDict.Clear();
        }

        public OutDevice AllocateController(OutputDeviceType contType)
        {
            OutDevice outputDevice = null;
            switch (contType)
            {
                case OutputDeviceType.Xbox360Controller:
                    outputDevice = new Xbox360OutDevice(Client);
                    break;
                case OutputDeviceType.DualShock4Controller:
                    //outputDevice = DS4OutDeviceFactory.CreateDS4Device(Client, Global.ViGEmBusVersionInfo);
                    //break;
                case OutputDeviceType.None:
                default:
                    break;
            }

            return outputDevice;
        }

        public void DeferredPlugin(OutDevice outputDevice, int inIdx, OutDevice[] outdevs,
            OutputDeviceType contType)
        {
            queueLocker.EnterWriteLock();
            queuedTasks++;
            //Action tempAction = new Action(() =>
            {
                var slot = FindEmptySlot();
                if (slot != -1)
                {
                    try
                    {
                        outputDevice.Connect();
                    }
                    catch (Win32Exception)
                    {
                        // Leave task immediately if connect call failed
                        ViGEmFailure?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    outputDevices[slot] = outputDevice;
                    deviceDict.Add(slot, outputDevice);
                    revDeviceDict.Add(outputDevice, slot);
                    OutputSlots[slot].AttachedDevice(outputDevice, contType);
                    if (inIdx != -1)
                    {
                        outdevs[inIdx] = outputDevice;
                        OutputSlots[slot].CurrentInputBound = OutSlotDevice.InputBound.Bound;
                    }

                    SlotAssigned?.Invoke(this, slot, OutputSlots[slot]);
                }
            }

            queuedTasks--;
            queueLocker.ExitWriteLock();
        }

        public void DeferredRemoval(OutDevice outputDevice, int inIdx,
            OutDevice[] outdevs, bool immediate = false)
        {
            _ = immediate;

            queueLocker.EnterWriteLock();
            queuedTasks++;

            {
                if (revDeviceDict.TryGetValue(outputDevice, out var slot))
                {
                    //int slot = revDeviceDict[outputDevice];
                    outputDevices[slot] = null;
                    deviceDict.Remove(slot);
                    revDeviceDict.Remove(outputDevice);
                    outputDevice.Disconnect();
                    if (inIdx != -1) outdevs[inIdx] = null;

                    OutputSlots[slot].DetachDevice();
                    SlotUnassigned?.Invoke(this, slot, OutputSlots[slot]);

                    //if (!immediate)
                    //{
                    //    Task.Delay(DELAY_TIME).Wait();
                    //}
                }
            }
            ;

            queuedTasks--;
            queueLocker.ExitWriteLock();
        }

        public OutSlotDevice FindOpenSlot()
        {
            OutSlotDevice temp = null;
            for (var i = 0; i < OutputSlots.Count; i++)
            {
                var tmp = OutputSlots[i];
                if (tmp.CurrentInputBound == OutSlotDevice.InputBound.Unbound &&
                    tmp.CurrentAttachedStatus == OutSlotDevice.AttachedStatus.UnAttached)
                {
                    temp = tmp;
                    break;
                }
            }

            return temp;
        }

        public OutSlotDevice GetOutSlotDevice(OutDevice outputDevice)
        {
            OutSlotDevice temp = null;
            if (outputDevice != null &&
                revDeviceDict.TryGetValue(outputDevice, out var slotNum))
                temp = OutputSlots[slotNum];

            return temp;
        }

        public OutSlotDevice FindExistUnboundSlotType(OutputDeviceType contType)
        {
            return OutputSlots.FirstOrDefault(tmp =>
                tmp.CurrentInputBound == OutSlotDevice.InputBound.Unbound &&
                tmp.CurrentAttachedStatus == OutSlotDevice.AttachedStatus.Attached && tmp.OutputDevice != null &&
                tmp.OutputDevice.GetDeviceType() == contType.ToString());
        }

        public void UnplugRemainingControllers(bool immediate = false)
        {
            _ = immediate;

            queueLocker.EnterWriteLock();
            queuedTasks++;
            {
                var slotIdx = 0;
                foreach (var device in OutputSlots)
                {
                    if (device.OutputDevice != null)
                    {
                        outputDevices[slotIdx] = null;
                        device.OutputDevice.Disconnect();

                        device.DetachDevice();
                        SlotUnassigned?.Invoke(this, slotIdx, OutputSlots[slotIdx]);
                        //if (!immediate)
                        //{
                        //    Task.Delay(DELAY_TIME).Wait();
                        //}
                    }

                    slotIdx++;
                }
            }

            queuedTasks--;
            queueLocker.ExitWriteLock();
        }

        private int FindEmptySlot()
        {
            var result = -1;
            for (var i = 0; i < outputDevices.Length && result == -1; i++)
            {
                var tempdev = outputDevices[i];
                if (tempdev == null) result = i;
            }

            return result;
        }

        public bool SlotAvailable(int slotNum)
        {
            bool result;
            if (slotNum < 0 && slotNum > lastSlotIndex) throw new ArgumentOutOfRangeException("Invalid slot number");

            //slotNum -= 1;
            result = OutputSlots[slotNum].CurrentAttachedStatus == OutSlotDevice.AttachedStatus.UnAttached;
            return result;
        }

        public OutSlotDevice GetOutSlotDevice(int slotNum)
        {
            OutSlotDevice temp;
            if (slotNum < 0 && slotNum > lastSlotIndex) throw new ArgumentOutOfRangeException("Invalid slot number");

            //slotNum -= 1;
            temp = OutputSlots[slotNum];
            return temp;
        }
    }
}
