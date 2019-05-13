<img src=images/iotera_logo.png width="10%" height="10%"></br>
# Hello Bluetera - Windows
A collection of sample code and reusable classes and libraries to work with the Bluetera platform for Windows.</br></br>
Visit our website: https://ioteratech.com

## Getting Started
Bluetera is an open source IoT platform for the development of smart and connected products. The platform includes:
* Bluetera Hardware module
* Bluetera Firmware
* Blutera SDK - various platforms are supported. This repository on Windows

### Prerequisits
- Windows 10 build 1803 or later, with a Bluetooth 4+ adapter
- A Bluetera module - you can buy one [here](https://ioteratech.com), or use the Bluetera Emulator (coming soon)
- Visual Studio 2017 or later
- (optional) Protocol buffers compiler v3+ (available [here](https://developers.google.com/protocol-buffers/docs/downloads))
 
### Building
- Open the solution file *HelloBluetera.sln* in VS2017
- Build the desired project/configuration. The projects require some NuGet pacakges - make sure they correctly install
- When Building the non-UWP projects (*HelloBlueteraWinRt* / *HelloBlueteraWpf* ), you may need to [manually update the paths of some Windows 10 API refrences](#winmd).

### Running
For starters, we recommend running the *HelloBlueteraWpf* project:
* Make sure your PC's Bluetooth is on
* Turn your Bluetera module on
* Build and run *HelloBlueteraWpf*
* Click 'Start'


## What's in the box?
- *BlueteraSDK* - a shared project which implements some glue code
- *HelloBlueteraWinRt* - a WinRt (i.e. non-UWP) console application
- *HelloBlueteraWpf* - a WPF rotating-cube application
- *HelloBlueteraUwp* - coming soon

## Authors
* **Boaz Aizenshtark** - [Iotera](https://ioteratech.com/company/)
* **Tomer Abramovich** - [Iotera](https://ioteratech.com/company/)
* **Avi Rabinovich** - [Iotera](https://ioteratech.com/company/)

## License
This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## Notes
##### <a name="winmd"></a> Adding refrences to Windows 10 API
Windows 10 API is not available out-of-the-box for WPF. 
However, for Windows 10 Desktop application, you can still access some APIs, specifically the Bluetooth device, which is needed to communicate with Bluetera.

When you create a new WPF/Console application, you will have to manually add some refrences to the project. 
This is described in detail [here](https://blogs.windows.com/buildingapps/2017/01/25/calling-windows-10-apis-desktop-application/),
but ultimatly boils down to adding refrences to:

- C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.17763.0\Windows.winmd
- C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v4.5\System.Runtime.WindowsRuntime.dll

If you are missing the specific Windows Kit, you can use other compatible versions.

