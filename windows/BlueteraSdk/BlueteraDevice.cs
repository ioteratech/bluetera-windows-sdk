using System;
using System.Collections.Generic;
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
using System.Diagnostics;
using Bluetera.Utilities;

namespace Bluetera
{
    public sealed class BlueteraDevice : IDisposable
    {
        #region Fields
        private static Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private GattDeviceService _service;
        private GattCharacteristic _txChar;     // TX is Bluetera --> Central (using Notifications)
        private GattCharacteristic _rxChar;     // RX is Central --> Bluetera     
        private List<byte> _rxData = new List<byte>();
        private bool _isDisposed = false;
        #endregion

        #region Properties
        public BluetoothLEDevice Device { get; private set; }

        public string Id
        {
            get { return Device.BluetoothDeviceId.Id; }
        }

        public ulong Address
        {
            get { return Device.BluetoothAddress; }
        }

        public string AddressAsString
        {
            get { return BlueteraUtilities.UlongAddressAsString(Address); }
        }
        #endregion

        #region Events
        internal event TypedEventHandler<BlueteraDevice, BluetoothConnectionStatus> ConnectionStatusChanged;
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
                throw new ArgumentException();
            }
        }
        #endregion

        #region Events Handlers
        private async void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            _logger.Debug($"Device_ConnectionStatusChanged() - device = {BlueteraUtilities.UlongAddressAsString(sender.BluetoothAddress)}, status = {sender.ConnectionStatus}");

            try
            {
                // protect against race conditions
                Debug.Assert(sender == Device);

                if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
                {
                    _txChar.ValueChanged += _txChar_ValueChanged;
                    await _txChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                }
                else
                {
                    if (_txChar != null)
                        _txChar.ValueChanged -= _txChar_ValueChanged;
                }

                ConnectionStatusChanged?.Invoke(this, sender.ConnectionStatus);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private void _txChar_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            _logger.Trace($"_txChar_ValueChanged() - received {args.CharacteristicValue.Length} bytes");

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
            Device = device;
            Device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
        }

        internal async Task Start()
        {
            _service = await GetServiceAsync(Device);
            _txChar = await GetCharacteristicAsync(_service, BlueteraConstants.BusTxCharUuid);
            _rxChar = await GetCharacteristicAsync(_service, BlueteraConstants.BusRxCharUuid);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Device?.Dispose();
            _service?.Dispose();
            _txChar = null;
            _rxChar = null;

            _isDisposed = true;
        }
        #endregion        
    }
}
