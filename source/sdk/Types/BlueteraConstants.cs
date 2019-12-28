using System;
using System.Collections.Generic;
using System.Text;

namespace Bluetera.Types
{
    internal class BlueteraConstants
    {
        #region Constants
        internal static Guid BusServiceUuid = new Guid("e2530001-9ba2-4913-9cd4-ce6ee7b579e8");
        internal static Guid BusRxCharUuid = new Guid("e2530002-9ba2-4913-9cd4-ce6ee7b579e8");
        internal static Guid BusTxCharUuid = new Guid("e2530003-9ba2-4913-9cd4-ce6ee7b579e8");
        internal static string[] ValidDeviceNames = { @"BlueTera", @"Bluetera" };
        #endregion

        #region Error Codes
        internal int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        internal int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        internal int E_ACCESSDENIED = unchecked((int)0x80070005);
        internal int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        #endregion
    }
}
