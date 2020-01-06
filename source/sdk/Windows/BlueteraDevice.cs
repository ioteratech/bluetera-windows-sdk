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
using Bluetera;

namespace Bluetera.Windows
{
    public sealed class BlueteraDevice : IBlueteraDevice, IDisposable
    {
        #region Fields
        private GattDeviceService _busService;     // Bluetera UART Service - see comment (1) at the end of this file
        private GattCharacteristic _busTxChar;     // TX is Bluetera --> Central (using Notifications)
        private GattCharacteristic _busRxChar;     // RX is Central --> Bluetera     
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

        public string HardwareVersion { get; private set; }

        public string FirmwareVersion { get; private set; }

        public ConnectionStatus ConnectionStatus {
            get
            {
                return (BaseDevice.ConnectionStatus == BluetoothConnectionStatus.Connected) ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            }
        }
        #endregion

        #region Events
        public event TypedEventHandler<IBlueteraDevice, ConnectionStatus> ConnectionStatusChanged;
        public event TypedEventHandler<IBlueteraDevice, DownlinkMessage> DownlinkMessageReceived;
        #endregion

        #region Methods
        public async Task<bool> SendMessage(UplinkMessage msg)
        {
            var stream = new MemoryStream();
            msg.WriteDelimitedTo(stream);

            if (stream.Position > 0)
            {
                byte[] buf = stream.ToArray();
                GattCommunicationStatus status = await _busRxChar.WriteValueAsync(stream.ToArray().AsBuffer());
                return (status == GattCommunicationStatus.Success) ? true : false;
            }
            else
            {
                throw new ArgumentException("Invalid UplinkMessage");
            }
        }

        public void Disconnect()
        {
            // Windows does not have a 'Disconnect' method per-se. The way to prevent the device from auto-connecting is to dispose it and any services it holds.
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
                    if (_busTxChar != null)
                        await _busTxChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                    ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Connected);
                }
                else
                {
                    ConnectionStatusChanged?.Invoke(this, ConnectionStatus.Disconnected);
                }
                
            }
            catch (Exception)
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
        private static async Task<GattDeviceService> GetServiceAsync(BluetoothLEDevice device, Guid uuid)
        {
            var result = await device.GetGattServicesForUuidAsync(uuid);
            return result.Services[0];
        }

        private static async Task<GattCharacteristic> GetCharacteristicAsync(GattDeviceService service, Guid uuid)
        {
            var result = await service.GetCharacteristicsForUuidAsync(uuid);
            return result.Characteristics[0];
        }

        private static async Task<Dictionary<Guid, byte[]>> ReadAllCharacteristicsAsync(BluetoothLEDevice device, Guid serviceUuid)
        {
            var result = new Dictionary<Guid, byte[]>();
            using (var infoService = await GetServiceAsync(device, GattServiceUuids.DeviceInformation))
            {
                // it is valid to await() in foreach() in this case, as we need the results in order
                var gcResult = await infoService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                foreach (var c in gcResult.Characteristics)
                {
                    if (c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                    {
                        var grResult = await c.ReadValueAsync(BluetoothCacheMode.Uncached);
                        using (var reader = DataReader.FromBuffer(grResult.Value))
                        {
                            var value = new byte[reader.UnconsumedBufferLength];
                            reader.ReadBytes(value);
                            result.Add(c.Uuid, value);
                        }
                    }
                }

                return result;
            }
        }
        #endregion

        #region Lifecycle        
        internal BlueteraDevice(BluetoothLEDevice device)
        {
            BaseDevice = device;
        }        

        internal async Task Connect()
        {
            try
            {
                // read device information
                byte[] bytes;
                var values = await ReadAllCharacteristicsAsync(BaseDevice, GattServiceUuids.DeviceInformation);
                if (values.TryGetValue(GattCharacteristicUuids.HardwareRevisionString, out bytes))
                    HardwareVersion = Encoding.UTF8.GetString(bytes);
                if (values.TryGetValue(GattCharacteristicUuids.FirmwareRevisionString, out bytes))
                    FirmwareVersion = Encoding.UTF8.GetString(bytes);

                // get service and characteristics. This will also physically connect to the device
                _busService = await GetServiceAsync(BaseDevice, BlueteraConstants.BusServiceUuid);
                _busRxChar = await GetCharacteristicAsync(_busService, BlueteraConstants.BusRxCharUuid);
                _busTxChar = await GetCharacteristicAsync(_busService, BlueteraConstants.BusTxCharUuid);
                _busTxChar.ValueChanged += _txChar_ValueChanged;
                var gattStatus = await _busTxChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (gattStatus != GattCommunicationStatus.Success)
                    throw new BlueteraException("Failed to enable notifications");

                // start watching for device connection/disconnection
                BaseDevice.ConnectionStatusChanged += _baseDevice_ConnectionStatusChanged;
            }
            catch (Exception ex)
            {
                Dispose();
                throw new BlueteraException("Operation failed", ex);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            BaseDevice?.Dispose();
            _busService?.Dispose();

            if (_busTxChar != null)
                _busTxChar.ValueChanged -= _txChar_ValueChanged;

            _busTxChar = null;
            _busRxChar = null;

            _isDisposed = true;
        }        
        #endregion
    }
}

/*
 * Comments:
 * 
 * 1. We don't strictly need to keep a reference to the Bluetera service. We keep it so we can call it's Disponse() when we wish to physically disconnect.
 *    See BlueteraDevice.Disconnect() and https://social.msdn.microsoft.com/Forums/windowsdesktop/en-US/9eae39ff-f6ca-4aa9-adaf-97450f2b4a6c/disconnect-bluetooth-low-energy?forum=wdk 
 * */
