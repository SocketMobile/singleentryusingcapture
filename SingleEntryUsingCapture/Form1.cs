///  SingleEntry Using Capture
///
/// Sample app that displays the scanner name
/// and the decoded data into a list box
/// 
/// Follow the steps from 1 to 4
/// ©2016-2020 Socket Mobile, Inc.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

//1- add the SocketMobile Capture namespace
using SocketMobile.Capture;

namespace SingleEntryUsingCapture
{
    public partial class Form1 : Form
    {
        //2- Create a CaptureHelper member
        CaptureHelper mCapture;
        public Form1()
        {
            InitializeComponent();
            // 3- instantiate and configure CaptureHelper
            mCapture = new CaptureHelper {ContextForEvents = WindowsFormsSynchronizationContext.Current};
            mCapture.DeviceArrival += mCapture_DeviceArrival;
            mCapture.DeviceRemoval += mCapture_DeviceRemoval;
            mCapture.DecodedData += mCapture_DecodedData;
            
            // this is to handle the case of the Socket Mobile Companion 
            // being stopped for some reason
            mCapture.Terminate+= mCapture_Terminate;

            // 4- Start opening the connection to Socket Mobile Companion
            // which must be running, here it is done with a timer
            // because we restart this timer in case the Socket Mobile Companion
            // has been stopped
            timerOpenCapture.Tick += timerOpenCapture_Tick;
            timerOpenCapture.Start();
        }

        private async void timerOpenCapture_Tick(object sender, EventArgs e)
        {
            timerOpenCapture.Stop();

            long Result = await mCapture.OpenAsync("windows:com.socketmobile.singleentry",
                "BB57D8E1-F911-47BA-B510-693BE162686A",
                "MCwCFAJIzwgOK1fYEE5KdmPOe+Lm+5x6AhR6kYJKPLvEsh8TO7jaivECRe5C9A==");
            if (SktErrors.SKTSUCCESS(Result))
            {
                // ask for the version
                CaptureHelper.VersionResult version = await mCapture.GetCaptureVersionAsync();
                if (version.IsSuccessful())
                {
                    labelVersion.Text = $"Capture version: {version.ToStringVersion()}";

                        ;
                }
            }
            else
            {
                labelVersion.Text = "Unable to connect to Socket Mobile Companion";
                DialogResult dialogResult = MessageBox.Show(
                    "Unable to open Capture, is Socket Mobile Companion Service running?",
                    "Error",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Warning);
                if (dialogResult == DialogResult.Retry)
                {
                    timerOpenCapture.Start();
                }
                else
                {
                    Close();
                }
            }
        }

        #region Capture Helper Event Handlers
        // received when something is wrong with Socket Mobile Companion
        // or when aborting Capture Helper (result is no error in that case)
        private void mCapture_Terminate(object sender, CaptureHelper.TerminateArgs e)
        {
            // if there is an error then Socket Mobile Companion
            // is not responding anymore
            if (!SktErrors.SKTSUCCESS(e.Result))
            {
                timerOpenCapture.Start();
            }
        }

        // received when a barcode has been decoded correctly
        void mCapture_DecodedData(object sender, CaptureHelper.DecodedDataArgs e)
        {
            string infoAndDecodedData = $"{e.DecodedData.SymbologyName}: {e.DecodedData.DataToUTF8String}";
            listBoxDecodedData.Items.Add(infoAndDecodedData);
        }

        // received when a scanner disconnects
        void mCapture_DeviceRemoval(object sender, CaptureHelper.DeviceArgs e)
        {
            UpdateStatus();
        }

        // received when a scanner connects
        void mCapture_DeviceArrival(object sender, CaptureHelper.DeviceArgs e)
        {
            UpdateStatus();
        }

        #endregion

        #region UI Handlers
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ProcessStartInfo sInfo = new ProcessStartInfo("https://www.socketmobile.com");
            Process.Start(sInfo);
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            listBoxDecodedData.Items.Clear();
        }
        #endregion

        #region Utility methods
        private void UpdateStatus()
        {
            List<CaptureHelperDevice> devices = mCapture.GetDevicesList();
            if (!devices.Any())
            {
                labelStatus.Text = "no scanner connected";
            }
            else
            {
                labelStatus.Text = string.Empty;
                foreach (CaptureHelperDevice device in devices)
                {
                    if (!string.IsNullOrEmpty((labelStatus.Text)))
                    {
                        labelStatus.Text += "\n";
                    }
                    labelStatus.Text += device.GetDeviceInfo().Name;
                }
            }
        }
        #endregion
    }
}
