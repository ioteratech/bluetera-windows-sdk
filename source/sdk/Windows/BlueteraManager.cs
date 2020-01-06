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
using Bluetera;

namespace Bluetera.Windows
{
    public sealed class BlueteraManager : IBlueteraManager
    {
        // Based on / Inspired by https://github.com/Microsoft/Windows-universal-samples.git
        #region Fields
        private ConcurrentBag<ulong> _devicesFound;
        private ConcurrentDictionary<string, IBlueteraDevice> _connectedDevices;
        private BluetoothLEAdvertisementWatcher _advWatcher;
        private static readonly Lazy<BlueteraManager> _instance = new Lazy<BlueteraManager>(() => new BlueteraManager());
        #endregion

        #region Properties
        public static BlueteraManager Instance
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
            bluetera.ConnectionStatusChanged += Bluetera_ConnectionStatusChanged; ;

            // non-UWP applications (console, WPF, etc.) should use the auto-pair with the device, or the pairing result will be 'RequiredHandlerNotRegistered'
            // see e.g. https://stackoverflow.com/questions/45191412/deviceinformation-pairasync-not-working-in-wpf/45196036#45196036
            DevicePairingResult pairingResult;
            if (autopair)
            {   // this will auto-pair without any popup
                bluetera.BaseDevice.DeviceInformation.Pairing.Custom.PairingRequested += (sender, args) => { args.Accept(); };
                pairingResult = await bluetera.BaseDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
            }
            else
            {   // If not already paired, this will invoke a pop-up requesting to pair
                pairingResult = await bluetera.BaseDevice.DeviceInformation.Pairing.PairAsync();
            }

            if ((pairingResult.Status == DevicePairingResultStatus.AlreadyPaired) || (pairingResult.Status == DevicePairingResultStatus.Paired))
            {
                await bluetera.Connect();
            }
            else
            {
                throw new BlueteraException($"Operation failed. Pairing result = {pairingResult.Status}");
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

        private void Bluetera_ConnectionStatusChanged(IBlueteraDevice sender, ConnectionStatus args)
        {
            if (args == ConnectionStatus.Connected)
            {
                _connectedDevices.TryAdd(sender.Id, sender);
            }
            else
            {
                IBlueteraDevice dummy;
                _connectedDevices.TryRemove(sender.Id, out dummy);
            }
        }
        #endregion

        #region Lifecycle        
        private BlueteraManager()
        {
            _advWatcher = new BluetoothLEAdvertisementWatcher();        // TODO: use advertisment filters
            _advWatcher.ScanningMode = BluetoothLEScanningMode.Active;
            _advWatcher.Received += _advWatcher_Received;

            _connectedDevices = new ConcurrentDictionary<string, IBlueteraDevice>();
        }

        event TypedEventHandler<IBlueteraManager, BlueteraAdvertisement> IBlueteraManager.AdvertismentReceived
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        public void DisposeAll()
        {
            //foreach (var device in _connectedDevices.Values)
            //    device.Dispose();
        }

        Task<IBlueteraDevice> IBlueteraManager.Connect(ulong addr, bool autopair)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
