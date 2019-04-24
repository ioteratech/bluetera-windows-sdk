using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bluetera.Utilities
{
    public class BlueteraUtilities
    {
        public static string UlongAddressAsString(ulong addr, bool addColons = true)
        {
            string separator = addColons ? ":" : "";
            var bytes = BitConverter.GetBytes(addr).Take(6).Reverse();
            return String.Join(separator, bytes.Select(b => String.Format("{0:X2}", b)));
        }

        public static ulong StringAddresssAsUlong(string addr)
        {
            addr = addr.Replace(":", "");
            var addrBytes = Enumerable.Range(0, addr.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(addr.Substring(x, 2), 16)).Reverse().ToArray();

            byte[] paddedAddr = new byte[8];
            Array.Copy(addrBytes, paddedAddr, 6);
            return BitConverter.ToUInt64(paddedAddr, 0);
        }
    }
}
