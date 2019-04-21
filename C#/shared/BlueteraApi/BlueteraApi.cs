﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Collections.Concurrent;
using Windows.Foundation;
using Windows.Storage.Streams;
using System.IO;
using Google.Protobuf;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Bluetera
{
    public sealed class BlueteraApi
    {
        // Based on / Inspired by https://github.com/Microsoft/Windows-universal-samples.git        

        #region Fields
        private static Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private static ConcurrentBag<ulong> _devicesFound;
        private static ConcurrentDictionary<string, BlueteraDevice> _devices;
        private static BluetoothLEAdvertisementWatcher _advWatcher;
        #endregion        

        #region Events
        public static event TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs> AdvertismentReceived;
        public static event TypedEventHandler<BlueteraDevice, BluetoothConnectionStatus> ConnectionStatusChanged;
        #endregion

        #region Methods
        public static void StartScan()
        {
            _logger.Info($"StartScan() - called");
            _devicesFound = new ConcurrentBag<ulong>();
            _advWatcher.Start();
        }

        public static void StopScan()
        {
            _logger.Info($"StopScan() - called");
            _advWatcher.Stop();
        }

        public static async Task<DevicePairingResult> Connect(ulong addr)
        {
            _logger.Info($"Connect() - called with address = {addr}");

            // get device            
            BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);

            // throw if already connected
            if (_devices.ContainsKey(device.BluetoothDeviceId.Id))
                throw new InvalidOperationException();

            DevicePairingResult result = null;
            if (device != null)
            {
                BlueteraDevice bluetera = new BlueteraDevice(device);
                bluetera.ConnectionStatusChanged += Bluetera_ConnectionStatusChanged;
                result = await bluetera.Device.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
                await bluetera.Start();
            }

            return result;
        }

        public static void Disconnect(BlueteraDevice device)
        {
            // Windows does not have a 'Disconnect' method per-se. The way to prevent the device from auto-connecting is to dispose it and any services it holds
            // See https://social.msdn.microsoft.com/Forums/windowsdesktop/en-US/9eae39ff-f6ca-4aa9-adaf-97450f2b4a6c/disconnect-bluetooth-low-energy?forum=wdk
            device.Dispose();
        }
        #endregion

        #region Events handlers
        private static void _advWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            _logger.Debug($"_advWatcher_Received() - found device = {args.BluetoothAddress}");

            // Ignore non-Bluetera devices
            if (!BlueteraConstants.ValidDeviceNames.Contains(args.Advertisement.LocalName))
                return;

            // Ignore duplicate calls
            if (_devicesFound.Contains(args.BluetoothAddress))
                return;

            // add and notify
            _devicesFound.Add(args.BluetoothAddress);
            AdvertismentReceived?.Invoke(sender, args);
        }

        private static void Bluetera_ConnectionStatusChanged(BlueteraDevice sender, BluetoothConnectionStatus args)
        {
            if (args == BluetoothConnectionStatus.Connected)
            {
                if(_devices.TryAdd(sender.Id, sender))
                {
                    ConnectionStatusChanged?.Invoke(sender, args);
                }
            }
            else
            {
                BlueteraDevice dummy;
                if(_devices.TryRemove(sender.Id, out dummy))
                {
                    ConnectionStatusChanged?.Invoke(sender, args);
                }
            }
        }
        #endregion

        #region Lifecycle        
        static BlueteraApi()
        {
            _advWatcher = new BluetoothLEAdvertisementWatcher();        // TODO: use advertisment filters
            _advWatcher.ScanningMode = BluetoothLEScanningMode.Active;
            _advWatcher.Received += _advWatcher_Received;

            _devices = new ConcurrentDictionary<string, BlueteraDevice>();
        }

        public static void DisposeAll()
        {
            foreach (var device in _devices.Values)
                device.Dispose();
        }
        #endregion
    }
}
