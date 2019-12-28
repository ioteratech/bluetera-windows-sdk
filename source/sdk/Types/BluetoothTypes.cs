using System;
using System.Collections.Generic;
using System.Text;

namespace Bluetera
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
        public double Rssi { get; set; }
    }
}
