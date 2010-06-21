﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using WiimoteLib;

namespace WiiTUIO.Provider
{
    /// <summary>
    /// The WiiProvider implements <see cref="IProvider"/> in order to offer a type of object which uses the Wiimote to generate new event frames.
    /// </summary>
    public class WiiProvider
    {
        #region CalibrationRectangle
        /// <summary>
        /// The CalibrationRectangle class defines a set of 4 2D coordinates that define a rectangle in absolute space.
        /// These are used as inputs that define transform the WiiProvider applies to any inputs from the Wiimote device.
        /// </summary>
        [Serializable]
        public class CalibrationRectangle
        {
            /// <summary>The top left corner of a rectangle in absolute coordinates.</summary>
            public Vector TopLeft;
            /// <summary>The top right corner of a rectangle in absolute coordinates.</summary>
            public Vector TopRight;
            /// <summary>The bottom left corner of a rectangle in absolute coordinates.</summary>
            public Vector BottomLeft;
            /// <summary>The bottom right corner of a rectangle in absolute coordinates.</summary>
            public Vector BottomRight;

            /// <summary>
            /// Construct a new CalibrationRectangle with dimension information.
            /// </summary>
            /// <param name="vTopLeft">The top left corner of a rectangle in absolute coordinates.</param>
            /// <param name="vTopRight">The top right corner of a rectangle in absolute coordinates.</param>
            /// <param name="vBottomLeft">The bottom left corner of a rectangle in absolute coordinates.</param>
            /// <param name="vBottomRight">The bottom right corner of a rectangle in absolute coordinates.</param>
            public CalibrationRectangle(Vector vTopLeft, Vector vTopRight, Vector vBottomLeft, Vector vBottomRight)
            {
                this.TopLeft = vTopLeft;
                this.TopRight = vTopRight;
                this.BottomLeft = vBottomLeft;
                this.BottomRight = vBottomRight;
            }

            /// <summary>
            /// Construct a new CalibrationRectangle with dimension information.
            /// </summary>
            /// <param name="x0">The x-coordinate of the top left corner of a rectangle in absolute coordinates.</param>
            /// <param name="y0">The y-coordinate of the top left corner of a rectangle in absolute coordinates.</param>
            /// <param name="x1">The x-coordinate of the top right corner of a rectangle in absolute coordinates.</param>
            /// <param name="y1">The y-coordinate of the top right corner of a rectangle in absolute coordinates.</param>
            /// <param name="x2">The x-coordinate of the bottom left corner of a rectangle in absolute coordinates.</param>
            /// <param name="y2">The y-coordinate of the bottom left corner of a rectangle in absolute coordinates.</param>
            /// <param name="x3">The x-coordinate of the bottom right corner of a rectangle in absolute coordinates.</param>
            /// <param name="y3">The y-coordinate of the bottom right corner of a rectangle in absolute coordinates.</param>
            public CalibrationRectangle(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
            {
                this.TopLeft = new Vector(x0, y0);
                this.TopRight = new Vector(x1, y1);
                this.BottomLeft = new Vector(x2, y2);
                this.BottomRight = new Vector(x3, y3);
            }

            /// <summary>
            /// Construct a new CalibrationRectangle with default 0-1 coordinates.
            /// </summary>
            public CalibrationRectangle()
                : this(0.0, 0.0, 1.0, 0.0, 0.0, 1.0, 1.0, 1.0)
            {
            }
        }
        #endregion

        #region Properties and Constructor
        /// <summary>
        /// Boolean which indicates if we are generating input or not.
        /// </summary>
        private bool bRunning = false;

        /// <summary>
        /// A queue for the frame of events.
        /// </summary>
        private Queue<WiiContact> lFrame = new Queue<WiiContact>();

        /// <summary>
        /// A refrence to our wiimote device.
        /// </summary>
        private Wiimote pDevice = null;

        /// <summary>
        /// Used to obtain mutual exlusion over Wiimote updates.
        /// </summary>
        private Mutex pDeviceMutex = new Mutex();

        /// <summary>
        /// An input classifier which we will use to organise points.
        /// </summary>
        public SpatioTemporalClassifier InputClassifier { get; protected set; }

        /// <summary>
        /// The screen size that we use for normalising coordinates.
        /// </summary>
        public Vector ScreenSize { get; protected set; }

        /// <summary>
        /// A reference to a warper which we use to transform points from the device.
        /// </summary>
        private Warper pWarper;

        /// <summary>
        /// A property to determine if this input provider is running (and thus generating events).
        /// </summary>
        public bool IsRunning { get { return this.bRunning; } }

        /// <summary>
        /// Do we want to use the calibration transformation step when generating input.
        /// </summary>
        public bool TransformResults { get; set; }

        /// <summary>
        /// This defines an event which is raised when a new frame of touch events is prepared and ready to be dispatched by this provider.
        /// </summary>
        public event EventHandler<FrameEventArgs> OnNewFrame;

        #region Battery State
        /// <summary>
        /// An event which is fired when the battery state changes.
        /// </summary>
        public event Action<int> OnBatteryUpdate;

        /// <summary>
        /// The internal battery state.
        /// </summary>
        private int iBatteryState = 0;

        /// <summary>
        /// Get the current battery state.
        /// </summary>
        public int BatteryState
        {
            get
            {
                return iBatteryState;
            }
            protected set
            {
                if (value != iBatteryState)
                {
                    iBatteryState = value;
                    if (OnBatteryUpdate != null)
                        OnBatteryUpdate(iBatteryState);
                }
            }
        }
        #endregion

        /// <summary>
        /// Construct a new wiimote provider.
        /// </summary>
        public WiiProvider()
        {
            // Create a new classifer.
            this.InputClassifier = new SpatioTemporalClassifier();
            this.InputClassifier.DefaultSmoothSize = 4;

            // Bind events.
            this.InputClassifier.OnStart += new SpatioTemporalClassifier.TrackerEventHandler(handleInputClassifier_OnStart);
            this.InputClassifier.OnUpdate += new SpatioTemporalClassifier.TrackerEventHandler(handleInputClassifier_OnUpdate);
            this.InputClassifier.OnEnd += new SpatioTemporalClassifier.TrackerEventHandler(handleInputClassifier_OnEnd);

            // Set default source and destination rectangles.
            this.pWarper = new Warper();
            this.setCalibrationData(new CalibrationRectangle(), new CalibrationRectangle(), new Vector(1.0, 1.0));

            // Enable the calibration.
            this.TransformResults = true;
        }
        #endregion

        #region SpatioTemporalClassifier Event Handling
        /// <summary>
        /// Enqueue a 'ContactType.End' event.
        /// </summary>
        /// <param name="pSource">The SpatioTemporalClassifier which was the source.</param>
        /// <param name="pTracker">The reference to the SpatioTemporalTracker responsible for tracking this input.</param>
        private void handleInputClassifier_OnEnd(SpatioTemporalClassifier pSource, SpatioTemporalTracker pTracker)
        {
            // Enqueue a contact removed event.
            lFrame.Enqueue(new WiiContact(pTracker.ID, ContactType.End, new System.Windows.Point(pTracker.Position.X, pTracker.Position.Y), ScreenSize, pSource, pTracker));
        }

        /// <summary>
        /// Enqueue a 'ContactType.Move' event.
        /// </summary>
        /// <param name="pSource">The SpatioTemporalClassifier which was the source.</param>
        /// <param name="pTracker">The reference to the SpatioTemporalTracker responsible for tracking this input.</param>
        private void handleInputClassifier_OnUpdate(SpatioTemporalClassifier pSource, SpatioTemporalTracker pTracker)
        {
            // Enqueue a contact removed event.
            lFrame.Enqueue(new WiiContact(pTracker.ID, ContactType.Move, new System.Windows.Point(pTracker.Position.X, pTracker.Position.Y), ScreenSize, pSource, pTracker));
        }

        /// <summary>
        /// Enqueue a 'ContactType.Start' event.
        /// </summary>
        /// <param name="pSource">The SpatioTemporalClassifier which was the source.</param>
        /// <param name="pTracker">The reference to the SpatioTemporalTracker responsible for tracking this input.</param>
        private void handleInputClassifier_OnStart(SpatioTemporalClassifier pSource, SpatioTemporalTracker pTracker)
        {
            // Enqueue a contact removed event.
            lFrame.Enqueue(new WiiContact(pTracker.ID, ContactType.Start, new System.Windows.Point(pTracker.Position.X, pTracker.Position.Y), ScreenSize, pSource, pTracker));
        }
        #endregion

        #region Start and Stop
        /// <summary>
        /// Instructs this input provider to begin generating events.
        /// </summary>
        public void start()
        {
            // Ensure we cannot process any events.
            //pDeviceMutex.WaitOne();

            // Create a new reference to a wiimote device.
            Exception pError = null;
            if (!this.initialiseWiimoteConnection(out pError))
            {
                //pDeviceMutex.ReleaseMutex();
                throw new Exception("Could not establish connection to Wiimote: " + pError.Message, pError);
            }

            // Clear the trackers.
            this.InputClassifier.reset();

            // Set the running flag.
            this.bRunning = true;

            // Release processing.
            //pDeviceMutex.ReleaseMutex();
        }

        /// <summary>
        /// Instructs this input provider to stop generating events.
        /// </summary>
        public void stop()
        {
            // Ensure we cannot process any events.
            pDeviceMutex.WaitOne();

            // Set the running flag.
            this.bRunning = false;

            // Reset the classifier.
            this.InputClassifier.reset();

            // Release processing.
            pDeviceMutex.ReleaseMutex();
        }
        #endregion

        #region Calibration
        /// <summary>
        /// Setup the calibration data which we want to use to transform points received by this Wiimote device before presenting
        /// them to the higher level application.
        /// </summary>
        /// <param name="pSource">The source rectangle.</param>
        /// <param name="pDestination">The destination rectangle.</param>
        /// <param name="vSize">The size of the screen which we will use to normalise input coordinates.</param>
        public void setCalibrationData(CalibrationRectangle pSource, CalibrationRectangle pDestination, Vector vSize)
        {
            // Set the source and destination.
            this.pWarper.setSource((float)pSource.TopLeft.X, (float)pSource.TopLeft.Y,
                                    (float)pSource.TopRight.X, (float)pSource.TopRight.Y,
                                    (float)pSource.BottomLeft.X, (float)pSource.BottomLeft.Y,
                                    (float)pSource.BottomRight.X, (float)pSource.BottomRight.Y);

            this.pWarper.setDestination((float)pDestination.TopLeft.X, (float)pDestination.TopLeft.Y,
                                        (float)pDestination.TopRight.X, (float)pDestination.TopRight.Y,
                                        (float)pDestination.BottomLeft.X, (float)pDestination.BottomLeft.Y,
                                        (float)pDestination.BottomRight.X, (float)pDestination.BottomRight.Y);

            // Save the newly computed screen size.
            this.ScreenSize = vSize;

            // Compute the new values.
            this.pWarper.computeWarp();
        }
        #endregion

        #region Connection creation and teardown.
        /// <summary>
        /// This method creates and sets up our connection to our class-gloal Wiimote device.
        /// This destroys any existing connection before creating a new one.
        /// </summary>
        /// <param name="pErrorReport">A reference to an exception which we want to contain our error if one happened.</param>
        private bool initialiseWiimoteConnection(out Exception pErrorReport)
        {
            // If we have an existing device, teardown the connection.
            this.teardownWiimoteConnection();

            // Check we have a valid device.
            if (this.pDevice == null)
                this.pDevice = new Wiimote();

            // Hook up device event handlers.
            this.pDevice.WiimoteChanged += new EventHandler<WiimoteChangedEventArgs>(handleWiimoteChanged);
            this.pDevice.WiimoteExtensionChanged += new EventHandler<WiimoteExtensionChangedEventArgs>(handleWiimoteExtensionChanged);

            // Try to establish a connection, enable the IR reader and flag some LEDs.
            try
            {
                this.pDevice.Connect();
                this.pDevice.SetReportType(InputReport.IRAccel, true);
                this.pDevice.SetLEDs(false, false, false, true);
            }

            // If something went wrong - notify the user..
            catch (Exception pError)
            {
                /*
                // Ensure we are ok.
                try
                {
                    this.teardownWiimoteConnection();
                }
                finally { }
                */
                // Say we screwed up.
                pErrorReport = pError;
                //throw new Exception("Error establishing connection: " + , pError);
                return false;
            }
            pErrorReport = null;
            return true;
        }

        /// <summary>
        /// This method destroys our connection to our class-global Wiimote device.
        /// </summary>
        private void teardownWiimoteConnection()
        {
            // If we don't have a device then do nothing.
            if (this.pDevice == null)
                return;

            // Close the connection and dispose of the device.
            this.pDevice.Disconnect();
            this.pDevice.Dispose();
            this.pDevice = null;
        }
        #endregion

        #region Wiimote Event Handlers
        /// <summary>
        /// This is called when an extension is attached or unplugged.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void handleWiimoteExtensionChanged(object sender, WiimoteExtensionChangedEventArgs e)
        {
            // Check we have a valid device.
            if (this.pDevice == null)
                return;

            // If an extension is attached at runtime we want to enable it.
            if (e.Inserted)
                this.pDevice.SetReportType(InputReport.IRExtensionAccel, true);
            else
                this.pDevice.SetReportType(InputReport.IRAccel, true);
        }

        /// <summary>
        /// This is called when the state of the wiimote changes and a new state report is available.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void handleWiimoteChanged(object sender, WiimoteChangedEventArgs e)
        {
            // Obtain mutual excluseion.
            pDeviceMutex.WaitOne();

            // If we not running then leave.
            if (!bRunning)
            {
                pDeviceMutex.ReleaseMutex();
                return;
            }

            // Store the state.
            WiimoteState pState = e.WiimoteState;

            // Contain active sensor data.
            List<SpatioTemporalInput> lInputs = new List<SpatioTemporalInput>();

            // For each IR sensor with data.
            IRSensor[] tSensors = pState.IRState.IRSensors;
            for (int iSensor = 0; iSensor < tSensors.Length; ++iSensor)
            {
                // If the sensor has data - queue an input for it.
                if (tSensors[iSensor].Found)
                {
                    // Extract the data from the sensor.
                    int x = tSensors[iSensor].RawPosition.X;
                    int y = tSensors[iSensor].RawPosition.Y;
                    float fWarpedX = x;
                    float fWarpedY = y;

                    // If we have transformation enabled
                    if (TransformResults)
                    {
                        // Transform the coordinates with the warper.
                        pWarper.warp(x, y, ref fWarpedX, ref fWarpedY);
                    }

                    // Make it into an input.
                    lInputs.Add(new SpatioTemporalInput((double)fWarpedX, (double)fWarpedY));
                }
            }

            // Prepare the frame to recieve new inputs.
            if (this.lFrame != null)
                this.lFrame = null;
            this.lFrame = new Queue<WiiContact>(1);

            // Now run these inputs through the classifier to see if they are related to any previous ones.
            // Thanks Nintendo... fix ye'r buffer ordering!
            this.InputClassifier.processFrame(lInputs);

            // Build that frame off to the input dispatcher.
            FrameEventArgs pFrame = new FrameEventArgs((ulong)Stopwatch.GetTimestamp(), this.lFrame);
            this.lFrame = null;

            // Ship it out!
            this.OnNewFrame(this, pFrame);

            /*
            // Has the 'A' button changed?
            if (!bLastTriggerState && pState.ButtonState.B)
            {
                // Click the button programmatically.
                Dispatcher.BeginInvoke(new Action(delegate()
                {
                    Button_Calibrate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }), null);
            }
            bLastTriggerState = pState.ButtonState.B;
            */

            // Update the battery state on the GUI.
            //Dispatcher.BeginInvoke(new Action(delegate()
            //{
            //    // float f = (((100.0f * 48.0f * (float)(ws.Battery / 48.0f))) / 192.0f);
            //    Bar_Battery.Value = (pState.Battery > 0xc8 ? 0xc8 : (int)pState.Battery);
            //}), null);
            this.BatteryState = (pState.Battery > 0xc8 ? 0xc8 : (int)pState.Battery);

            // Release mutual exclusion.
            pDeviceMutex.ReleaseMutex();
        }
        #endregion
    }
}
