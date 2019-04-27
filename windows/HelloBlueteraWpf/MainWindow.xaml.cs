using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using Bluetera;
using Windows.Devices.Bluetooth.Advertisement;
using Bluetera.Utilities;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace HelloBlueteraWpf
{
    public enum ApplicationStatusType { Idle, Scanning, Connecting, Connected, Disconnecting, Failed }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Constants / Enums
        
        #endregion        

        #region Lifecycle
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _sdk = BlueteraSdk.Instance;
            _sdk.AdvertismentReceived += _sdk_AdvertismentReceived;

            Status = ApplicationStatusType.Idle;
        }
        #endregion        

        #region UI
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Event handlers
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _sdk.StartScan();
        }

        private void ResetCubeButton_Click(object sender, RoutedEventArgs e)
        {

        }

        #region Bound Properties
        private ApplicationStatusType _status = ApplicationStatusType.Idle;
        public ApplicationStatusType Status
        {
            get { return _status; }
            set { _status = value; NotifyPropertyChanged(); }
        }
        #endregion
        #endregion
        #endregion

        #region Bluetera
        #region Fields
        private Quaternion _q0 = Quaternion.Identity;
        private BlueteraSdk _sdk;
        private BlueteraDevice _bluetera;
        #endregion

        #region Event Handlers
        private void _sdk_AdvertismentReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    Status = ApplicationStatusType.Connecting;

                    // Try connecting to Bluetera                    
                    _bluetera = await _sdk.Connect(args.BluetoothAddress);  // This method will either connect or throw
                    _bluetera.ConnectionStatusChanged += _bluetera_ConnectionStatusChanged;
                    _bluetera.DownlinkMessageReceived += _bluetera_DownlinkMessageReceived;
                    Status = ApplicationStatusType.Connected;

                    // Configure and Start IMU. Methods will throw on failure
                    await ConfigureImu();
                    await StartImu();
                }
                catch(BlueteraException)
                {
                    Status = ApplicationStatusType.Failed;
                }
            });
        }        

        private void _bluetera_ConnectionStatusChanged(BlueteraDevice sender, BluetoothConnectionStatus args)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // avoid some potential races
                    if (sender != _bluetera)
                        return;

                    // re-enable IMU
                    if (args == BluetoothConnectionStatus.Connected)
                    {                        
                        await ConfigureImu();
                        await StartImu();
                        Status = ApplicationStatusType.Connected;
                    }
                    else
                    {
                        _bluetera.Dispose();
                        _bluetera = null;
                        Status = ApplicationStatusType.Idle;
                    }
                }
                catch(BlueteraException)
                {
                    Status = ApplicationStatusType.Failed;
                }
            });
        }

        private void _bluetera_DownlinkMessageReceived(BlueteraDevice sender, DownlinkMessage args)
        {
            Dispatcher.InvokeAsync(() =>
            {
                switch(args.PayloadCase)
                {
                    case DownlinkMessage.PayloadOneofCase.Quaternion:
                        {
                            // apply rotation
                            Quaternion q = new Quaternion(args.Quaternion.X, args.Quaternion.Y, args.Quaternion.Z, args.Quaternion.W);
                            CubeModel.Transform = new RotateTransform3D(new QuaternionRotation3D(q));
                        }
                        break;

                    default:
                        /* Currently ignore all other message types */
                        break;
                }
            });
        }
        #endregion

        #region Helpers
        #endregion
        private async Task ConfigureImu()
        {
            UplinkMessage msg = new UplinkMessage()
            {
                Imu = new ImuCommand
                {
                    Config = new ImuConfig()
                    {
                        DataTypes = (uint)ImuDataType.Accelerometer,    // ImuDataType are enum flags - logically 'OR' to combine several types
                        Odr = 50,                                       // Output Data Rate [Hz]
                        AccFsr = 4,                                     // Acceleromenter Full Scale Range [g]
                        GyroFsr = 500                                   // Gyroscope Full Scale Range [deg/sec]
                    }
                }
            };

            await SendOrThrow(msg);
        }

        private async Task StartImu()
        {
            UplinkMessage msg = new UplinkMessage() 
            {
                Imu = new ImuCommand()
                {
                    Start = true
                }
            };

            await SendOrThrow(msg);
        }

        private async Task StopImu()
        {
            UplinkMessage msg = new UplinkMessage()
            {
                Imu = new ImuCommand()
                {
                    Stop = true
                }
            };

            await SendOrThrow(msg);
        }

        private async Task SendOrThrow(UplinkMessage msg)
        {
            var status = await _bluetera.SendMessage(msg);
            if (status != GattCommunicationStatus.Success)
                throw new BlueteraException($"Operation failed. Status = {GattCommunicationStatus.Success}");
        }
        #endregion
    }
}
