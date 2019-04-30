using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bluetera;
using Bluetera.Utilities;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace HelloBlueteraWinRt
{
    class Program
    {
        private static BlueteraDevice device;
        private static ulong lastAddress;

        static void Main(string[] args)
        {
            // Note
            // When running in non-UWP desktop applications like this one, you must either:
            // - pair with the Bluetera from Windows Settings before running this example
            // - uncomment the relevant lines in the sdkConnect() method
            Console.WriteLine("Running");

            BlueteraSdk sdk = BlueteraSdk.Instance;
            sdk.AdvertismentReceived += BlueteraDevice_AdvertismentReceived;

            bool running = true;
            Console.WriteLine("\n\n'q' - quit\n's' - start scan\n't' - stop scan\n'c' - connect\n'd' - disconnect\n'e' - send Echo\n'i' - configure and start IMU\n'j' - stop IMU\n\n");
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
                        sdk.StartScan();
                        break;

                    case 't':
                        Console.WriteLine("Stopping Scan");
                        sdk.StopScan();
                        break;

                    case 'c':
                        Console.WriteLine("Connecting");
                        sdk.StopScan();
                        device = sdk.Connect(lastAddress, autopair: true).Result;   // Note: non-UWP apps (like this one) should set autopair:true. See BlueteraSdk.Connect() for more info
                        device.ConnectionStatusChanged += Device_ConnectionStatusChanged;
                        device.DownlinkMessageReceived += Device_DownlinkMessageReceived;
                        Console.WriteLine($"Device connection status: {device.BaseDevice.ConnectionStatus}");
                        break;

                    case 'd':
                        Console.WriteLine("Disconnecting");
                        sdk.Disconnect(device);
                        break;

                    case 'e':
                        {
                            Console.WriteLine("Sending echo");
                            var result = SendEcho().Result;
                            Console.WriteLine($"GattCommunicationStatus: {result}");
                        }
                        break;

                    case 'i':
                        {
                            Console.WriteLine("Configuring and starting IMU");
                            var result = StartImu().Result;
                            Console.WriteLine($"GattCommunicationStatus: {result}");
                        }
                        break;

                    case 'j':
                        {
                            Console.WriteLine("Stopping IMU");
                            var result = StopImu().Result;
                            Console.WriteLine($"GattCommunicationStatus: {result}");
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid command");
                        break;

                }
            }

            sdk.DisposeAll();
        }

        private static void Device_ConnectionStatusChanged(BlueteraDevice sender, BluetoothConnectionStatus args)
        {
            Console.WriteLine($"Connection status changed. Device = {sender.AddressAsString}, Status = {args}");
            if (args == BluetoothConnectionStatus.Connected)
            {
                device = sender;
            }
            else
            {
                device.Dispose();
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

        private static async Task<GattCommunicationStatus> SendEcho()
        {
            UplinkMessage msg = new UplinkMessage()
            {
                Echo = new EchoPayload()
                {
                    Value = "Hello"
                }
            };

            return await device.SendMessage(msg);
        }

        private static async Task<GattCommunicationStatus> StartImu()
        {
            GattCommunicationStatus status;

            UplinkMessage msg = new UplinkMessage()
            {
                Imu = new ImuCommand
                {
                    Start = new ImuStart()
                    {
                        DataTypes = (uint)ImuDataType.Quaternion,    // ImuDataType are enum flags - logically 'OR' to combine several types
                        Odr = 50,                                       // Output Data Rate [Hz]
                        AccFsr = 4,                                     // Acceleromenter Full Scale Range [g]
                        GyroFsr = 500                                   // Gyroscope Full Scale Range [deg/sec]
                    }
                }
            };
            
            return await device.SendMessage(msg);
        }

        private static async Task<GattCommunicationStatus> StopImu()
        {
            UplinkMessage stopImuMsg = new UplinkMessage()
            {
                Imu = new ImuCommand()
                {
                    Stop = true
                }
            };
            return await device.SendMessage(stopImuMsg);
        }
    }
}
