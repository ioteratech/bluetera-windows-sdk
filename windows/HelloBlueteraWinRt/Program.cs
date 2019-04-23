using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bluetera;
using Bluetera.Utilities;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace HelloBlueteraWinRt
{
    class Program
    {
        private static BlueteraDevice device;
        private static ulong lastAddress;

        static void Main(string[] args)
        {
            Console.WriteLine("Running");

            BlueteraSdk.AdvertismentReceived += BlueteraDevice_AdvertismentReceived;
            BlueteraSdk.ConnectionStatusChanged += BlueteraApi_ConnectionStatusChanged;

            bool running = true;
            Console.WriteLine("'q' - quit\n's' - start scan\n't' - stop scan\n'c' - connect\n'd' - disconnect\n'b' - send scanner command 0x22\n\n");
            while (running)
            {
                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case 'q':
                        Console.WriteLine("Quitting");
                        running = false;
                        break;

                    case 's':
                        Console.WriteLine("Starting Scan");
                        BlueteraSdk.StartScan();
                        break;

                    case 't':
                        Console.WriteLine("Stopping Scan");
                        BlueteraSdk.StopScan();
                        break;

                    case 'c':
                        Console.WriteLine("Connecting");
                        BlueteraSdk.StopScan();
                        var paringResult = BlueteraSdk.Connect(lastAddress).Result;
                        Console.WriteLine($"Pairing result = {paringResult.Status}");
                        break;

                    case 'd':
                        Console.WriteLine("Disconnecting");
                        BlueteraSdk.Disconnect(device);
                        break;

                    case 'f':
                        Console.WriteLine("Configuring device");
                        ConfigureDevice().Wait();
                        break;

                    case 'e':
                        Console.WriteLine("Sending echo");
                        SendEcho().Wait();
                        break;

                    case 'i':
                        break;

                    default:
                        Console.WriteLine("Invalid command");
                        break;

                }
            }

            BlueteraSdk.DisposeAll();
        }

        private static void BlueteraApi_ConnectionStatusChanged(BlueteraDevice sender, BluetoothConnectionStatus args)
        {
            Console.WriteLine($"Connection status changed. Device = {sender.AddressAsString}, Status = {args}");
            if (args == BluetoothConnectionStatus.Connected)
            {
                device = sender;
                device.DownlinkMessageReceived += Device_DownlinkMessageReceived;
            }
            else
            {
                device.DownlinkMessageReceived -= Device_DownlinkMessageReceived;
                device = null;
            }
        }

        private static void Device_DownlinkMessageReceived(BlueteraDevice sender, DownlinkMessage args)
        {
            Console.WriteLine($"Recevied message: {args.ToString()}");
        }

        private static void BlueteraDevice_AdvertismentReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            Console.WriteLine($"Bluetera found: {BlueteraUtilities.UlongAddressAsString(args.BluetoothAddress)}");
            lastAddress = args.BluetoothAddress;            
        }

        private static async Task ConfigureDevice()
        {
            if (device != null)
            {
                UplinkMessage msg = new UplinkMessage()
                {
                    Imu = new ImuCommand()
                    {
                        Config = new ImuConfig()
                        {
                            DataTypes = (uint)ImuDataType.Accelerometer,
                            Odr = 50,
                            AccFsr = 2,
                            GyroFsr = 1
                        }
                    }
                };

                await device.SendMessage(msg);
            }
            else
            {
                Console.WriteLine("No device is connected - ignoring command");
            }
        }

        private static async Task SendEcho()
        {
            if (device != null)
            {
                UplinkMessage msg = new UplinkMessage()
                {
                    Echo = new EchoPayload()
                    {
                        Value = "Hello"
                    }
                };

                await device.SendMessage(msg);
            }
            else
            {
                Console.WriteLine("No device is connected - ignoring command");
            }
        }
    }
}
