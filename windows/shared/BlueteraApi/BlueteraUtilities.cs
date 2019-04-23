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
    }
}
