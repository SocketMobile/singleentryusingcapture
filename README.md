# Single Entry using Capture SDK

## Introduction
This is a sample application to show how to use the Socket Mobile Capture SDK.

The Socket Mobile Capture SDK is available as NuGet from NuGets.org.

When loading the project in Visual Studio, the Capture NuGet should restore itself from NuGets.org.

The Socket Mobile Capture relies on a service running on the host in order to manage the connection to a Socket Mobile scanner.

This service is called Socket Mobile Companion. If you have installed the Socket Mobile Keyboard package or the Socket Mobile Companion package, this service comes with an UI called Socket Mobile Companion UI. From that UI you can check the version and the status of the service.

The Socket Mobile Companion is also required when connecting a scanner for the first time to the host as it will configure the scanner during the first connection process.

## Connecting a Socket Mobile scanner
In order to connect a Socket Mobile scanner to a Windows host, an initial setting should be done in order to pair and configure the scanner for a particular host.

This phase requires the Socket Mobile Companion UI to be running on the Windows host machine.

Once the Socket Mobile Companion UI is running, it is easy to connect the Socket Mobile scanner by clicking on the Socket Mobile Companion UI and select "Help to connect" from the popup menu of this application. This displays a dialog with a barcode that can be scanned in order to configure the scanner in the right Application Mode.

Once this is done, the dialog can be closed, and the scanner is ready to be discovered by the Windows host machine, by going to the Bluetooth settings of the Windows machine. Once the scanner appears in the list of discovered devices, clicking on the scanner name will start the pairing process. During that process the Socket Mobile Companion UI configuration dialog appears to indicates it is configuring the scanner to reconnect back to this host machine. Once this operation completes, the scanner beeps to indicates it is ready to be used.

The next time you power on the scanner, it will reconnect automatically to this host. If it needs to connect to a different host, its pairing should be deleted. For this specific operation please refer to the scanner user guide and follow the steps to clear the bonding.

## Using Capture SDK
First the connection and disconnection process is independent of the application. The application receives a device arrival notification when a Socket Mobile scanner is connected to the host, and a device removal when it disconnects.

The best way of using Capture, is to use it with Capture Helper.

Capture Helper provides most of the APIs Capture offers, and hide most the complexity of handling the scanner properties.

Capture Helper asynchronous event handlers should be set up before opening Capture Helper.
Here is an example that came from the source of this sample application:
```
mCapture.DeviceArrival += mCapture_DeviceArrival;
mCapture.DeviceRemoval += mCapture_DeviceRemoval;
mCapture.DecodedData += mCapture_DecodedData;
```

If the application UI needs to be updated from these handler, or in the callbacks of the Capture Helper API methods, a context can be set once also before opening Capture Helper as follow:
`mCapture.ContextForEvents = WindowsFormsSynchronizationContext.Current;`

This sample app uses a timer to open Capture. This is due in part to handle the case that the Socket Mobile Companion service is not running at the time of this application is trying to use and can be retried.

The scanner decoded data are received by the application by providing a decoded data handler to the Capture Helper event: `DecodedData`.

## Opening Capture - Application Registration
Opening Capture requires some application information, such as the application ID, a developer ID and the application AppKey.
These information requires a Socket Mobile Developer account that can be created on the Socket Mobile developer portal. The application AppKey is retrieved when registering the application in the same Socket Mobile developer portal and by providing the application ID.  

## Documentation
For more information please consult the Capture SDK documentation that can be found here: http://docs.socketmobile.com/capture-preview/csharp/en/latest/index.html
