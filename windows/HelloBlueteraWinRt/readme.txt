In order to use Bluetooth LE in a desktop application, you will need to manually add refrences to some assemblies:

1) Windows.winmd - in C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0\ExtensionSDKs\Microsoft.VCLibs\11.0\References\CommonConfiguration\neutral
2) System.Runtime.WindowsRuntime.dll - in C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v4.5

You should also probably read:
- https://blogs.windows.com/buildingapps/2017/01/25/calling-windows-10-apis-desktop-application/#otXBUtkJEOu1Lhyz.97
- https://software.intel.com/en-us/articles/using-winrt-apis-from-desktop-applications