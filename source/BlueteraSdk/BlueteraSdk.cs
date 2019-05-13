using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            _devicesFound = new ConcurrentBag<ulong>();
            _advWatcher.Start();
        }

        public void StopScan()
        {
            _advWatcher.Stop();
        }

        public async Task<BlueteraDevice> Connect(ulong addr, bool autopair = false)
        {
            BlueteraDevice bluetera = null;

            // get device                        
            BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(addr);

            // throw if already connected
            if (_connectedDevices.ContainsKey(device.BluetoothDeviceId.Id))
                throw new InvalidOperationException();

            // create Bluetera wrapper
            bluetera = new BlueteraDevice(device);
            bluetera.ConnectionStatusChanged += _bluetera_ConnectionStatusChanged;

            // non-UWP applications (console, WPF, etc.) should use the auto-pair with the device, or the pairing result will be 'RequiredHandlerNotRegistered'
            // see e.g. https://stackoverflow.com/questions/45191412/deviceinformation-pairasync-not-working-in-wpf/45196036#45196036
            if (autopair)
                bluetera.BaseDevice.DeviceInformation.Pairing.Custom.PairingRequested += (sender, args) => { args.Accept(); };

            var result = await bluetera.BaseDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
            if ((result.Status == DevicePairingResultStatus.AlreadyPaired) || (result.Status == DevicePairingResultStatus.Paired))
            {
                await bluetera.Connect();
            }
            else
            {
                throw new BlueteraException($"Operation failed. Pairing result = {result.Status}");
            }

            return bluetera;
        }
        #endregion

        #region Events handlers
        private void _advWatcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
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
