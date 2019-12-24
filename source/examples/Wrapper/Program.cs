using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wrapper
{
    class Program
    {
        static void Main(string[] args)
        {

            StatusCode err = SdkWrapper.Initialize();
                
            SdkWrapper.SetOnDeviceConnected(OnDeviceConnected);
            Console.WriteLine("connecting...");
            err = SdkWrapper.Connect("F6:EF:6A:A3:43:72");
            Console.WriteLine($"SdkWrapper.Connect: {err.ToString()}");

            Console.WriteLine("done");
            Console.ReadLine();

            SdkWrapper.Terminate();
        }

        static void OnDeviceConnected(string addr)
        {
            Console.WriteLine("device connected: " + addr);
            SdkWrapper.SetOnUartTx(addr, OnUartTx);
        }

        static void OnUartTx(byte[] data, ushort length)
        {

        }
    }
}
