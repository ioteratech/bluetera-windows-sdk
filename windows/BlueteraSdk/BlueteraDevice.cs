using System;
using System.Collections.Generic;
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
using System.Diagnostics;
using Bluetera.Utilities;

namespace Bluetera
{
    public sealed class BlueteraDevice : IDisposable
    {
        #region Fields
        private GattDeviceService _service;
        private GattCharacteristic _txChar;     // TX is Bluetera --> Central (using Notifications)
        private GattCharacteristic _rxChar;     // RX is Central --> Bluetera     
        private List<byte> _rxData = new List<byte>();
        private bool _isDisposed = false;
        #endregion

        #region Properties
        public BluetoothLEDevice BaseDevice { get; private set; }

        public string Id
        {
            get { return BaseDevice.BluetoothDeviceId.Id; }
        }

        public ulong Address
        {
            get { return BaseDevice.BluetoothAddress; }
        }

        public string AddressAsString
        {
            get { return BlueteraUtilities.UlongAddressAsString(Address); }
        }

        public bool IsConnected
        {
            get { return (BaseDevice.ConnectionStatus == BluetoothConnectionStatus.Connected); }
        }
        #endregion

        #region Events
        public event TypedEventHandler<BlueteraDevice, BluetoothConnectionStatus> ConnectionStatusChanged;
        public event TypedEventHandler<BlueteraDevice, DownlinkMessage> DownlinkMessageReceived;
        #endregion

        #region Methods
        public async Task<GattCommunicationStatus> SendMessage(UplinkMessage msg)
        {
            var stream = new MemoryStream();
            msg.WriteDelimitedTo(stream);

            if (stream.Position > 0)
            {
                return await _rxChar.WriteValueAsync(stream.ToArray().AsBuffer());
            }
            else
            {
                throw new ArgumentException("Invalid UplinkMessage");
            }
        }

        public void Disconnect()
        {
            // Windows does not have a 'Disconnect' method per-se. The way to prevent the device from auto-connecting is to dispose it and any services it holds
            // See https://social.msdn.microsoft.com/Forums/windowsdesktop/en-US/9eae39ff-f6ca-4aa9-adaf-97450f2b4a6c/disconnect-bluetooth-low-energy?forum=wdk

            Dispose();
        }
        #endregion

        #region Events Handlers
        private async void _baseDevice_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            try
            {
                if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
                {
                    if (_txChar != null)
                        await _txChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                }

                ConnectionStatusChanged?.Invoke(this, sender.ConnectionStatus);
            }
            catch (Exception ex)
            {
                /* Currently ignore */
            }
        }

        private void _txChar_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // enqueue new data
            var data = new byte[args.CharacteristicValue.Length];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
            _rxData.AddRange(data);

            // try parsing
            int consumedBytes = 0;
            DownlinkMessage msg = null;
            using (var stream = new MemoryStream(_rxData.ToArray()))
            {
                msg = DownlinkMessage.Parser.ParseDelimitedFrom(stream);
                consumedBytes = (int)stream.Position;
            }

            // publish
            if (msg != null)
            {
                _rxData.RemoveRange(0, consumedBytes);
                DownlinkMessageReceived?.Invoke(this, msg);
            }
        }
        #endregion

        #region Helpers
        private static async Task<GattDeviceService> GetServiceAsync(BluetoothLEDevice device)
        {
            var result = await device?.GetGattServicesForUuidAsync(BlueteraConstants.BusServiceUuid);
            return (result?.Status == GattCommunicationStatus.Success) ? result.Services[0] : null;
        }

        private static async Task<GattCharacteristic> GetCharacteristicAsync(GattDeviceService service, Guid uid)
        {
            var result = await service?.GetCharacteristicsForUuidAsync(uid);
            return (result?.Status == GattCommunicationStatus.Success) ? result.Characteristics[0] : null;
        }
        #endregion

        #region Lifecycle        
        internal BlueteraDevice(BluetoothLEDevice device)
        {
            BaseDevice = device;
        }

        internal async Task Start()
        {            
            try
            {
                // get service and characteristics. This will also physically connect to the device
                _service = await GetServiceAsync(BaseDevice);
                _rxChar = await GetCharacteristicAsync(_service, BlueteraConstants.BusRxCharUuid);
                _txChar = await GetCharacteristicAsync(_service, BlueteraConstants.BusTxCharUuid);
                _txChar.ValueChanged += _txChar_ValueChanged;
                var gattStatus = await _txChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (gattStatus != GattCommunicationStatus.Success)
                    throw new BlueteraException("Failed to enable notifications");

                // start watching for device connection/disconnection
                BaseDevice.ConnectionStatusChanged += _baseDevice_ConnectionStatusChanged;
            }
            catch(BlueteraException)
            {
                throw;
            }
            catch(Exception ex)
            {
                throw new BlueteraException("Failed to obtain Bluetera service - make sure the device has been paired", ex);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            BaseDevice?.Dispose();
            _service?.Dispose();

            if (_txChar != null)
                _txChar.ValueChanged -= _txChar_ValueChanged;

            _txChar = null;
            _rxChar = null;

            _isDisposed = true;
        }
        #endregion        
    }
}
