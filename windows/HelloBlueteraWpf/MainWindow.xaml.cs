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
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Constants / Enums
        public enum ApplicationStateType { Idle, Scanning, Connecting, Connected, Disconnecting, Error }
        #endregion

        #region Lifecycle
        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _sdk = BlueteraSdk.Instance;
            _sdk.AdvertismentReceived += _sdk_AdvertismentReceived;

            ApplicationState = ApplicationStateType.Idle;
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

        #region Bound Properties
        private ApplicationStateType _applicationState = ApplicationStateType.Idle;
        public ApplicationStateType ApplicationState
        {
            get { return _applicationState; }
            set { _applicationState = value; NotifyPropertyChanged(); }
        }
        #endregion

        #region Event handlers
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ApplicationState = ApplicationStateType.Scanning;
            _sdk.StartScan();
        }

        private void ResetCubeButton_Click(object sender, RoutedEventArgs e)
        {
            _q0 = _qt;
        }
        #endregion
        #endregion

        #region Bluetera
        #region Fields
        private Quaternion _q0 = Quaternion.Identity;
        private Quaternion _qt = Quaternion.Identity;
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
                    _sdk.StopScan();

                    ApplicationState = ApplicationStateType.Connecting;

                    // Try connecting to Bluetera                    
                    _bluetera = await _sdk.Connect(args.BluetoothAddress);  // This method will either connect or throw
                    _bluetera.ConnectionStatusChanged += _bluetera_ConnectionStatusChanged;
                    _bluetera.DownlinkMessageReceived += _bluetera_DownlinkMessageReceived;

                    ApplicationState = ApplicationStateType.Connected;
                    DeviceLabel.Content = _bluetera.AddressAsString;

                    // Configure and Start IMU. Methods will throw on failure
                    await ConfigureImu();
                    await StartImu();
                }
                catch (BlueteraException)
                {
                    if (_bluetera != null)
                    {
                        _bluetera.Dispose();
                        _bluetera = null;
                    }

                    ApplicationState = ApplicationStateType.Error;
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
                        ApplicationState = ApplicationStateType.Connected;
                        DeviceLabel.Content = _bluetera.AddressAsString;
                    }
                    else
                    {
                        _bluetera.Dispose();
                        _bluetera = null;
                        ApplicationState = ApplicationStateType.Idle;
                        DeviceLabel.Content = null;
                    }
                }
                catch (BlueteraException)
                {
                    ApplicationState = ApplicationStateType.Error;
                }
            });
        }

        private void _bluetera_DownlinkMessageReceived(BlueteraDevice sender, DownlinkMessage args)
        {
            Dispatcher.InvokeAsync(() =>
            {
                switch (args.PayloadCase)
                {
                    case DownlinkMessage.PayloadOneofCase.Quaternion:
                        {
                            // apply rotation
                            _qt = new Quaternion(args.Quaternion.X, args.Quaternion.Y, args.Quaternion.Z, args.Quaternion.W);
                            CubeModel.Transform = new RotateTransform3D(new QuaternionRotation3D(_qt));
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
        #endregion
    }
}
