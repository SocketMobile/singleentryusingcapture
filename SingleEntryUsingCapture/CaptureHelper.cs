//
// CaptureHelper.cs
// 
// This file helps to use Capture SDK in a friendly way 
// by implementing the most commonly used Capture API.
// 
// (c) Socket Mobile, Inc.
// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SocketMobile
{
    namespace Capture
    {
#if DEBUG
        /// <summary>
        /// interface to catch the DEBUG traces in the App
        /// </summary>
        public interface CaptureHelperDebug
        {
            /// <summary>
            /// print a message to the debug output window
            /// </summary>
            /// <param name="message"></param>
            void PrintLine(string message);
        }
#endif
        /// <summary>
        /// Main class for using Capture.
        /// When a device is connected, the DeviceArrival event
        /// is fired with a CaptureHelperDevice sets as a reference
        /// to the device. Any operation related to this particular 
        /// device should use that instance of CaptureHelperDevice.
        /// When a device is disconnected from the host then a Device
        /// Removal event is fired. Whatever operation done afterward
        /// using its CaptureHelperDevice instance will result in a failure
        /// case.
        /// Initialization: 
        /// 1. create a new instance of CaptureHelper
        /// 2. set the property ContextForEvents to your UI context 
        /// if you want to refresh the UI with any events coming from
        /// CaptureHelper or CaptureHelperDevice
        /// 3. install an handler on the CaptureHelper events that is required
        /// by the application. Most of the time, DecodedData, DeviceArrival,
        /// DeviceRemoval, Error and Terminated are handled by the application.
        /// 4. open CaptureHelper.
        /// To terminate properly the usage of CaptureHelper, the SetAbortAsync
        /// function should be called, upon which the Terminate event handler can then
        /// close everything and release any resources.
        /// In the case where the Socket Mobile Companion service has a fatal error
        /// like a disconnection of the websocket, then a Terminate event is fired
        /// this time with Result indicating an error. The best is to close everything
        /// and try again to re-open CaptureHelper.
        /// If opening CaptureHelper result in an error, this might be because the
        /// Socket Mobile Companion software is either not present on the host or stopped.
        /// </summary>
        public partial class CaptureHelper
        {
            private ICapture _capture;
            private List<CaptureHelperDevice> _devices = new List<CaptureHelperDevice>();

            /// <summary>
            /// Force Capture to not use WebSocket. This is useful only on platforms that
            /// don't support correctly WebSocket.
            /// </summary>
            public bool DoNotUseWebSocket;

            /// <summary>
            /// provides a UI Thread context in order to receive the CaptureHelper and
            /// CaptureHelperDevice event in the context of the UI thread.
            /// For example: captureHelper.ContextForEvents = SynchronizationContext.Current;
			/// Setting this is optional, if left null CaptureHelper will not 
			/// try to update the UI.
			/// Note: Setting this MUST be done on the UI thread only. See Microsoft
			/// documentation for SynchronizationContext.
            /// </summary>
            public SynchronizationContext ContextForEvents;

#if DEBUG
            /// <summary>
            /// Interface for Debug traces that can be used by the App to 
            /// troubleshoot a particular issue.
            /// </summary>
            public CaptureHelperDebug DebugConsole;
#endif
            /// <summary>
            /// the PowerState of a device
            /// </summary>
            public enum PowerState
            {
                /// <summary>
                /// the power state is unknown
                /// </summary>
                Unknown = 0,
                /// <summary>
                /// the device is running on its battery
                /// </summary>
                Battery = 1,
                /// <summary>
                /// the device is in the charging cradle
                /// </summary>
                Cradle = 2,
                /// <summary>
                /// the device is charging from AC
                /// </summary>
                AC = 4
            }

            /// <summary>
            /// convert a power state into a string
            /// </summary>
            /// <param name="state">the actual power state</param>
            /// <returns>a string reflecting the power state</returns>
            public static string ConvertPowerStateToString(PowerState state)
            {
                string powerStateString = "Unknown";

                switch (state)
                {
                    case PowerState.Unknown:
                        powerStateString = "Unknown";
                        break;
                    case PowerState.Battery:
                        powerStateString = "On battery";
                        break;
                    case PowerState.Cradle:
                        powerStateString = "On cradle";
                        break;
                    case PowerState.AC:
                        powerStateString = "On AC";
                        break;
                }
                return powerStateString;
            }

            /// <summary>
            /// convert the Battery Level into Percentage
            /// </summary>
            /// <param name="min">minimum value of the battery level range</param>
            /// <param name="max">maximum value of the battery level range</param>
            /// <param name="current">the current value of the battery in that range</param>
            /// <returns>a string with the battery level expressed in Percentage</returns>
            public static string ConvertBatteryLevelInPercentage(int min, int max, int current)
            {
                string percentage = ((float)current / (max - min)) * 100 + "%";
                return percentage;
            }

            /// <summary>
            /// connect this CaptureHelper instance to the 
            /// Socket Mobile Companion software
            /// </summary>
            /// <param name="appId">contains the appId ie: windows:com.mycompany.myapp</param>
            /// <param name="developerId">contains the developer ID used to register the app, ie: 520CE559-D74D-4447-9E65-0E35512A0344</param>
            /// <param name="appKey">contains the application app key returned from the registration ie: MC0CFB+435omgQLdmzUItnt+0nho0niSAhUAhZz/P6RoKyD/2i3Hwty7WuqtJrY=</param>
            /// <returns>ESKT_NOERROR in case of success, ESKT_UNABLEOPEN if
            /// the Socket Mobile Companion service is not responding to
            /// the open, any other error otherwise</returns>

            public Task<long> OpenAsync(
                string appId, string developerId, string appKey)
            {
                _capture = SktClassFactory.createCaptureInstance();
                _capture.DoNotUseWebSocket = DoNotUseWebSocket;
                _capture.CaptureEvent += OnCaptureEvent;
#if DEBUG
                if (DebugConsole != null)
                {
                    DebugConsole.PrintLine("about to open Capture interface");
                }
#endif
                string appInfo = "{\"appId\":\"" + appId + "\", \"developerId\":\"" + developerId + "\", \"appKey\":\"" + appKey + "\"}";
                Task<long> openTask = _capture.OpenAsync(appInfo);
                Task<long> resultTask = openTask.ContinueWith<long>(res => {
#if DEBUG
                    if (DebugConsole != null)
                    {
                        DebugConsole.PrintLine("done opening Capture interface: " + res.Result);
                    }
#endif
                    if (!SktErrors.SKTSUCCESS(res.Result))
                    {
                        _capture.CaptureEvent -= OnCaptureEvent;
                        _capture = null;
                    }
                    return res.Result;
                });
                return resultTask;
            }

            /// <summary>
            /// Close the connection to Socket Mobile Companion
            /// software. It's best to set the Abort property, and
            /// upon receiving the Terminate event then it's
            /// totally safe to Close the connection.
            /// </summary>
            /// <returns>ESKT_NOERROR in case of success, an error
            /// otherwise</returns>
            public Task<long> CloseAsync()
            {
                Task<long> result = Task.Run<long>(() => {

                    if (_capture != null)
                    {
#if DEBUG
                        if (DebugConsole != null)
                        {
                            DebugConsole.PrintLine("about to close Capture interface");
                        }
#endif
                        Task<long> closeTask = _capture.CloseAsync();
                        Task<long> resultTask = closeTask.ContinueWith<long>(res =>
                            {
#if DEBUG
                                if (DebugConsole != null)
                                {
                                    DebugConsole.PrintLine("done closing Capture interface: " + res.Result);
                                }
#endif
                                _capture.CaptureEvent -= OnCaptureEvent;
                                _capture = null;
                                return res.Result;
                            });
                        return resultTask.Result;
                    }
                    return SktErrors.ESKT_NOERROR;
                });
                return result;
            }

            /// <summary>
            /// return a shallow copy of the connected devices list. Since this list is 
            /// initially used by CaptureHelper, adding or removing device will have no
            /// effect.
            /// </summary>
            /// <returns>a list of CaptureHelperDevice</returns>
            public List<CaptureHelperDevice> GetDevicesList()
            {
                return new List<CaptureHelperDevice>(_devices);
            }

            #region Aynchronous Result
            /// <summary>
            /// generic result object containing 
            /// the result of an asynchronous operation
            /// </summary>
            public class AsyncResult
            {
                /// <summary>
                /// contains the operation result
                /// </summary>
                public long Result;
                /// <summary>
                /// helper to check if the result is successful
                /// </summary>
                /// <returns>true if the result is successful, false otherwise</returns>
                public bool IsSuccessful()
                {
                    return SktErrors.SKTSUCCESS(Result);
                }
            }

            /// <summary>
            /// contains the result of getting the Socket
            /// Mobile Companion software version 
            /// </summary>
            public class VersionResult : AsyncResult
            {
                /// <summary>
                /// Major version number
                /// </summary>
                public int Major;
                /// <summary>
                /// Middle version number
                /// </summary>
                public int Middle;
                /// <summary>
                /// Minor version number
                /// </summary>
                public int Minor;
                /// <summary>
                /// Build version number
                /// </summary>
                public int Build;
                /// <summary>
                /// Month of the build
                /// </summary>
                public int Month;
                /// <summary>
                /// Day of the build
                /// </summary>
                public int Day;
                /// <summary>
                /// Year of the build
                /// </summary>
                public int Year;
                /// <summary>
                /// Hour of the build
                /// </summary>
                public int Hour;
                /// <summary>
                /// Minute of the build
                /// </summary>
                public int Minute;
                /// <summary>
                /// retrieve the version as a string ie: "1.2.3.4" or "Error: -15" if an error
                /// occurs
                /// </summary>
                /// <returns>the string version</returns>
                public string ToStringVersion()
                {
                    if (Day != 0)
                    {
                        return Major + "." + Middle + "." + Minor + "." + Build;
                    }
                    else
                    {
                        return "Error: " + Result;
                    }
                }
                /// <summary>
                /// returns the date when Capture has been built. A flag
                /// determines if the time should be included in the string
                /// returned
                /// </summary>
                /// <param name="withTime">specify the time if true, no time otherwise</param>
                /// <returns>the date of the build or "Error: x" in case of error</returns>
                public string ToStringDate(bool withTime = false)
                {
                    if (Year != 0)
                    {
                        DateTime dateTime = new DateTime(Year, Month, Day, Hour, Minute, 0);

                        if (withTime)
                        {
                            return dateTime.ToShortDateString() + " " + dateTime.ToShortTimeString();
                        }
                        else
                        {
                            return dateTime.ToShortDateString();
                        }
                    }
                    else
                    {
                        return "Error: " + Result;
                    }
                }
            }

            /// <summary>
            /// contains a string value for async methods returning a string
            /// </summary>
            public class StringResult : AsyncResult
            {
                /// <summary>
                /// the string value of an async method.
                /// </summary>
                public string Value;
            }

            /// <summary>
            /// result containing the data confirmation mode 
            /// </summary>
            public class DataConfirmationModeResult : AsyncResult
            {
                /// <summary>
                /// Data confirmation mode with these possible values:
                /// ICaptureProperty.Values.ConfirmationMode.kApp the App has to send a confirmation for the decoded data
                /// ICaptureProperty.Values.ConfirmationMode.kCapture the Capture is sending a confirmation for the decoded data
                /// ICaptureProperty.Values.ConfirmationMode.kDevice the Device (local) is sending a confirmation for the decoded data
                /// ICaptureProperty.Values.ConfirmationMode.kOff there is no confirmation required
                /// </summary>
                public byte Mode;
            }

            /// <summary>
            /// contains the result of getting the Data Confirmation Action
            /// </summary>
            public class DataConfirmationActionResult : AsyncResult
            {
                /// <summary>
                /// Beep Action could be one of the following values:
                /// 
                /// ICaptureProperty.Values.DataConfirmation.kBeepGood
                /// ICaptureProperty.Values.DataConfirmation.kBeepBad
                /// ICaptureProperty.Values.DataConfirmation.kBeepNone
                /// 
                /// </summary>
                public int Beep;
                /// <summary>
                /// LED Action could be one of the following values:
                /// 
                /// ICaptureProperty.Values.DataConfirmation.kLedGreen
                /// ICaptureProperty.Values.DataConfirmation.kLedRed
                /// ICaptureProperty.Values.DataConfirmation.kLedNone
                /// </summary>
                public int Led;
                /// <summary>
                /// Rumble Action could be one of the following values:
                /// 
                /// ICaptureProperty.Values.DataConfirmation.kRumbleGood
                /// ICaptureProperty.Values.DataConfirmation.kRumbleBad
                /// ICaptureProperty.Values.DataConfirmation.kRumbleNone
                /// </summary>
                public int Rumble;
            }
            #endregion

            #region Capture Main Object methods
            /// <summary>
            /// Retrieve the Socket Mobile Companion software
            /// version
            /// </summary>
            /// <returns>VersionResult containing the version in case
            /// of success or an error code in the result member of 
            /// the VersionResult object</returns>
            public Task<VersionResult> GetCaptureVersionAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kVersion;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> result = _capture.GetPropertyAsync(property);
                Task<VersionResult> verResult = result.ContinueWith(res =>
                {
                    VersionResult versionResult;

                    if (res.Result.IsSuccessful())
                    {
                        versionResult = new VersionResult()
                        {
                            Result = res.Result.Result,
                            Major = res.Result.Property.Version.dwMajor,
                            Middle = res.Result.Property.Version.dwMiddle,
                            Minor = res.Result.Property.Version.dwMinor,
                            Build = res.Result.Property.Version.dwBuild,
                            Month = res.Result.Property.Version.wMonth,
                            Day = res.Result.Property.Version.wDay,
                            Year = res.Result.Property.Version.wYear,
                            Hour = res.Result.Property.Version.wHour,
                            Minute = res.Result.Property.Version.wMinute
                        };
                    }
                    else
                    {
                        versionResult = new VersionResult() { Result = res.Result.Result };
                    }
                    return versionResult;
                });
                return verResult;
            }

            /// <summary>
            /// abort the usage of Socket Mobile Companion software
            /// which CaptureHelper will fire the Terminate event when
            /// it is perfectly safe to Close CaptureHelper connection from
            /// Socket Mobile Companion software
            /// </summary>
            /// <returns>result of setting the Abort operation</returns>
            public Task<AsyncResult> SetAbortAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kAbort;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> result = _capture.SetPropertyAsync(property);
                Task<AsyncResult> abortResult = result.ContinueWith(res =>
                {
                    return new AsyncResult() { Result = res.Result.Result };
                });
                return abortResult;
            }

            /// <summary>
            /// configure the Data Confirmation mode. The confirmation mode
            /// could be one of these possible values:
            /// ICaptureProperty.Values.ConfirmationMode.kApp the App has to send a confirmation for the decoded data
            /// ICaptureProperty.Values.ConfirmationMode.kCapture the Capture is sending a confirmation for the decoded data
            /// ICaptureProperty.Values.ConfirmationMode.kDevice the Device (local) is sending a confirmation for the decoded data
            /// ICaptureProperty.Values.ConfirmationMode.kOff there is no confirmation required
            /// </summary>
            /// <param name="mode">the mode set to one of the value describe in this method</param>
            /// <returns>result of setting the data confirmation mode</returns>
            public Task<AsyncResult> SetDataConfirmationModeAsync(byte mode)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kDataConfirmationMode;
                property.Type = ICaptureProperty.Types.kByte;
                property.Byte = mode;
                Task<PropertyResult> result = _capture.SetPropertyAsync(property);
                Task<AsyncResult> dataConfirmationModeResult = result.ContinueWith(res =>
                {
                   return new AsyncResult() { Result = res.Result.Result };
                });
                return dataConfirmationModeResult;
            }

            /// <summary>
            /// retrieve the Capture Data Confirmation Mode.
            /// The mode could be one of the following:
            /// ICaptureProperty.Values.ConfirmationMode.kApp the App has to send a confirmation for the decoded data
            /// ICaptureProperty.Values.ConfirmationMode.kCapture the Capture is sending a confirmation for the decoded data
            /// ICaptureProperty.Values.ConfirmationMode.kDevice the Device (local) is sending a confirmation for the decoded data
            /// ICaptureProperty.Values.ConfirmationMode.kOff there is no confirmation required
            /// </summary>
            /// <returns>the Data Confirmation Mode</returns>
            public Task<DataConfirmationModeResult> GetDataConfirmationModeAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kDataConfirmationMode;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> result = _capture.GetPropertyAsync(property);
                Task<DataConfirmationModeResult> dataConfirmationModeResult = result.ContinueWith(res =>
                {
                    return new DataConfirmationModeResult() { Result = res.Result.Result, Mode = res.Result.Property.Byte };
                });
                return dataConfirmationModeResult;
            }

            /// <summary>
            /// configure the Data Confirmation Action that is used when Data Confirmation
            /// Mode is set to Capture. The values for beep, led and rumble could be respectively
            /// one of the following:
            /// 
            /// ICaptureProperty.Values.DataConfirmation.kBeepGood
            /// ICaptureProperty.Values.DataConfirmation.kBeepBad
            /// ICaptureProperty.Values.DataConfirmation.kBeepNone
            /// 
            /// ICaptureProperty.Values.DataConfirmation.kLedGreen
            /// ICaptureProperty.Values.DataConfirmation.kLedRed
            /// ICaptureProperty.Values.DataConfirmation.kLedNone
            /// 
            /// ICaptureProperty.Values.DataConfirmation.kRumbleGood
            /// ICaptureProperty.Values.DataConfirmation.kRumbleBad
            /// ICaptureProperty.Values.DataConfirmation.kRumbleNone
            /// 
            /// </summary>
            /// <param name="beep">defines the action for the beep</param>
            /// <param name="led">defines the action for the LED</param>
            /// <param name="rumble">defines the action for the rumble</param>
            /// <returns>result of setting the confirmation action</returns>
            public Task<AsyncResult> SetDataConfirmationActionAsync(int beep, int led, int rumble)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kDataConfirmationAction;
                property.Type = ICaptureProperty.Types.kUlong;
                property.Ulong = Capture.Helper.DATACONFIRMATION(0, rumble, beep, led);
                Task<PropertyResult> result = _capture.SetPropertyAsync(property);
                Task<AsyncResult> dataConfirmationActionResult = result.ContinueWith(res =>
                {
                    return new AsyncResult() { Result = res.Result.Result };
                });
                return dataConfirmationActionResult;
            }

            /// <summary>
            /// retrieve the Data Confirmation Action that is used only if the
            /// Data Confirmation Mode is set to Capture.
            /// </summary>
            /// <returns>the Data Confirmation Action</returns>
            public Task<DataConfirmationActionResult> GetDataConfirmationActionAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kDataConfirmationAction;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> propertyResult = _capture.GetPropertyAsync(property);
                Task<DataConfirmationActionResult> result =
                    propertyResult.ContinueWith( res => {
                        int beep = 0;
                        int led = 0;
                        int rumble = 0;
                        DataConfirmationActionResult dataConfirmationActionResult;
                        if (res.Result.IsSuccessful())
                        {
                            beep = Capture.Helper.DATACONFIRMATION_BEEP(res.Result.Property.Ulong);
                            led = Capture.Helper.DATACONFIRMATION_LED(res.Result.Property.Ulong);
                            rumble = Capture.Helper.DATACONFIRMATION_RUMBLE(res.Result.Property.Ulong);
                            dataConfirmationActionResult = new DataConfirmationActionResult()
                            {
                                Result = res.Result.Result,
                                Beep = beep,
                                Led = led,
                                Rumble = rumble
                            };
                        }
                        else
                        {
                            dataConfirmationActionResult = new DataConfirmationActionResult()
                            {
                                Result = res.Result.Result,
                                Beep = beep,
                                Led = led,
                                Rumble = rumble
                            };
                        }
                        return dataConfirmationActionResult;
                    });
                return result;
            }

            #endregion

            #region Capture Events
            /// <summary>
            /// Event Arguments for the Error Event
            /// </summary>
            public class ErrorEventArgs : EventArgs
            {
                /// <summary>
                /// contains the Result Error
                /// </summary>
                public long Result;
                /// <summary>
                /// may contain an optional error message
                /// </summary>
                public string Message;
            }

            /// <summary>
            /// Event for reporting Error coming from Capture or
            /// Socket Mobile Companion software.
            /// One of the possible error could be ESKT_NOTHINGTOLISTEN (-47)
            /// which in this case means there is an issue with the configuration
            /// of the Socket Mobile Companion software. The COM Port used for
            /// listening for device incoming connection cannot be opened.
            /// </summary>
            public event EventHandler<ErrorEventArgs> Errors;

            /// <summary>
            /// Arguments containing the Device for the events
            /// DeviceArrival and DeviceRemoval
            /// </summary>
            public class DeviceArgs : EventArgs
            {
                /// <summary>
                /// Contains information about the device. This object
                /// can be used to request or to configure a particular
                /// device.
                /// </summary>
                public CaptureHelperDevice CaptureDevice;
            }

            private EventHandler<DeviceArgs> deviceArrival;
            /// <summary>
            /// DeviceArrival event fires when a device
            /// connects to the host. The argument
            /// contains the CaptureHelperDevice that should
            /// be used in order to control this particular device.
            /// At this point CaptureHelper has already open the device
            /// so the application can receive the decoded data.
            /// Since multiple apps can use Capture, an event confirming
            /// the device ownership can also be monitored.
            /// </summary>
            public event EventHandler<DeviceArgs> DeviceArrival
            {
                add
                {
                    deviceArrival += value;
                    lock (_devices)
                    {
                        if (_devices.Count > 0)
                        {
                            foreach (CaptureHelperDevice device in _devices)
                            {
                                OnDeviceArrival(device);
                            }
                        }
                    }
                }
                remove
                {
                    deviceArrival -= value;
                }
            }

            /// <summary>
            /// DeviceRemoval event fires when a device disconnects
            /// from the host. The CaptureHelperDevice passed in argument
            /// is for reference only. Whatever operation is done with
            /// this CaptureHelperDevice will fail.
            /// </summary>
            public event EventHandler<DeviceArgs> DeviceRemoval;

            /// <summary>
            /// Arguments contains the device ownership information
            /// used for the DeviceOwnershipChange event
            /// </summary>
            public class DeviceOwnershipArgs : DeviceArgs
            {
                /// <summary>
                /// checks if the app has still the device ownership
                /// </summary>
                public bool HasOwnership;
                /// <summary>
                /// GUID identifying the ownership, if the Ownership GUID
                /// is filled with 0s (NULL GUID) it means the application 
                /// has lost the ownership of the device.
                /// </summary>
                public string OwnershipGuid;
            }
            private EventHandler<DeviceOwnershipArgs> deviceOwnershipChange;
            /// <summary>
            /// DeviceOwnershipChange event fires when a particular device
            /// changes ownership. This event indicates when the app gains or
            /// loses that ownership. An app loses the ownership of the device 
            /// when another app is launched and uses the same device. The 
            /// ownership will automatically come back when the other app closes
            /// the device or Capture, or if this app closes and opens the 
            /// device. This last way is not recommended to do it as soon the app
            /// loses the ownership because this app does not have knownledge of
            /// what the other app is doing. It is best to offer an UI notification
            /// to the user indicating the device is no longer owned by the app,
            /// and maybe offering a button to regain the ownership if the user
            /// really need to use the device.
            /// </summary>
            public event EventHandler<DeviceOwnershipArgs> DeviceOwnershipChange
            {
                add
                {
                    deviceOwnershipChange += value;
                    lock (_devices)
                    {
                        if (_devices.Count > 0)
                        {
                            foreach (CaptureHelperDevice device in _devices)
                            {
                                string ownership = ICaptureEvent.OWNERSHIP_LOST;

                                if (device.HasOwnership)
                                {
                                    ownership = device.OwnershipGuid;
                                }
                                OnDeviceOwnership(device, device.HasOwnership, ownership);
                            }
                        }
                    }
                }
                remove
                {
                    deviceOwnershipChange -= value;
                }
            }

            /// <summary>
            /// Arguments for the DecodedData event containing the reference of
            /// the device from which the decoded data comes from and the decoded
            /// data received
            /// </summary>
            public class DecodedDataArgs : DeviceArgs
            {
                /// <summary>
                /// the decoded data with information such as Symbology ID
                /// and Symbology Name
                /// </summary>
                public ICaptureDecodedData DecodedData;
            }

            /// <summary>
            /// DecodedData event fires when decoded data is received.
            /// </summary>
            public event EventHandler<DecodedDataArgs> DecodedData;

            /// <summary>
            /// Arguments for the Power State event containing the
            /// actual power state of a particular device and the 
            /// reference of that device
            /// </summary>
            public class PowerStateArgs : DeviceArgs
            {
                /// <summary>
                /// contains the device new power state
                /// </summary>
                public PowerState State;
            }

            /// <summary>
            /// DevicePowerState event fires when a power state change 
            /// on a device where the device notifications have been set
            /// to trigger on Power State Change. 
            /// </summary>
            public event EventHandler<PowerStateArgs> DevicePowerState;

            /// <summary>
            /// Arguments for the DeviceBatteryLevel event containing
            /// the battery level information and a reference of the
            /// device for which the battery level has changed.
            /// </summary>
            public class BatteryLevelArgs : DeviceArgs
            {
                /// <summary>
                /// the minimal level of the battery range, usually 0
                /// </summary>
                public int MinLevel;
                /// <summary>
                /// the maximal level of the battery range, usually 100
                /// </summary>
                public int MaxLevel;
                /// <summary>
                /// the current battery level in the specified range
                /// </summary>
                public int CurrentLevel;
            }
            /// <summary>
            /// DeviceBatteryLevel fires each time a device has is battery
            /// level change and if that device notifications has been 
            /// configured for triggering this event on battery level change.
            /// </summary>
            public event EventHandler<BatteryLevelArgs> DeviceBatteryLevel;

            /// <summary>
            /// Arguments for the DeviceButtonsState event containing the 
            /// state of all the buttons of the device and a reference of
            /// the actual device from which this event fired.
            /// </summary>
            public class ButtonsStateArgs : DeviceArgs
            {
                /// <summary>
                /// true is the Left Button is Pressed
                /// </summary>
                public bool LeftButtonPressed;
                /// <summary>
                /// true is the Middle Button is Pressed
                /// </summary>
                public bool MiddleButtonPressed;
                /// <summary>
                /// true is the Right Button is Pressed
                /// </summary>
                public bool RightButtonPressed;
                /// <summary>
                /// true is the Power Button is Pressed
                /// </summary>
                public bool PowerButtonPressed;
            }

            /// <summary>
            /// DeviceButtonsState fires each time a button is press on 
            /// a particular device assuming this device has its notifications
            /// configured to trigger such event.
            /// </summary>
            public event EventHandler<ButtonsStateArgs> DeviceButtonsState;

            /// <summary>
            /// Argument for the Listener Started event. This event
            /// does not have any parameter because when it is fired
            /// it means the listener thread has started. If the listener
            /// thread fails for whatever reason, and error ESKT_NOTHINGTOLISTEN
            /// would be reported to the application through the Error event.
            /// </summary>
            public class ListenerStartedArgs : EventArgs
            {
            }

            /// <summary>
            /// Arguments for the Terminate event containing an eventual 
            /// error in its result property. If that is the case then 
            /// the connection with Socket Mobile Companion software might
            /// be lost and the best is to close CaptureHelper and retry.
            /// If this event is received after the app calls SetAbortAsync
            /// then the app can safely close Capture as it won't receive 
            /// any event from Capture.
            /// </summary>
            public class TerminateArgs : EventArgs
            {
                /// <summary>
                /// contains the result of the Terminate event. If there is
                /// an error then the communication between the app and the
                /// Socket Mobile Companion software has been broken
                /// </summary>
                public long Result;
            }
            /// <summary>
            /// Listener Started event fires when the Capture Helper opens
            /// the communication with Socket Mobile Companion software
            /// and the Socket Mobile Companion has its listener thread 
            /// running. This event is also fired when the Socket Mobile
            /// Companion software has its listening ports configuration 
            /// changed confirming the success of the new configuration.
            /// </summary>
            public event EventHandler<ListenerStartedArgs> ListenerStarted;

            /// <summary>
            /// Terminate event fires when the app calls SetAbortAsync or
            /// when the communication with Socket Mobile Companion software
            /// is broken, in which case closing and re-opening CaptureHelper
            /// can restore it.
            /// </summary>
            public event EventHandler<TerminateArgs> Terminate;
            #endregion

            #region internal functions
            // receive all the events from capture and depending on the 
            // context for events, dispatch them to multiple CaptureHelper events 
            internal void OnCaptureEvent(object sender, ICapture.CaptureEventArgs e)
            {
                if (ContextForEvents == null)
                {
                    Task<long> result = DoFireCaptureEventAsync(e);
                }
                else
                {
                    ContextForEvents.Post(new SendOrPostCallback( async (o) =>
                    {
                        await DoFireCaptureEventAsync((ICapture.CaptureEventArgs)o);
                    }), e);
                }
            }

            internal async Task<long> DoFireCaptureEventAsync(ICapture.CaptureEventArgs e)
            {
                long result = SktErrors.ESKT_NOERROR;

                switch (e.captureEvent.EventID)
                {
                    case ICaptureEvent.Id.kDeviceArrival:
                        {
                            ICaptureDevice device = SktClassFactory.createDeviceInstance(_capture);
#if DEBUG
                            if (DebugConsole != null)
                            {
                                DebugConsole.PrintLine("about to open the Capture Device " + e.captureEvent.DeviceInfo.Guid);
                            }
#endif
                            long resultOpen = await device.OpenAsync(e.captureEvent.DeviceInfo.Guid);
#if DEBUG
                            if (DebugConsole != null)
                            {
                                DebugConsole.PrintLine("done opening the Capture Device " + e.captureEvent.DeviceInfo.Guid + " : " + result);
                            }
#endif
                            if (SktErrors.SKTSUCCESS(resultOpen))
                            {
                                // attach the device info to the capture device interface
                                lock (_devices)
                                {
                                    device.DeviceInfo = e.captureEvent.DeviceInfo;
                                    CaptureHelperDevice helperDevice = new CaptureHelperDevice(this, device, true) { HasOwnership = true };
                                    _devices.Add(helperDevice);
                                    OnDeviceArrival(helperDevice);
                                }
                            }
                            else
                            {
                                OnError(resultOpen, "Error while opening the device " + e.captureEvent.DeviceInfo.Name);
                            }
                        }
                        break;
                    case ICaptureEvent.Id.kDeviceRemoval:
                        {
                            CaptureHelperDevice deviceFound = null;

                            lock (_devices)
                            {
                                deviceFound = _devices.Find(device => device.GetDeviceInfo().Guid == e.captureEvent.DeviceInfo.Guid);

                                if (deviceFound != null)
                                {
                                    _devices.Remove(deviceFound);
                                }
                            }
#if DEBUG
                            if (DebugConsole != null)
                            {
                                DebugConsole.PrintLine("about to close the Capture Device " + e.captureEvent.DeviceInfo.Guid);
                            }
#endif
                            if (deviceFound != null)
                            {
                                result = await deviceFound.CloseAsync();
#if DEBUG
                                if (DebugConsole != null)
                                {
                                    DebugConsole.PrintLine("done closing the Capture Device " + e.captureEvent.DeviceInfo.Guid + " : " + result);
                                }
#endif
                                if (SktErrors.SKTSUCCESS(result))
                                {
                                    if (deviceFound != null)
                                    {
                                        OnDeviceRemoval(deviceFound);
                                    }
                                }
                                else
                                {
                                    OnError(result, "Error closing the Capture device " + e.captureEvent.DeviceInfo.Name);
                                }
                            }
                        }
                        break;
                    case ICaptureEvent.Id.kDeviceOwnership:
                        {
                            lock (_devices)
                            {
                                CaptureHelperDevice deviceFound = _devices.Find(device => device.GetDeviceInfo().Guid == e.captureDevice.Guid);

                                if (deviceFound != null)
                                {
                                    bool hasOwnership = !(e.captureEvent.DataString == ICaptureEvent.OWNERSHIP_LOST);
                                    deviceFound.HasOwnership = hasOwnership;
                                    if (hasOwnership)
                                    {
                                        deviceFound.OwnershipGuid = e.captureEvent.DataString;
                                    }
                                    OnDeviceOwnership(deviceFound, hasOwnership, e.captureEvent.DataString);
                                }
                            }
                        }
                        break;
                    case ICaptureEvent.Id.kDecodedData:
                        {
                            lock (_devices)
                            {
                                CaptureHelperDevice deviceFound = _devices.Find(device => device.GetDeviceInfo().Guid == e.captureDevice.Guid);

                                if (deviceFound != null)
                                {
                                    OnDecodedData(deviceFound, e.captureEvent.DataDecodedData);
                                }
                            }
                        }
                        break;
                    case ICaptureEvent.Id.kError:
                        {
                            OnError(e.captureEvent.DataLong, "receive an error from Socket Mobile Companion Service");
                        }
                        break;
                    case ICaptureEvent.Id.kPower:
                        {
                            lock (_devices)
                            {
                                CaptureHelperDevice deviceFound = _devices.Find(device => device.GetDeviceInfo().Guid == e.captureDevice.Guid);
                                PowerState state = (PowerState)Helper.POWER_GETSTATE(e.captureEvent.DataLong);
                                OnDevicePowerState(deviceFound, state);
                            }
                        }
                        break;
                    case ICaptureEvent.Id.kBatteryLevel:
                        {
                            lock (_devices)
                            {
                                CaptureHelperDevice deviceFound = _devices.Find(device => device.GetDeviceInfo().Guid == e.captureDevice.Guid);
                                int min = Helper.BATTERY_GETMINLEVEL(e.captureEvent.DataLong);
                                int max = Helper.BATTERY_GETMAXLEVEL(e.captureEvent.DataLong);
                                int current = Helper.BATTERY_GETCURLEVEL(e.captureEvent.DataLong);
                                OnDeviceBatteryLevel(deviceFound, min, max, current);
                            }
                        }
                        break;
                    case ICaptureEvent.Id.kButtons:
                        {
                            lock (_devices)
                            {
                                CaptureHelperDevice deviceFound = _devices.Find(device => device.GetDeviceInfo().Guid == e.captureDevice.Guid);
                                bool leftButton = Helper.BUTTON_ISLEFTPRESSED(e.captureEvent.DataByte);
                                bool middleButton = Helper.BUTTON_ISMIDDLEPRESSED(e.captureEvent.DataByte);
                                bool rightButton = Helper.BUTTON_ISRIGHTPRESSED(e.captureEvent.DataByte);
                                bool powerButton = Helper.BUTTON_ISPOWERPRESSED(e.captureEvent.DataByte);
                                OnDeviceButtonState(deviceFound, leftButton, middleButton, rightButton, powerButton);
                            }
                        }
                        break;
                    case ICaptureEvent.Id.kListenerStarted:
                        {
                            OnListenerStarted();
                        }
                        break;
                    case ICaptureEvent.Id.kTerminate:
                        {
                            OnTerminate(e.captureEvent.DataLong);
                        }
                        break;
                }
                return result;
            }

            internal void OnError(long result, string message)
            {
                EventHandler<ErrorEventArgs> handler = Errors;

                if (handler != null)
                {
                    handler(this, new ErrorEventArgs() { Result = result, Message = message });
                }
            }

            internal void OnDeviceArrival(CaptureHelperDevice device)
            {
                EventHandler<DeviceArgs> handler = deviceArrival;

                if (handler != null)
                {
                    handler(this, new DeviceArgs() { CaptureDevice = device });
                }
            }

            internal void OnDeviceRemoval(CaptureHelperDevice device)
            {
                EventHandler<DeviceArgs> handler = DeviceRemoval;

                if (handler != null)
                {
                    handler(this, new DeviceArgs() { CaptureDevice = device });
                }
            }

            internal void OnDeviceOwnership(CaptureHelperDevice device, bool hasOwnership, string ownershipGuid)
            {
                EventHandler<DeviceOwnershipArgs> handler = deviceOwnershipChange;

                if (handler != null)
                {
                    handler(this, new DeviceOwnershipArgs()
                    {
                        CaptureDevice = device,
                        HasOwnership = hasOwnership,
                        OwnershipGuid = ownershipGuid
                    });
                }
            }

            internal void OnDecodedData(CaptureHelperDevice device, ICaptureDecodedData data)
            {
                EventHandler<DecodedDataArgs> handler = DecodedData;

                if (handler != null)
                {
                    handler(this, new DecodedDataArgs()
                    {
                        CaptureDevice = device,
                        DecodedData = data
                    });
                }
            }

            internal void OnDevicePowerState(CaptureHelperDevice device, PowerState state)
            {
                EventHandler<PowerStateArgs> handler = DevicePowerState;

                if (handler != null)
                {
                    handler(this, new PowerStateArgs()
                    {
                        CaptureDevice = device,
                        State = state
                    });
                }
            }

            internal void OnDeviceBatteryLevel(CaptureHelperDevice device, int min, int max, int current)
            {
                EventHandler<BatteryLevelArgs> handler = DeviceBatteryLevel;

                if (handler != null)
                {
                    handler(this, new BatteryLevelArgs()
                    {
                        CaptureDevice = device,
                        MinLevel = min,
                        MaxLevel = max,
                        CurrentLevel = current
                    });
                }
            }

            internal void OnDeviceButtonState(CaptureHelperDevice device, bool leftButtonPress, bool middleButtonPress, bool rightButton, bool powerButton)
            {
                EventHandler<ButtonsStateArgs> handler = DeviceButtonsState;

                if (handler != null)
                {
                    handler(this, new ButtonsStateArgs()
                    {
                        CaptureDevice = device,
                        LeftButtonPressed = leftButtonPress,
                        MiddleButtonPressed = middleButtonPress,
                        RightButtonPressed = rightButton,
                        PowerButtonPressed = powerButton
                    });
                }
            }

            internal void OnListenerStarted()
            {
                EventHandler<ListenerStartedArgs> handler = ListenerStarted;

                if (handler != null)
                {
                    handler(this, new ListenerStartedArgs());
                }
            }

            internal void OnTerminate(long result)
            {
                EventHandler<TerminateArgs> handler = Terminate;

                if (handler != null)
                {
                    handler(this, new TerminateArgs() { Result = result });
                }
            }
            #endregion
        }

        #region CaptureHelper Device
        /// <summary>
        /// Object representing a device. This object is
        /// given to the app from an event such as DeviceArrival,
        /// DeviceRemoval, DeviceOwnershipChange, DecodedData
        /// and the various Device notification events.
        /// </summary>
        public partial class CaptureHelperDevice
        {
            private ICaptureDevice CaptureDevice;
            private CaptureHelper Helper;
            private bool opened = false;
            /// <summary>
            /// indicates if the app has the device ownership
            /// </summary>
            public bool HasOwnership = false;// { get; set; }
            /// <summary>
            /// could contains the Battery Level string of the device
            /// </summary>
            public string BatteryLevel = "";
            /// <summary>
            /// could contains the Power state string of the device
            /// </summary>
            public string PowerState = "";

            /// <summary>
            /// contains the ownership GUID assigned to this device,
            /// it does not mean the application has the ownership
            /// for this device. For that checking the hasOwnership flag 
            /// will confirm to the application it has the device ownership. 
            /// </summary>
            public string OwnershipGuid { get; set; }

            internal CaptureHelperDevice(CaptureHelper helper, ICaptureDevice captureDevice, bool open)
            {
                Helper = helper;
                this.CaptureDevice = captureDevice;
                opened = open;
            }

            /// <summary>
            /// retrieves the information about the device
            /// </summary>
            /// <returns>ICaptureDeviceInfo contains the device GUID, name</returns>
            public ICaptureDeviceInfo GetDeviceInfo()
            {
                return CaptureDevice.DeviceInfo;
            }

            /// <summary>
            /// Property containing the Device Type as a string
            /// </summary>
            public string DeviceTypeName
            {
                get
                {
                    string deviceName = "Unknown device";

                    switch (CaptureDevice.DeviceInfo.Type)
                    {
                        case DeviceType.kNone:
                            deviceName = "Unknown device";
                            break;
                        case DeviceType.kScanner7:
                            deviceName = "CHS 7";
                            break;
                        case DeviceType.kScanner7x:
                            deviceName = "CHS 7X";
                            break;
                        case DeviceType.kScanner7xi:
                            deviceName = "CHS 7Xi";
                            break;
                        case DeviceType.kScanner8ci:
                            deviceName = "SocketScan S800";
                            break;
                        case DeviceType.kScanner8qi:
                            deviceName = "SocketScan S850";
                            break;
                        case DeviceType.kScannerS840:
                            deviceName = "SocketScan S840";
                            break;
                        case DeviceType.kScanner9:
                            deviceName = "CRS 9";
                            break;
                        case DeviceType.kScannerD700:
                            deviceName = "DuraScan D700";
                            break;
                        case DeviceType.kScannerD730:
                            deviceName = "DuraScan D730";
                            break;
                        case DeviceType.kScannerD740:
                            deviceName = "DuraScan D740";
                            break;
                        case DeviceType.kScannerD750:
                            deviceName = "DuraScan D750";
                            break;
                        case DeviceType.kScannerD760:
                            deviceName = "DuraScan D750";
                            break;
                        case DeviceType.kScannerS700:
                            deviceName = "SocketScan S700";
                            break;
                        case DeviceType.kScannerS730:
                            deviceName = "SocketScan S730";
                            break;
                        case DeviceType.kScannerS740:
                            deviceName = "SocketScan S740";
                            break;
                        case DeviceType.kScannerS750:
                            deviceName = "SocketScan S750";
                            break;
                        case DeviceType.kScannerS860:
                            deviceName = "SocketScan S860";
                            break;
                        case DeviceType.kScannerD790:
                            deviceName = "DuraScan D790";
                            break;
                        case DeviceType.kSoftScan:
                            deviceName = "Soft Scanner";
                            break;
                        case DeviceType.kBtUnknown:
                            deviceName = "Unknown Bluetooth device";
                            break;
                    }
                    return deviceName;
                }
            }
            /// <summary>
            /// DeviceOwnershipChange fires when this device ownership change.
            /// </summary>
            public event EventHandler<CaptureHelper.DeviceOwnershipArgs> DeviceOwnershipChange
            {
                add
                {
                    Helper.DeviceOwnershipChange += value;
                }
                remove
                {
                    Helper.DeviceOwnershipChange -= value;
                }
            }
            /// <summary>
            /// DeviceRemoval fires when this device disconnects from the host.
            /// </summary>
            public event EventHandler<CaptureHelper.DeviceArgs> DeviceRemoval
            {
                add
                {
                    Helper.DeviceRemoval += value;
                }
                remove
                {
                    Helper.DeviceRemoval -= value;
                }
            }
            /// <summary>
            /// DecodedData fires when this device sent its decoded data.
            /// </summary>
            public event EventHandler<CaptureHelper.DecodedDataArgs> DecodedData
            {
                add
                {
                    Helper.DecodedData += value;
                }
                remove
                {
                    Helper.DecodedData -= value;
                }
            }

            /// <summary>
            /// DevicePowerState fires when this device has a change in its power state
            /// and if this device notifications has been configured to trigger such event.
            /// </summary>
            public event EventHandler<CaptureHelper.PowerStateArgs> DevicePowerState
            {
                add
                {
                    Helper.DevicePowerState += value;
                }
                remove
                {
                    Helper.DevicePowerState -= value;
                }
            }
            /// <summary>
            /// DeviceBatteryLevel fires when this device has a change in its battery level
            /// and if this device notifications has been configured to trigger such event.
            /// </summary>
            public event EventHandler<CaptureHelper.BatteryLevelArgs> DeviceBatteryLevel
            {
                add
                {
                    Helper.DeviceBatteryLevel += value;
                }
                remove
                {
                    Helper.DeviceBatteryLevel -= value;
                }
            }

            /// <summary>
            /// DeviceButtonsState fires when this device has a change in its buttons state
            /// and if this device notifications has been configured to trigger such event.
            /// </summary>
            public event EventHandler<CaptureHelper.ButtonsStateArgs> DeviceButtonsState
            {
                add
                {
                    Helper.DeviceButtonsState += value;
                }
                remove
                {
                    Helper.DeviceButtonsState -= value;
                }
            }

            /// <summary>
            /// Terminate fires when the connection to Socket Mobile Companion software
            /// is broken. 
            /// </summary>
            public event EventHandler<CaptureHelper.ListenerStartedArgs> ListenerStarted
            {
                add
                {
                    Helper.ListenerStarted += value;
                }
                remove
                {
                    Helper.ListenerStarted -= value;
                }
            }

            /// <summary>
            /// Terminate fires when the connection to Socket Mobile Companion software
            /// is broken. 
            /// </summary>
            public event EventHandler<CaptureHelper.TerminateArgs> Terminate
            {
                add
                {
                    Helper.Terminate += value;
                }
                remove
                {
                    Helper.Terminate -= value;
                }
            }

            /// <summary>
            /// By default a CaptureHelperDevice is already opened
            /// by CaptureHelper when it fires the DeviceArrival event.
            /// This OpenAsync method can be used to re-gain the device
            /// ownership.
            /// Opening the device after having closed it allows
            /// to have the device ownership back
            /// </summary>
            /// <returns>SktErrors.ESKT_NOERROR in case of success, an error otherwise</returns>
            public Task<long> OpenAsync()
            {
                Task<long> result = CaptureDevice.OpenAsync(CaptureDevice.Guid);
                opened = true;
                return result;
            }

            /// <summary>
            /// By default a CaptureHelperDevice is closed by
            /// CaptureHelper when it fires the DeviceRemoval event.
            /// Closing temporary the device allows to not receive
            /// anymore any notification coming from this device, and
            /// also to re-gain device ownership after re-open it.
            /// If this method is called when the device is actually not
            /// opened then no error will be reported.
            /// </summary>
            /// <returns>SktErrors.ESKT_NOERROR in case of success, an error otherwise</returns>
            public Task<long> CloseAsync()
            {
                Task<long> result;

                if (opened)
                {
                    opened = false;
                    result = CaptureDevice.CloseAsync();
                }
                else
                {
                    result = Task<long>.Run(() => ((long)SktErrors.ESKT_NOERROR));
                }
                return result;
            }

            /// <summary>
            /// Retrieves the device friendly name
            /// </summary>
            /// <returns>FriendlyName result containing the friendly name or an error in case of failure</returns>
            public Task<FriendlyNameResult> GetFriendlyNameAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kFriendlyNameDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<FriendlyNameResult> result = resultGetProperty.ContinueWith(res =>
                {
                    return new FriendlyNameResult() { Result = res.Result.Result, FriendlyName = res.Result.Property.String };
                });
                return result;
            }
            /// <summary>
            /// results for getting the device friendly name
            /// </summary>
            public class FriendlyNameResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// friendly name of the the device
                /// </summary>
                public string FriendlyName;
            }
            /// <summary>
            /// Change the device friendly name
            /// </summary>
            /// <param name="friendlyName">the new friendly name</param>
            /// <returns>AsyncResult contains the result of changing the friendly name</returns>
            public Task<CaptureHelper.AsyncResult> SetFriendlyNameAsync(string friendlyName)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kFriendlyNameDevice;
                property.Type = ICaptureProperty.Types.kString;
                property.String = friendlyName;
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    return new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                });
                return result;
            }

            /// <summary>
            /// Retrieve the device Bluetooth address. Note that some device might
            /// not support this function, which in this case returns a Result set to
            /// SktErrors.ESKT_NOTSUPPORTED
            /// </summary>
            /// <param name="separator">optional separator of each Bluetooth Address members</param>
            /// <returns>BluetoothAddressResult containing the Bluetooth Address or an error in its result property.</returns>
            public Task<BluetoothAddressResult> GetBluetoothAddressAsync(string separator = ":")
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kBluetoothAddressDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<BluetoothAddressResult> result = resultGetProperty.ContinueWith(res =>
                {
                    string bluetoothAddress = "";

                    if (res.Result.IsSuccessful())
                    {
                        if (res.Result.Property.Type == ICaptureProperty.Types.kArray)
                        {
                            foreach (byte val in res.Result.Property.Array)
                            {
                                if (bluetoothAddress.Length > 0)
                                {
                                    bluetoothAddress += separator;
                                }
                                bluetoothAddress += val.ToString("X2");
                            }
                        }
                        else
                        {
                            res.Result.Result = SktErrors.ESKT_INVALIDOPERATION;
                            bluetoothAddress = "Error: " + res.Result.Result + ", property type unexpected";
                        }
                    }
                    else
                    {
                        bluetoothAddress = "Error: " + res.Result.Result;
                    }
                    return new BluetoothAddressResult() { Result = res.Result.Result, BluetoothAddress = bluetoothAddress };
                });
                return result;
            }
            /// <summary>
            /// contains the device Bluetooth address and the result code
            /// </summary>
            public class BluetoothAddressResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// Bluetooth address of the device, ie: "AA:22:33:44:55:66"
                /// </summary>
                public string BluetoothAddress;
            }
            /// <summary>
            /// retrieve the device power state
            /// </summary>
            /// <returns>PowerState</returns>
            public Task<PowerStateResult> GetPowerStateAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kPowerStateDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<PowerStateResult> result = resultGetProperty.ContinueWith(res =>
               {
                   string powerState = "Unknown";
                   CaptureHelper.PowerState state = (CaptureHelper.PowerState)Capture.Helper.POWER_GETSTATE(res.Result.Property.Ulong);

                   if (res.Result.IsSuccessful())
                   {
                       powerState = CaptureHelper.ConvertPowerStateToString(state);
                   }
                   else
                   {
                       powerState = "Error: " + res.Result.Result;
                   }
                   return new PowerStateResult() { Result = res.Result.Result, PowerState = powerState, State = state };
               });
                return result;
            }
            /// <summary>
            /// contains the power state result and the success code of getting the power state.
            /// </summary>
            public class PowerStateResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// contains the power State (On Battery, On cradle, On AC)
                /// </summary>
                public string PowerState;
                /// <summary>
                /// the actual enum value of the device power state 
                /// </summary>
                public CaptureHelper.PowerState State;
            }
            /// <summary>
            /// retrieve the device battery level.
            /// </summary>
            /// <returns>the battery level</returns>
            public Task<BatteryLevelResult> GetBatteryLevelAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kBatteryLevelDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<BatteryLevelResult> result = resultGetProperty.ContinueWith(res =>
                {
                    int minLevel = 0;
                    int maxLevel = 0;
                    int curLevel = 0;
                    string percentage = "";

                    if (res.Result.IsSuccessful())
                    {
                        minLevel = Capture.Helper.BATTERY_GETMINLEVEL(res.Result.Property.Ulong);
                        maxLevel = Capture.Helper.BATTERY_GETMAXLEVEL(res.Result.Property.Ulong);
                        curLevel = Capture.Helper.BATTERY_GETCURLEVEL(res.Result.Property.Ulong);
                        percentage = CaptureHelper.ConvertBatteryLevelInPercentage(minLevel, maxLevel, curLevel);
                    }
                    else
                    {
                        percentage = "Error: " + res.Result.Result;
                    }
                    return new BatteryLevelResult()
                    {
                        Percentage = percentage,
                        MinimumLevel = minLevel,
                        MaximumLevel = maxLevel,
                        CurrentLevel = curLevel,
                        Result = res.Result.Result
                    };
                });
                return result;
            }
            /// <summary>
            /// contains the battery level range and current level as well as 
            /// the success code of getting the battery level.
            /// </summary>
            public class BatteryLevelResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// battery level expressed as percentage, ie: "58%"
                /// </summary>
                public string Percentage;
                /// <summary>
                /// minimal value of the battery level range, ie: 0
                /// </summary>
                public int MinimumLevel;
                /// <summary>
                /// maximal value of the battery level range, ie: 100
                /// </summary>
                public int MaximumLevel;
                /// <summary>
                /// the actual battery level within the specified range, ie: 58
                /// </summary>
                public int CurrentLevel;
            }

            /// <summary>
            /// retrieve the device decoded data suffix
            /// </summary>
            /// <returns>a string containing the device suffix</returns>
            public Task<CaptureHelper.StringResult> GetSuffixAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kPostambleDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<CaptureHelper.StringResult> result = resultGetProperty.ContinueWith(res =>
                {
                    string value = "";

                    if (res.Result.IsSuccessful())
                    {
                        value = res.Result.Property.String;
                    }
                    else
                    {
                        value = "Error: " + res.Result.Result;
                    }
                    return new CaptureHelper.StringResult() { Value = value, Result = res.Result.Result };
                });
                return result;
            }

            /// <summary>
            /// set the device decoded data suffix
            /// </summary>
            /// <param name="suffix"></param>
            /// <returns>
            /// a result with a success code set to SktErrors.ESKT_NOERROR in case of success or
            /// to an error otherwise
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetSuffixAsync(string suffix)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kPostambleDevice;
                property.Type = ICaptureProperty.Types.kString;
                property.String = suffix;
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    return new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                });
                return result;
            }

            /// <summary>
            /// retrieve the device decoded data prefix.
            /// </summary>
            /// <returns>
            /// the prefix and a success code set to SktErrors.ESKT_NOERRROR in case of success
            /// or to an error code otherwise
            /// </returns>
            public Task<CaptureHelper.StringResult> GetPrefixAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kPreambleDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<CaptureHelper.StringResult> result = resultGetProperty.ContinueWith(res =>
                {
                    string value = "";

                    if (res.Result.IsSuccessful())
                    {
                        value = res.Result.Property.String;
                    }
                    else
                    {
                        value = "Error: " + res.Result.Result;
                    }
                    return new CaptureHelper.StringResult() { Value = value, Result = res.Result.Result };
                });
                return result;
            }

            /// <summary>
            /// set the device decoded data prefix
            /// </summary>
            /// <param name="prefix">new prefix</param>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case of success or to 
            /// an error otherwise
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetPrefixAsync(string prefix)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kPreambleDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    return new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                });
                return result;
            }

            /// <summary>
            /// retrieve the notifications configuration of this device 
            /// </summary>
            /// <returns>
            /// the notifications configuration with a result code set to SktErrors.ESKT_NOERROR in case 
            /// of success or to an error otherwise
            /// </returns>
            public Task<NotificationsResult> GetNotificationsAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kNotificationsDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<NotificationsResult> result = resultGetProperty.ContinueWith(res =>
                {
                    bool batteryLevel = false;
                    bool powerButtonPress = false;
                    bool powerButtonRelease = false;
                    bool powerState = false;
                    bool scanButtonPress = false;
                    bool scanButtonRelease = false;

                    if (res.Result.IsSuccessful())
                    {
                        if ((res.Result.Property.Ulong & ICaptureProperty.Values.Notifications.kBatteryLevelChange)
                            == ICaptureProperty.Values.Notifications.kBatteryLevelChange)
                        {
                            batteryLevel = true;
                        }
                        if ((res.Result.Property.Ulong & ICaptureProperty.Values.Notifications.kPowerButtonPress)
                            == ICaptureProperty.Values.Notifications.kPowerButtonPress)
                        {
                            powerButtonPress = true;
                        }
                        if ((res.Result.Property.Ulong & ICaptureProperty.Values.Notifications.kPowerButtonRelease)
                            == ICaptureProperty.Values.Notifications.kPowerButtonRelease)
                        {
                            powerButtonRelease = true;
                        }
                        if ((res.Result.Property.Ulong & ICaptureProperty.Values.Notifications.kPowerState)
                            == ICaptureProperty.Values.Notifications.kPowerState)
                        {
                            powerState = true;
                        }
                        if ((res.Result.Property.Ulong & ICaptureProperty.Values.Notifications.kScanButtonPress)
                            == ICaptureProperty.Values.Notifications.kScanButtonPress)
                        {
                            scanButtonPress = true;
                        }
                        if ((res.Result.Property.Ulong & ICaptureProperty.Values.Notifications.kScanButtonRelease)
                            == ICaptureProperty.Values.Notifications.kScanButtonRelease)
                        {
                            scanButtonRelease = true;
                        }
                    }
                    return new NotificationsResult()
                    {
                        Result = res.Result.Result,
                        Notifications = new Notifications()
                        {
                            PowerState = powerState,
                            BatteryLevel = batteryLevel,
                            ScanButtonPress = scanButtonPress,
                            ScanButtonRelease = scanButtonRelease,
                            PowerButtonPress = powerButtonPress,
                            PowerButtonRelease = powerButtonRelease
                        }
                    };
                });
                return result;
            }
            /// <summary>
            /// contains the device notifications configuration
            /// </summary>
            public class Notifications
            {
                /// <summary>
                /// Notify when there is a change in the power state
                /// </summary>
                public bool PowerState;
                /// <summary>
                /// Notify when there is a change in the device battery level
                /// </summary>
                public bool BatteryLevel;
                /// <summary>
                /// Notify if the Scan button is pressed
                /// </summary>
                public bool ScanButtonPress;
                /// <summary>
                /// Notify if the Scan button is release
                /// </summary>
                public bool ScanButtonRelease;
                /// <summary>
                /// Notify if the Power button is pressed
                /// </summary>
                public bool PowerButtonPress;
                /// <summary>
                /// Notify if the Power button is released
                /// </summary>
                public bool PowerButtonRelease;
            }

            /// <summary>
            /// result of getting the device notifications configuration.
            /// The result code should be checked before assuming the properties
            /// are valid.
            /// </summary>
            public class NotificationsResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// Contains the actual notification configuration
                /// </summary>
                public Notifications Notifications;
            }

            /// <summary>
            /// configure the device notifications
            /// </summary>
            /// <param name="notifications">device notifications configuration</param>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case of success of to an error otherwise
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetNotificationsAsync(Notifications notifications)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kNotificationsDevice;
                property.Type = ICaptureProperty.Types.kUlong;
                property.Ulong = 0;
                if (notifications.BatteryLevel)
                {
                    property.Ulong += ICaptureProperty.Values.Notifications.kBatteryLevelChange;
                }
                if (notifications.PowerButtonPress)
                {
                    property.Ulong += ICaptureProperty.Values.Notifications.kPowerButtonPress;
                }
                if (notifications.PowerButtonRelease)
                {
                    property.Ulong += ICaptureProperty.Values.Notifications.kPowerButtonRelease;
                }
                if (notifications.PowerState)
                {
                    property.Ulong += ICaptureProperty.Values.Notifications.kPowerState;
                }
                if (notifications.ScanButtonPress)
                {
                    property.Ulong += ICaptureProperty.Values.Notifications.kScanButtonPress;
                }
                if (notifications.ScanButtonRelease)
                {
                    property.Ulong += ICaptureProperty.Values.Notifications.kScanButtonRelease;
                }
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    return new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                });
                return result;
            }

            /// <summary>
            /// retrieve the device firmware version
            /// </summary>
            /// <returns>
            /// the firmware version and a result code set to SktErrors.ESKT_NOERROR in case
            /// of success or to an error otherwise.
            /// </returns>
            public Task<CaptureHelper.VersionResult> GetFirmwareVersionAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kVersionDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<CaptureHelper.VersionResult> result = resultGetProperty.ContinueWith(res =>
                {
                    CaptureHelper.VersionResult versionResult;

                    if (res.Result.IsSuccessful())
                    {
                        versionResult = new CaptureHelper.VersionResult()
                        {
                            Result = res.Result.Result,
                            Major = res.Result.Property.Version.dwMajor,
                            Middle = res.Result.Property.Version.dwMiddle,
                            Minor = res.Result.Property.Version.dwMinor,
                            Build = res.Result.Property.Version.dwBuild,
                            Month = res.Result.Property.Version.wMonth,
                            Day = res.Result.Property.Version.wDay,
                            Year = res.Result.Property.Version.wYear,
                            Hour = res.Result.Property.Version.wHour,
                            Minute = res.Result.Property.Version.wMinute
                        };
                    }
                    else
                    {
                        versionResult = new CaptureHelper.VersionResult() { Result = res.Result.Result };
                    }
                    return versionResult;
                });
                return result;
            }

            /// <summary>
            /// confirm the decoded data received with a confirmation feedback.
            /// The confirmation feedback can be a bitwise values defines as follow:
            /// For Beep confirmation
            /// ICaptureProperty.Values.DataConfirmation.kBeepBad
            /// ICaptureProperty.Values.DataConfirmation.kBeepGood
            /// ICaptureProperty.Values.DataConfirmation.kBeepNone
            /// 
            /// For LED confirmation
            /// ICaptureProperty.Values.DataConfirmation.kLedGreen
            /// ICaptureProperty.Values.DataConfirmation.kLedNone
            /// ICaptureProperty.Values.DataConfirmation.kLedRed
            /// 
            /// For Rumble confirmation
            /// ICaptureProperty.Values.DataConfirmation.kRumbleBad
            /// ICaptureProperty.Values.DataConfirmation.kRumbleGood
            /// ICaptureProperty.Values.DataConfirmation.kRumbleNone
            /// </summary>
            /// <param name="beep"> the value for the beep</param>
            /// <param name="led"> the value for the LED</param>
            /// <param name="rumble"> the value to rumble the scanner</param>
            /// <returns></returns>
            public Task<CaptureHelper.AsyncResult> SetDataConfirmationAsync(int beep, int led, int rumble)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kDataConfirmationDevice;
                property.Type = ICaptureProperty.Types.kUlong;
                property.Ulong = Capture.Helper.DATACONFIRMATION(0, rumble, beep, led);
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    CaptureHelper.AsyncResult confirmationResult =
                        new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                    return confirmationResult;
                });
                return result;
            }

            /// <summary>
            /// configure the device when used with a stand.
            /// The stand configuration can be one of these values:
            /// ICaptureProperty.Values.StandConfig.kAutoMode
            /// ICaptureProperty.Values.StandConfig.kDetectMode
            /// ICaptureProperty.Values.StandConfig.kMobileMode
            /// ICaptureProperty.Values.StandConfig.kStandMode
            /// </summary>
            /// <param name="standConfiguration"></param>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case of success or to
            /// an error otherwise
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetStandConfigurationAsync(int standConfiguration)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kStandConfigDevice;
                property.Type = ICaptureProperty.Types.kUlong;
                property.Ulong = standConfiguration;
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    CaptureHelper.AsyncResult standConfigurationResult = new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                    return standConfigurationResult;
                });
                return result;
            }
            /// <summary>
            /// contains the current stand configuration of the device
            /// </summary>
            public class StandConfigurationResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// contains the stand configuration of the device. The stand configuration
                /// defines the device behavior when it rests on the stand or when it is 
                /// taking out of the stand.
                /// It can be one of the following values:
                /// ICaptureProperty.Values.StandConfig.kAutoMode
                /// ICaptureProperty.Values.StandConfig.kDetectMode
                /// ICaptureProperty.Values.StandConfig.kMobileMode
                /// ICaptureProperty.Values.StandConfig.kStandMode
                /// </summary>
                public int StandConfiguration;
            }
            /// <summary>
            /// retrieve the stand configuration of the device
            /// </summary>
            /// <returns>
            /// the stand configuration and a result code set to SktErrors.ESKT_NOERROR or
            /// to an error otherwise
            /// </returns>
            public Task<StandConfigurationResult> GetStandConfigurationAsync()
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kStandConfigDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<StandConfigurationResult> result = resultGetProperty.ContinueWith(res =>
                {
                    StandConfigurationResult standConfigurationResult =
                        new StandConfigurationResult() { Result = res.Result.Result, StandConfiguration = res.Result.Property.Ulong };
                    return standConfigurationResult;
                });
                return result;
            }

            /// <summary>
            /// configure the device decode action that occurs when a barcode is read
            /// </summary>
            /// <param name="beep">true to beep when decode occurs</param>
            /// <param name="flash">true to flash LED when decode occurs</param>
            /// <param name="rumble">true to rumble when decode occurs</param>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case of success or
            /// to an error code otherwise
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetDecodeActionAsync(bool beep, bool flash, bool rumble)
            {
                byte decodeAction = ICaptureProperty.Values.LocalDecodeAction.kNone;

                if (beep)
                {
                    decodeAction |= ICaptureProperty.Values.LocalDecodeAction.kBeep;
                }
                if (flash)
                {
                    decodeAction |= ICaptureProperty.Values.LocalDecodeAction.kFlash;
                }
                if (rumble)
                {
                    decodeAction |= ICaptureProperty.Values.LocalDecodeAction.kRumble;
                }

                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kLocalDecodeActionDevice;
                property.Type = ICaptureProperty.Types.kByte;
                property.Byte = decodeAction;
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    CaptureHelper.AsyncResult standConfigurationResult = new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                    return standConfigurationResult;
                });
                return result;
            }
            /// <summary>
            /// contains a bitwise value for the device decode action
            /// </summary>
            public class DecodeActionResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// Beep is true if the scanner beeps on decode action
                /// </summary>
                public bool Beep;
                /// <summary>
                /// Flash is true if the scanner flashes the LED on decode action
                /// </summary>
                public bool Flash;
                /// <summary>
                /// Rumble is true if the scanner vibrates on decode action
                /// </summary>
                public bool Rumble;
            }

            /// <summary>
            /// retrieve the device decode action configuration
            /// </summary>
            /// <returns>
            /// the device decode action and a result code set
            /// to SktErrors.ESKT_NOERROR or to an error code otherwise
            /// </returns>
            public Task<DecodeActionResult> GetDecodeActionAsync()
            {
                bool beep = false, flash = false, rumble = false;
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kLocalDecodeActionDevice;
                property.Type = ICaptureProperty.Types.kNone;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<DecodeActionResult> result = resultGetProperty.ContinueWith(res =>
                {

                    if ((res.Result.Property.Byte & ICaptureProperty.Values.LocalDecodeAction.kBeep)
                        == ICaptureProperty.Values.LocalDecodeAction.kBeep)
                    {
                        beep = true;
                    }
                    if ((res.Result.Property.Byte & ICaptureProperty.Values.LocalDecodeAction.kFlash)
                        == ICaptureProperty.Values.LocalDecodeAction.kFlash)
                    {
                        flash = true;
                    }
                    if ((res.Result.Property.Byte & ICaptureProperty.Values.LocalDecodeAction.kRumble)
                        == ICaptureProperty.Values.LocalDecodeAction.kRumble)
                    {
                        rumble = true;
                    }

                    DecodeActionResult decodeActionResult =
                        new DecodeActionResult() { Result = res.Result.Result, Beep = beep, Flash = flash, Rumble = rumble };
                    return decodeActionResult;
                });
                return result;
            }

            /// <summary>
            /// contains the symbology information result
            /// </summary>
            public class SymbologyResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// contains the actual state of the symbology
                /// </summary>
                public enum eStatus
                {
                    /// <summary>
                    /// the symbology is enabled
                    /// </summary>
                    enable,
                    /// <summary>
                    /// the symbology is disabled
                    /// </summary>
                    disable,
                    /// <summary>
                    /// the symbology is not supported on this device
                    /// </summary>
                    notSupported
                };
                /// <summary>
                /// defines the last Symbology ID. This can be useful when requesting the
                /// state of all the symbologies of a device starting with ID = 1 to ID = LastSymbologyID.
                /// NOTE: Symbology ID 0 means unknown Symbology.
                /// </summary>
                public const int LastSymbologyID = ICaptureSymbology.Id.kLastSymbologyID;
                /// <summary>
                /// contains the actual Symbology ID.
                /// </summary>
                public int ID;
                /// <summary>
                /// contains the symbology status.
                /// </summary>
                public eStatus Status;
                /// <summary>
                /// contains the symbology name.
                /// </summary>
                public string Name;
            }
            /// <summary>
            /// retrieve the information about a symbology identified by its ID.
            /// the symbology IDs go from 1 to ICaptureSymbology.Id.kLastSymbologyID (excluded).
            /// The ID of a specific symbology can be discovered by typing ICaptureSymbology.Id. 
            /// </summary>
            /// <param name="SymbologyId"></param>
            /// <returns>
            /// the symbology information and a result code set to SktErrors.ESKT_NOERROR in 
            /// case of success or to an error code otherwise.
            /// </returns>
            public Task<SymbologyResult> GetSymbologyAsync(int SymbologyId)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kSymbologyDevice;
                property.Type = ICaptureProperty.Types.kSymbology;
                property.Symbology.ID = SymbologyId;
                property.Symbology.Flags = ICaptureSymbology.FlagsMask.kStatus;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<SymbologyResult> result = resultGetProperty.ContinueWith(res =>
                {
                    SymbologyResult symbologyResult;

                    if (res.Result.IsSuccessful())
                    {
                        SymbologyResult.eStatus symbologyStatus;

                        switch (res.Result.Property.Symbology.Status)
                        {
                            case ICaptureSymbology.StatusValues.kEnable:
                                symbologyStatus = SymbologyResult.eStatus.enable;
                                break;
                            case ICaptureSymbology.StatusValues.kDisable:
                                symbologyStatus = SymbologyResult.eStatus.disable;
                                break;
                            case ICaptureSymbology.StatusValues.kNotSupported:
                            default:
                                symbologyStatus = SymbologyResult.eStatus.notSupported;
                                break;
                        }
                        symbologyResult = new SymbologyResult()
                        {
                            Result = res.Result.Result,
                            ID = res.Result.Property.Symbology.ID,
                            Status = symbologyStatus,
                            Name = res.Result.Property.Symbology.Name
                        };
                    }
                    else
                    {
                        symbologyResult = new SymbologyResult() { Result = res.Result.Result };
                    }
                    return symbologyResult;
                });
                return result;
            }

            /// <summary>
            /// enable or disable a specific symbology.
            /// The symbology ID can be find out by typing: ICaptureSymbology.Id.
            /// </summary>
            /// <param name="SymbologyId">the symbology to enable or disable</param>
            /// <param name="enable">true to enable the symbology, false otherwise</param>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case of success or
            /// to an error code otherwise
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetSymbologyAsync(int SymbologyId, bool enable)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kSymbologyDevice;
                property.Type = ICaptureProperty.Types.kSymbology;
                property.Symbology.ID = SymbologyId;
                property.Symbology.Flags = ICaptureSymbology.FlagsMask.kStatus;
                if (enable)
                {
                    property.Symbology.Status = ICaptureSymbology.StatusValues.kEnable;
                }
                else
                {
                    property.Symbology.Status = ICaptureSymbology.StatusValues.kDisable;
                }
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    CaptureHelper.AsyncResult resultSymbology = new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                    return resultSymbology;
                });
                return result;
            }

            /// <summary>
            /// Start the device trigger operation. The device stays in 
            /// trigger mode for about 3s before giving up. If a barcode
            /// is scanned the device returns immediately to rest mode
            /// until the trigger button is pressed or this method is called
            /// again
            /// </summary>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case
            /// of success or to an error otherwise.
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetTriggerStartAsync()
            {
                return SetTriggerAsync(ICaptureProperty.Values.Trigger.kStart);
            }

            /// <summary>
            /// Stop the device trigger operation. This is effective only
            /// with the 3 second the device is actually triggering.
            /// </summary>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case
            /// of success or to an error otherwise.
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetTriggerStopAsync()
            {
                return SetTriggerAsync(ICaptureProperty.Values.Trigger.kStop);
            }

            /// <summary>
            /// Enable the trigger button. This is effective only
            /// when the trigger button has been disabled.
            /// </summary>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case
            /// of success or to an error otherwise.
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetTriggerEnableAsync()
            {
                return SetTriggerAsync(ICaptureProperty.Values.Trigger.kEnable);
            }

            /// <summary>
            /// Disable the trigger button. The trigger button stays disabled
            /// until it is enabled again or until the device is restarted.
            /// </summary>
            /// <returns>
            /// a result code set to SktErrors.ESKT_NOERROR in case
            /// of success or to an error otherwise.
            /// </returns>
            public Task<CaptureHelper.AsyncResult> SetTriggerDisableAsync()
            {
                return SetTriggerAsync(ICaptureProperty.Values.Trigger.kDisable);
            }

            internal Task<CaptureHelper.AsyncResult> SetTriggerAsync(byte trigger)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kTriggerDevice;
                property.Type = ICaptureProperty.Types.kByte;
                property.Byte = trigger;
                Task<PropertyResult> resultSetProperty = CaptureDevice.SetPropertyAsync(property);
                Task<CaptureHelper.AsyncResult> result = resultSetProperty.ContinueWith(res =>
                {
                    CaptureHelper.AsyncResult resultTrigger = new CaptureHelper.AsyncResult() { Result = res.Result.Result };
                    return resultTrigger;
                });
                return result;
            }
            /// <summary>
            /// result contains the response to the GetDeviceSpecificAsync method
            /// </summary>
            public class DeviceSpecificResult : CaptureHelper.AsyncResult
            {
                /// <summary>
                /// contains the specific data as reply of the command.
                /// </summary>
                public byte[] DeviceSpecificData;
            }

            /// <summary>
            /// sends and retrieves a device specific firmware command.
            /// </summary>
            /// <param name="specificCommand"> contains the command in an array of bytes</param>
            /// <returns>
            /// the command response as byte array and a result code set to 
            /// SktErrors.ESKT_NOERROR in case of success or to an error code otherwise
            /// </returns>
            public Task<DeviceSpecificResult> GetDeviceSpecificAsync(byte[] specificCommand)
            {
                ICaptureProperty property = SktClassFactory.createCaptureProperty();
                property.ID = ICaptureProperty.PropId.kDeviceSpecific;
                property.Type = ICaptureProperty.Types.kArray;
                property.Array = specificCommand;
                Task<PropertyResult> resultGetProperty = CaptureDevice.GetPropertyAsync(property);
                Task<DeviceSpecificResult> result = resultGetProperty.ContinueWith(res =>
                {
                    DeviceSpecificResult deviceSpecificResult =
                        new DeviceSpecificResult() { Result = res.Result.Result, DeviceSpecificData = res.Result.Property.Array };
                    return deviceSpecificResult;
                });
                return result;
            }
        }
        #endregion
    }
}