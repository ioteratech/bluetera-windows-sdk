﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Google.Protobuf;
using Bluetera;
using Bluetera.Utilities;

namespace Bluetera.Iotera
{
    public class BlueteraDevice : IBlueteraDevice
    {
        #region Fields
        private List<byte> _rxData = new List<byte>();
        #endregion

        #region Properties
        public string Id { get; }   // TODO: implement
        public ulong Address { get; private set; }
        public string AddressAsString { get { return BlueteraUtilities.UlongAddressAsString(Address); }}
        public string HardwareVersion { get; }   // TODO: implement
        public string FirmwareVersion { get; }   // TODO: implement
        public ConnectionStatus ConnectionStatus { get; private set; }
        #endregion

        #region Events
        public event TypedEventHandler<IBlueteraDevice, ConnectionStatus> ConnectionStatusChanged;
        public event TypedEventHandler<IBlueteraDevice, DownlinkMessage> DownlinkMessageReceived;
        #endregion

        #region Methods
        public void Disconnect()
        {
            NativeSdkWrapper.Disconnect(Address);
        }

        public Task<bool> SendMessage(UplinkMessage msg)
        {
            var stream = new MemoryStream();
            msg.WriteDelimitedTo(stream);

            if (stream.Position > 0)
            {
                byte[] buf = stream.ToArray();
                return Task.Factory.StartNew(() =>
                {
                    NativeSdkWrapper.StatusCode status = NativeSdkWrapper.Write(Address, buf, (ushort)buf.Length);
                    return (status == NativeSdkWrapper.StatusCode.Success);
                });
            }
            else
            {
                throw new ArgumentException("Invalid UplinkMessage");
            }
        }
        #endregion

        #region Event Forwarding
        internal void OnUartTx(byte[] data, ushort length)
        {
            // enqueue new data           
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

        internal void OnConnectionStatusChanged(ConnectionStatus status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }
        #endregion

        #region Lifecycle
        internal BlueteraDevice(ulong addr)
        {
            Address = addr;           
        }
        #endregion
    }
}
