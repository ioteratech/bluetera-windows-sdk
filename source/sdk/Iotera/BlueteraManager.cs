using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Bluetera;
using Bluetera.Utilities;

namespace Bluetera.Iotera
{
    public class BlueteraManager : IBlueteraManager
    {
        #region Fields
        private static readonly Lazy<BlueteraManager> _instance = new Lazy<BlueteraManager>(() => new BlueteraManager());

        private ConcurrentDictionary<ulong, BlueteraDevice> _connectedDevices = new ConcurrentDictionary<ulong, BlueteraDevice>();
        private ConcurrentDictionary<ulong, TaskCompletionSource<BlueteraDevice>> _connectTcs = new ConcurrentDictionary<ulong, TaskCompletionSource<BlueteraDevice>>();
        #endregion

        #region Proporties        
        public static BlueteraManager Instance
        {
            get { return _instance.Value; }
        }
        #endregion

        #region Events
        public event TypedEventHandler<IBlueteraManager, BlueteraAdvertisement> AdvertismentReceived;
        #endregion

        #region Event Forwarding
        private NativeSdkWrapper.OnDeviceDiscovered _onDeviceDiscoveredDelegate = new NativeSdkWrapper.OnDeviceDiscovered(OnDeviceDiscoveredInternal);
        private NativeSdkWrapper.OnDeviceConnected _onDeviceConnectedDelegate = new NativeSdkWrapper.OnDeviceConnected(OnDeviceConnectedInternal);
        private NativeSdkWrapper.OnDeviceDisconnected _onDeviceDisconnectedDelegate = new NativeSdkWrapper.OnDeviceDisconnected(OnDeviceDisconnectedInternal);
        private NativeSdkWrapper.OnUartTx _onUartTx = new NativeSdkWrapper.OnUartTx(OnUartTx);

        private static void OnDeviceDiscoveredInternal(string addr)
        {
            BlueteraAdvertisement adv = new BlueteraAdvertisement()
            {
                Address = BlueteraUtilities.StringAddresssAsUlong(addr),
                Rssi = Double.NaN   // TODO: Native SDK to return RSSI
            };

            Instance.AdvertismentReceived?.Invoke(Instance, adv);
        }

        private static void OnDeviceConnectedInternal(string addrStr)
        {
            Console.WriteLine("OnDeviceConnectedInternal");

            ulong addr = BlueteraUtilities.StringAddresssAsUlong(addrStr);

            // create and hook up device
            BlueteraDevice device = new BlueteraDevice(addr);            

            // add to tracked devices            
            Instance._connectedDevices.TryAdd(addr, device);

            // notify pending tasks
            TaskCompletionSource<BlueteraDevice> tcs;
            if (Instance._connectTcs.TryRemove(addr, out tcs))
                tcs.SetResult(device);            
        }

        private static void OnDeviceDisconnectedInternal(string addrStr)
        {
            ulong addr = BlueteraUtilities.StringAddresssAsUlong(addrStr);

            BlueteraDevice device;
            if (Instance._connectedDevices.TryRemove(addr, out device))
                device.OnConnectionStatusChanged(ConnectionStatus.Disconnected);
        }

        private static void OnUartTx(string addrStr, byte[] data, ushort length)
        {
            if (data == null || length == 0)
                return;

            BlueteraDevice device;
            if(Instance._connectedDevices.TryGetValue(BlueteraUtilities.StringAddresssAsUlong(addrStr), out device))
                device.OnUartTx(data, length);
        }
        #endregion

        #region Methods
        public async Task<IBlueteraDevice> Connect(ulong addr, bool autopair = false)
        {
            // ignore duplicate requests
            TaskCompletionSource<BlueteraDevice> tcs = new TaskCompletionSource<BlueteraDevice>();
            if (!_connectTcs.TryAdd(addr, tcs))
                return null;

            // send command            
            string addrAsString = BlueteraUtilities.UlongAddressAsString(addr);
            NativeSdkWrapper.Connect(addrAsString);

            // _connectedTcs will fire when _onDeviceConnectedDelegate() is called 
            return await tcs.Task.ContinueWith(t => (IBlueteraDevice)t.Result);
        }

        public void StartScan()
        {
            NativeSdkWrapper.StartScan();
        }

        public void StopScan()
        {
            NativeSdkWrapper.StopScan();
        }
        #endregion

        #region Lifecycle
        public BlueteraManager()
        {
            var status = NativeSdkWrapper.Initialize();
            if (status != NativeSdkWrapper.StatusCode.Success)
                throw new BlueteraException("Native SDK initialization failed");

            NativeSdkWrapper.SetOnDeviceDiscovered(_onDeviceDiscoveredDelegate);
            NativeSdkWrapper.SetOnDeviceConnected(_onDeviceConnectedDelegate);
            NativeSdkWrapper.SetOnDeviceDisconnected(_onDeviceDisconnectedDelegate);
            NativeSdkWrapper.SetOnUartTx(_onUartTx);
        }
        #endregion
    }


    internal static class NativeSdkWrapper
    {
        public enum StatusCode : int
        {
            Success = 0,
            Failed = 1,
            InvalidHandle = 2,
            ConnectionInProgress = 3,
            AlreadyExists = 4,
            NotInitialized = 5,
            UnavailableBle = 6
        }

        private const string DLLName = "sdk";

        static NativeSdkWrapper()
        {
            string path = new Uri(typeof(NativeSdkWrapper).Assembly.CodeBase).LocalPath;
            string current_folder = Path.GetDirectoryName(path);
            string subfolder = Environment.Is64BitProcess ? "\\x64\\" : "\\x86\\";

            LoadLibrary(current_folder + subfolder + DLLName);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnDeviceDiscovered(string addr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnDeviceConnected(string addr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnDeviceDisconnected(string addr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OnUartTx([MarshalAs(UnmanagedType.LPStr)] string addr, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]byte[] data, ushort length);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_Initialize")]
        public static extern StatusCode Initialize();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_Terminate")]
        public static extern StatusCode Terminate();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_Connect")]
        public static extern StatusCode Connect(string addr);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_Disconnect")]
        public static extern StatusCode Disconnect(string addr);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_SetOnDeviceDiscovered")]
        public static extern StatusCode SetOnDeviceDiscovered(OnDeviceDiscovered func);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_SetOnDeviceConnected")]
        public static extern StatusCode SetOnDeviceConnected(OnDeviceConnected func);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_SetOnDeviceDisconnected")]
        public static extern StatusCode SetOnDeviceDisconnected(OnDeviceDisconnected func);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_SetOnUartTx")]
        public static extern StatusCode SetOnUartTx(OnUartTx func);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_StartScan")]
        public static extern StatusCode StartScan(bool filter = true);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_StopScan")]
        public static extern StatusCode StopScan();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_Write")]
        public static extern StatusCode Write(string addr, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] data, ushort length);
    }
}
