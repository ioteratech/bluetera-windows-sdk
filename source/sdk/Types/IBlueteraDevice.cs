using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Bluetera
{
    public interface IBlueteraDevice
    {
        #region Properties
        string Id { get; }
        ulong Address { get; }
        string AddressAsString { get; }
        string HardwareVersion { get; }
        string FirmwareVersion { get; }
        ConnectionStatus ConnectionStatus { get; }
        #endregion

        #region Events
        event TypedEventHandler<IBlueteraDevice, ConnectionStatus> ConnectionStatusChanged;
        event TypedEventHandler<IBlueteraDevice, DownlinkMessage> DownlinkMessageReceived;
        #endregion

        #region Methods
        Task<bool> SendMessage(UplinkMessage msg);
        void Disconnect();
        #endregion
    }
}
