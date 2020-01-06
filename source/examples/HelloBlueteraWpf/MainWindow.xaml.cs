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
using LiveCharts;
using HelixToolkit.Wpf;

using Bluetera;
using Bluetera.Utilities;
//using Bluetera.Windows; // To use Windows native BLE, uncomment this line and comment the next oune
using Bluetera.Iotera; // Recommended - To use Iotera's driver, uncomment this line and comment the previous one

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace HelloBlueteraWpf
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Constants / Enums
        public enum ApplicationStateType { Idle, Scanning, Connecting, Connected, Disconnecting, Error }
        #endregion

        #region Fields
        private DataRateMeter _dataRateMeter = new DataRateMeter();
        private uint _odr = 50;
        private static Logger _qLogger = LogManager.GetLogger("QuaternionLogger");
        private static Logger _aLogger = LogManager.GetLogger("AccLogger");
        #endregion

        #region Lifecycle
        public MainWindow()
        {
            InitializeComponent();

            // Model
            ObjReader CurrentHelixObjReader = new ObjReader();
            model.Content = CurrentHelixObjReader.Read(@"D:\Users\Boaz\Desktop\temp\Airplane_v2_L2.123c9fd3dbfa-7118-4fde-af56-f04ef61f45dd\11804_Airplane_v2_l2.obj");
            //model.Content = CurrentHelixObjReader.Read(@"D:\Users\Boaz\Desktop\temp\Brown_Betty_Teapot_v1_L1.123c0890bb91-c798-45a4-8c00-bdb74366c50e\20900_Brown_Betty_Teapot_v1.obj");

            // Graph
            AccelerationValues_X = new ChartValues<double>();
            AccelerationValues_Y = new ChartValues<double>();
            AccelerationValues_Z = new ChartValues<double>();
            AccelerationYFormatter = value => value.ToString("0.0");

            // Data Rate Gauge
            DataRateGauge.AnimationsSpeed = new TimeSpan(0, 0, 0, 0, 100);

            DataContext = this;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _btManager = BlueteraManager.Instance;
            _btManager.AdvertismentReceived += _btManager_AdvertismentReceived;

            ApplicationState = ApplicationStateType.Idle;
            UpdateControls();
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
        // Status bar
        private ApplicationStateType _applicationState = ApplicationStateType.Idle;
        public ApplicationStateType ApplicationState
        {
            get { return _applicationState; }
            set { _applicationState = value; NotifyPropertyChanged(); }
        }

        // Accelaration chart and values
        public ChartValues<double> AccelerationValues_X { get; set; }
        public ChartValues<double> AccelerationValues_Y { get; set; }
        public ChartValues<double> AccelerationValues_Z { get; set; }
        public Func<double, string> AccelerationYFormatter { get; set; }

        private double _accX = 0.0;
        public double AccX
        {
            get { return _accX; }
            set { _accX = value; NotifyPropertyChanged(); }
        }

        private double _accY = 0.0;
        public double AccY
        {
            get { return _accY; }
            set { _accY = value; NotifyPropertyChanged(); }
        }

        private double _accZ = 0.0;
        public double AccZ
        {
            get { return _accZ; }
            set { _accZ = value; NotifyPropertyChanged(); }
        }

        private double _roll = 0.0;
        public double Roll
        {
            get { return _roll; }
            set { _roll = value; NotifyPropertyChanged(); }
        }

        private double _pitch = 0.0;
        public double Pitch
        {
            get { return _pitch; }
            set { _pitch = value; NotifyPropertyChanged(); }
        }

        private double _yaw = 0.0;
        public double Yaw
        {
            get { return _yaw; }
            set { _yaw = value; NotifyPropertyChanged(); }
        }

        private double _dataRate = 0.0;
        public double DataRate
        {
            get { return _dataRate; }
            set { _dataRate = value; NotifyPropertyChanged(); }
        }

        #endregion

        #region Event handlers
        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if ((ApplicationState == ApplicationStateType.Idle) || (ApplicationState == ApplicationStateType.Error))
            {   // Button is 'Start'
                _btManager.StartScan();

                // Update UI
                ApplicationState = ApplicationStateType.Scanning;
                UpdateControls();
            }
            else
            {   // Button is 'Stop' - depending on the state, it either: stops scanning, disconnects, aborts connection attempt
                // It is OK to call StopScan(), even if we are not scanning
                StopAll();
            }
        }

        private void SetHeadingButton_Click(object sender, RoutedEventArgs e)
        {
            // Capture last reeived Quaternion as initial rotation
            _qbm = QuaternionExtensions.CalcBodyToImuRotationFromPitch(_q0.Inverse(), _qt);
            _q0 = (_q0.Inverse() * _qbm).Inverse();
        }

        #region Helpers
        private void UpdateControls()
        {
            switch (ApplicationState)
            {
                case ApplicationStateType.Idle:
                case ApplicationStateType.Error:
                    StartStopButton.Content = "Start";
                    DeviceLabel.Content = "";
                    DataRate = 0;
                    break;

                case ApplicationStateType.Scanning:
                case ApplicationStateType.Connecting:
                    StartStopButton.Content = "Stop";
                    DeviceLabel.Content = "";
                    DataRate = 0;
                    break;

                case ApplicationStateType.Connected:
                case ApplicationStateType.Disconnecting:
                    StartStopButton.Content = "Stop";
                    DeviceLabel.Content = _bluetera?.AddressAsString;
                    break;
            }
        }
        #endregion
        #endregion
        #endregion

        #region Bluetera
        #region Fields
        private Quaternion _qbm = Quaternion.Identity;
        private Quaternion _q0 = Quaternion.Identity;
        private Quaternion _qt = Quaternion.Identity;
        private BlueteraManager _btManager;
        private IBlueteraDevice _bluetera;
        #endregion

        #region Event Handlers
        private void _btManager_AdvertismentReceived(IBlueteraManager sender, BlueteraAdvertisement args)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // avoid race 
                    if (ApplicationState != ApplicationStateType.Scanning)
                        return;

                    _btManager.StopScan();

                    _dataRateMeter.Reset();
                    ApplicationState = ApplicationStateType.Connecting;
                    UpdateControls();

                    // Try connecting to Bluetera                    
                    _bluetera = await _btManager.Connect(args.Address);  // This method will either connect or throw
                    _bluetera.ConnectionStatusChanged += _bluetera_ConnectionStatusChanged;
                    _bluetera.DownlinkMessageReceived += _bluetera_DownlinkMessageReceived;

                    // Start IMU. Methods will throw on failure
                    await StartImu();

                    // update UI
                    ApplicationState = ApplicationStateType.Connected;
                    UpdateControls();
                }
                catch (BlueteraException ex)
                {
                    _bluetera?.Disconnect();
                    _bluetera = null;

                    ApplicationState = ApplicationStateType.Error;
                    UpdateControls();
                }
            });
        }

        private void _bluetera_ConnectionStatusChanged(IBlueteraDevice sender, ConnectionStatus args)
        {
            Dispatcher.Invoke(async () =>
            {
                try
                {
                    // avoid some potential races
                    if (sender != _bluetera)
                        return;

                    // re-enable IMU
                    if (args == ConnectionStatus.Connected)
                    {
                        await StartImu();
                        ApplicationState = ApplicationStateType.Connected;
                        UpdateControls();
                    }
                    else
                    {
                        StopAll();  // This will prevent auto-reconnection
                    }
                }
                catch (BlueteraException)
                {
                    ApplicationState = ApplicationStateType.Error;
                }
            });
        }

        private void _bluetera_DownlinkMessageReceived(IBlueteraDevice sender, DownlinkMessage args)
        {
            Dispatcher.Invoke(() =>
            {
                // update data rate UI
                //DataRate = _dataRateMeter.DataRate;
                switch (args.PayloadCase)
                {
                    case DownlinkMessage.PayloadOneofCase.Quaternion:
                        {
                            // log
                            //_qLogger.Info(args.Quaternion.ToString());

                            // update rate meter
                            _dataRateMeter.Update(args.Quaternion.Timestamp);
                            DataRate = _dataRateMeter.DataRate;

                            // raw Bluetera quaternion
                            var qb = new Quaternion(args.Quaternion.X, args.Quaternion.Y, args.Quaternion.Z, args.Quaternion.W);

                            // change coordinates and apply rotation - see note (2) at the end of this file                            
                            _qt = new Quaternion(qb.X, qb.Y, qb.Z, qb.W);

                            if (_q0.IsIdentity)
                                _q0 = _qt.Inverse();

                            model.Transform = new RotateTransform3D(new QuaternionRotation3D(_q0 * _qt * _qbm));   // we multiple by q0 to apply 'reset cube' operation

                            // update Euler angles
                            var angles = qb.GetEuelerAngles();
                            Roll = angles[0];
                            Pitch = angles[1];
                            Yaw = angles[2];
                        }
                        break;

                    case DownlinkMessage.PayloadOneofCase.Acceleration:

                        // log
                        //_qLogger.Info(args.Acceleration.ToString());

                        //// update rate meter
                        //_dataRateMeter.Update(args.Acceleration.Timestamp);
                        //DataRate = _dataRateMeter.DataRate;

                        // Update chart
                        AccX = args.Acceleration.X;
                        AccelerationValues_X.Add(AccX);
                        if (AccelerationValues_X.Count > 100) AccelerationValues_X.RemoveAt(0);

                        AccY = args.Acceleration.Y;
                        AccelerationValues_Y.Add(AccY);
                        if (AccelerationValues_Y.Count > 100) AccelerationValues_Y.RemoveAt(0);

                        AccZ = args.Acceleration.Z;
                        AccelerationValues_Z.Add(AccZ);
                        if (AccelerationValues_Z.Count > 100) AccelerationValues_Z.RemoveAt(0);
                        break;

                    default:
                        /* Currently ignore all other message types */
                        break;
                }
            });
        }

        private void StopAll()
        {
            try
            {
                // Stop scanning - will be ignored if not relevant
                _btManager.StopScan();

                // Dispose Bluetera device. Will disconnect if needed
                _bluetera?.Disconnect();
                _bluetera = null;
            }
            catch (Exception) { /* Ignore */}

            ApplicationState = ApplicationStateType.Idle;
            UpdateControls();
        }
        #endregion

        #region Helpers
        private async Task StartImu()
        {
            UplinkMessage msg = new UplinkMessage()
            {
                Imu = new ImuCommand
                {
                    Start = new ImuStart()
                    {
                        DataTypes = (uint)(ImuDataType.Accelerometer | ImuDataType.Quaternion), // ImuDataType are enum flags - logically 'OR' to combine several types
                        Odr = _odr,                                                             // Output Data Rate [Hz] - see note (1) at the end of this file before changing this value
                        AccFsr = 4,                                                             // Acceleromenter Full Scale Range [g]
                        GyroFsr = 500                                                           // Gyroscope Full Scale Range [deg/sec]
                    }
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
            if (!status)
                throw new BlueteraException($"Operation failed.");
        }
        #endregion

        #endregion

        private async void RateUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_odr <= 990)
                _odr += 10;

            await StartImu();
        }

        private async void RateDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_odr >= 20)
                _odr -= 10;

            await StartImu();
        }
    }
}

/*
 * Notes:
 * 
 * 1. While Bluetera will happily stream Acceleration and Quaternion data at 200 samples/sec, some machines we tested choked on this (Windows 10 Pro, Version 10.0.17763).
 *    To try out higher data rate, either disable Acceleration, buy a stronger machine, or wait until the issue is resolved.
 *    If the machine does choke, the Bluetera will be disconnect, but you will not get a 'disconnect' event, and you will have to reset your adapter (Windows-->Settings-->Bluetooth)
 *    
 * 2. We also change coordinates from Bluetera-Frame to WPF-Frame. Also note Bluetera is left-handed whereas WPF is right-handed
 *    WPF graphics: https://docs.microsoft.com/en-us/dotnet/framework/wpf/graphics-multimedia/3-d-graphics-overview
 * */


