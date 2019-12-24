using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

namespace Wrapper
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

    internal static class SdkWrapper
    {
        private const string DLLName = "sdk";

        static SdkWrapper()
        {
            string path = new Uri(typeof(SdkWrapper).Assembly.CodeBase).LocalPath;
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
        public delegate void OnUartTx([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]byte[] data, ushort length);

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
        public static extern StatusCode SetOnUartTx(string addr, OnUartTx func);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_StartScan")]
        public static extern StatusCode StartScan(bool filter = true);

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_StopScan")]
        public static extern StatusCode StopScan();

        [DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IOTE_Write")]
        public static extern StatusCode Write(string addr, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] data, ushort length);
    }
}