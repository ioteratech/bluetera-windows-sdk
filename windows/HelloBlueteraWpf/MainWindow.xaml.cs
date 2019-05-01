﻿using System;
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
        private ApplicationStateType _applicationState = ApplicationStateType.Idle;
        public ApplicationStateType ApplicationState
        {
            get { return _applicationState; }
            set { _applicationState = value; NotifyPropertyChanged(); }
        }
        #endregion       

        #region Event handlers
        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if ((ApplicationState == ApplicationStateType.Idle) || (ApplicationState == ApplicationStateType.Error))
            {   // Button is 'Start'
                _sdk.StartScan();

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

        private void ResetCubeButton_Click(object sender, RoutedEventArgs e)
        {
            // Capture last reeived Quaternion as initial rotation
            _q0 = _qt;
            _q0.Normalize();
            _q0.Invert();
        }

        #region Helpers
        private void UpdateControls()
        {
            switch(ApplicationState)
            {
                case ApplicationStateType.Idle:
                case ApplicationStateType.Error:
                    StartStopButton.Content = "Start";
                    DeviceLabel.Content = "";
                    break;

                case ApplicationStateType.Scanning:
                case ApplicationStateType.Connecting:                
                    StartStopButton.Content = "Stop";
                    DeviceLabel.Content = "";
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
                    UpdateControls();

                    // Try connecting to Bluetera                    
                    _bluetera = await _sdk.Connect(args.BluetoothAddress, true);  // This method will either connect or throw
                    _bluetera.ConnectionStatusChanged += _bluetera_ConnectionStatusChanged;
                    _bluetera.DownlinkMessageReceived += _bluetera_DownlinkMessageReceived;

                    ApplicationState = ApplicationStateType.Connected;
                    UpdateControls();

                    // Start IMU. Methods will throw on failure
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
                    UpdateControls();
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
                            CubeModel.Transform = new RotateTransform3D(new QuaternionRotation3D(_q0 * _qt));
                        }
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
                _sdk.StopScan();            

                // Dispose Bluetera device. Will disconnect if needed
                if (_bluetera != null)
                {
                    _bluetera.Dispose();
                    _bluetera = null;
                }
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
                        DataTypes = (uint)ImuDataType.Quaternion,       // ImuDataType are enum flags - logically 'OR' to combine several types
                        Odr = 50,                                       // Output Data Rate [Hz]
                        AccFsr = 4,                                     // Acceleromenter Full Scale Range [g]
                        GyroFsr = 500                                   // Gyroscope Full Scale Range [deg/sec]
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
            if (status != GattCommunicationStatus.Success)
                throw new BlueteraException($"Operation failed. Status = {GattCommunicationStatus.Success}");
        }
        #endregion
        #endregion
    }
}
