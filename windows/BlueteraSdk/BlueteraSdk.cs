using System;
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
    public sealed class BlueteraSdk
    {
        // Based on / Inspired by https://github.com/Microsoft/Windows-universal-samples.git
        #region Fields
        private static Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private ConcurrentBag<ulong> _devicesFound;
        private ConcurrentDictionary<string, BlueteraDevice> _connectedDevices;
        private BluetoothLEAdvertisementWatcher _advWatcher;
        private static readonly Lazy<BlueteraSdk> _instance = new Lazy<BlueteraSdk>(() => new BlueteraSdk());
        #endregion

        #region Properties
        public static BlueteraSdk Instance
        {
            get { return _instance.Value; }
        }
        #endregion

        #region Events
        public event TypedEventHandler<BluetoothLEAdvertisementWatcher, BluetoothLEAdvertisementReceivedEventArgs> AdvertismentReceived;
        #endregion

        #region Methods
        public void StartScan()
        {
            _logger.Info($"StartScan() - called");
            _devicesFound = new ConcurrentBag<ulong>();
            _advWatcher.Start();
        }

        public void StopScan()
        {
            _logger.Info($"StopScan() - called");
            _advWatcher.Stop();
        }

        public async Task<BlueteraDevice> Connect(ulong addr)
        {
            _logger.Info($"Connect() - called with address = {addr}");

            // get device                        
            BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);

            // throw if already connected
            if (_connectedDevices.ContainsKey(device.BluetoothDeviceId.Id))
                throw new InvalidOperationException();

            BlueteraDevice bluetera = null;
            if (device != null)
            {
                bluetera = new BlueteraDevice(device);
                bluetera.ConnectionStatusChanged += _bluetera_ConnectionStatusChanged;

                // for non-UWP applications (console, WPF, etc.) uncomment the following code line to auto-pair with the device
                // see e.g. https://stackoverflow.com/questions/45191412/deviceinformation-pairasync-not-working-in-wpf/45196036#45196036
                // bluetera.BaseDevice.DeviceInformation.Pairing.Custom.PairingRequested += (sender, args) => { args.Accept(); };

                var pairingResult = await bluetera.BaseDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
                _logger.Debug($"Connect() - pairing status = {pairingResult.Status}");
                
                await bluetera.Start();
            }

            return bluetera;
        }

        public void Disconnect(BlueteraDevice device)
        {
            // Windows does not have a 'Disconnect' method per-se. The way to prevent the device from auto-connecting is to dispose it and any services it holds
            // See https://social.msdn.microsoft.com/Forums/windowsdesktop/en-US/9eae39ff-f6ca-4aa9-adaf-97450f2b4a6c/disconnect-bluetooth-low-energy?forum=wdk
            device.Dispose();
        }
        #endregion

        #region Events handlers
        private void _advWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
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

        private void _bluetera_ConnectionStatusChanged(BlueteraDevice sender, BluetoothConnectionStatus args)
        {
            if (args == BluetoothConnectionStatus.Connected)
            {
                _connectedDevices.TryAdd(sender.Id, sender);
            }
            else
            {
                BlueteraDevice dummy;
                _connectedDevices.TryRemove(sender.Id, out dummy);
            }
        }
        #endregion

        #region Lifecycle        
        private BlueteraSdk()
        {
            _advWatcher = new BluetoothLEAdvertisementWatcher();        // TODO: use advertisment filters
            _advWatcher.ScanningMode = BluetoothLEScanningMode.Active;
            _advWatcher.Received += _advWatcher_Received;

            _connectedDevices = new ConcurrentDictionary<string, BlueteraDevice>();
        }

        public void DisposeAll()
        {
            foreach (var device in _connectedDevices.Values)
                device.Dispose();
        }
        #endregion
    }
}
