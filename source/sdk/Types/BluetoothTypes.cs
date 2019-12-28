using System;
using System.Collections.Generic;
using System.Text;

namespace Bluetera.Types
{
    public enum ConnectionStatus
    {
        Disconnected = 0,
        Connected = 1
    }

    public class BlueteraAdvertisement
    {
        // TODO: add properties
        public ulong Address { get; set; }
        public int Rssi { get; set; }
    }
}
