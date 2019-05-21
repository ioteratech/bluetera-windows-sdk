<img src=docs/images/iotera_logo.png width="10%" height="10%"></br>
# Bluetera Windows SDK
A collection of sample code and reusable classes and libraries to work with the Bluetera platform for Windows.</br></br>
Visit our website: https://ioteratech.com

## Getting Started
Bluetera is an open source IoT platform for the development of smart and connected products. Communication is built around Protocol Buffers, which makes it super easy to add functionality and modify the API. The platform includes:
* Bluetera Hardware module - repository (coming soon)
* Bluetera Firmware - repository [here](https://github.com/ioteratech/bluetera-firmware)
* Blutera SDK(s) - this repository is the Windows SDK
 

### Prerequisites
- Windows 10 build 1803 or later, with a Bluetooth 4+ adapter
- Visual Studio 2017 or later
- Windows 10 SDK version 10.0.17763.0 or higher
- A Bluetera module - buy one [here](https://ioteratech.com), or use the Bluetera Emulator (coming soon)
- (optional) Protocol buffers compiler v3+ (available [here](https://developers.google.com/protocol-buffers/docs/downloads))
 
### Building
- Open the solution file *source\examples\HelloBluetera.sln* in VS2017
- Build the desired project/configuration. The projects require some NuGet packages - make sure they correctly install
- When Building the non-UWP projects (*HelloBlueteraWinRt* and *HelloBlueteraWpf* ), you may need to [manually update the paths of some Windows 10 API references](#winmd).

### Running
For starters, we recommend running the *HelloBlueteraWpf* project:
* Make sure your PC's Bluetooth is on
* Turn your Bluetera module on
* Build and run *HelloBlueteraWpf*
* Run the demo, turn on your Bluetera, and click 'Start'

<img src=docs/images/bluetera-logger.png width="80%" height="80%"></br>

## What's in the box?
- *BlueteraSDK* - a shared project which implements some convenience code
- *HelloBlueteraWinRt* - a WinRt (as in non-UWP) console application
- *HelloBlueteraWpf* - a WPF rotating-cube application
- *HelloBlueteraUwp* - coming soon

## Contributing
* When contributing to this repository, please first discuss the change you wish to make via issue, email, or any other method with the owners of this repository before making a change
* Your code should match the project's code style
* Your code must build without errors nor warnings
* Once ready, create a pull request

## Authors
* **Boaz Aizenshtark** - [Iotera](https://ioteratech.com/company/)
* **Tomer Abramovich** - [Iotera](https://ioteratech.com/company/)
* **Avi Rabinovich** - [Iotera](https://ioteratech.com/company/)

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Notes
##### <a name="winmd"></a> Adding references to Windows 10 API
Windows 10 API is not available out-of-the-box for WPF. 
However, for Windows 10 Desktop application, you can still access some APIs, specifically the Bluetooth device, which is needed to communicate with Bluetera.

When you create a new WPF/Console application, you will have to manually add some references to the project. 
This is described in detail [here](https://blogs.windows.com/buildingapps/2017/01/25/calling-windows-10-apis-desktop-application/),
but ultimately boils down to adding references to:

- C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.17763.0\Windows.winmd
- C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\\.NETCore\v4.5\System.Runtime.WindowsRuntime.dll

If you are missing the specific Windows Kit, you can use other compatible versions.

