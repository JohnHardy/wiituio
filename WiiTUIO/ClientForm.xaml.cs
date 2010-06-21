using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using WiiTUIO.WinTouch;
using WiiTUIO.Provider;
using OSC.NET;

namespace WiiTUIO
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ClientForm : UserControl
    {
        /// <summary>
        /// A reference to the WiiProvider we want to use to get/forward input.
        /// </summary>
        private WiiProvider pWiiProvider = null;

        /// <summary>
        /// A reference to an OSC data transmitter.
        /// </summary>
        private OSCTransmitter pUDPWriter = null;

        /// <summary>
        /// A reference to the windows 7 HID driver data provider.  This takes data from the <see cref="pWiiProvider"/> and transforms it.
        /// </summary>
        private ProviderHandler pTouchDevice = null;

        /// <summary>
        /// A refrence to the calibration window.
        /// </summary>
        private CalibrationWindow pCalibrationWindow = null;

        /// <summary>
        /// Boolean to tell if we are connected to the mote and network.
        /// </summary>
        private bool bConnected = false;

        /// <summary>
        /// Boolean to tell if we have received a reconnect command.
        /// </summary>
        private bool bReconnect = false;

        /// <summary>
        /// Construct a new Window.
        /// </summary>
        public ClientForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called when the IP Address has been changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtIPAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Flag that we want to reconnect.
            bReconnect = true;
            btnConnect.Content = "Reconnect";
        }

        /// <summary>
        /// Called when the port has been changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Flag that we want to reconnect.
            bReconnect = true;
            btnConnect.Content = "Reconnect";
        }

        /// <summary>
        /// Called when the calibrate button has been clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCalibrate_Click(object sender, RoutedEventArgs e)
        {
            // Create a calibrate form.
            if (pCalibrationWindow != null)
                pCalibrationWindow.Close();

            try
            {
                // Create a new calibration window.
                this.pCalibrationWindow = new CalibrationWindow();
                //this.pCalibrationWindow.Topmost = true;
                this.pCalibrationWindow.WindowStyle = WindowStyle.None;
                this.pCalibrationWindow.WindowState = WindowState.Maximized;
                this.pCalibrationWindow.Show();

                // Event handler for the finish calibration.
                this.pCalibrationWindow.CalibrationCanvas.OnCalibrationFinished += new Action<WiiProvider.CalibrationRectangle, WiiProvider.CalibrationRectangle, Vector>(CalibrationCanvas_OnCalibrationFinished);

                // Begin the calibration.
                this.pCalibrationWindow.CalibrationCanvas.beginCalibration(this.pWiiProvider);
            }
            catch (Exception pError)
            {
                MessageBox.Show(pError.Message, "WiiTUIO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// This is called when calibration is finished.
        /// </summary>
        private void CalibrationCanvas_OnCalibrationFinished(WiiProvider.CalibrationRectangle pSource, WiiProvider.CalibrationRectangle pDestination, Vector vScreenSize)
        {
            // Persist the calibration data
            if (!savePersistentCalibration("./Calibration.dat", new PersistentCalibrationData(pSource, pDestination, vScreenSize)))
            {
                // Error - Failed to save calibration data
                MessageBox.Show("Failed to save calibration data", "WiiTUIO", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Close the calibration window.
            if (pCalibrationWindow != null)
            {
                pCalibrationWindow.Close();
            }
        }

        /// <summary>
        /// Called when the 'About' button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            String sMessage = "WiiTUIO was written by John Hardy & Christopher Bull of the HighWire Programme at Lancaster University.";
            sMessage += "\nYou can contact us at: hardyj2@unix.lancs.ac.uk & c.bull@lancaster.ac.uk";
            sMessage += "\n";
            sMessage += "\nCredits:";
            sMessage += "\n  Johnny Chung Lee: http://johnnylee.net/projects/wii/";
            sMessage += "\n  Brian Peek: http://www.brianpeek.com/";
            sMessage += "\n  TUIO Project: http://www.tuio.org";
            sMessage += "\n  OSC.NET Library: http://luvtechno.net/";
            TextBlock pMessage = new TextBlock();
            pMessage.Text = sMessage;
            pMessage.TextWrapping = TextWrapping.Wrap;
            pMessage.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            pMessage.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            pMessage.FontSize = 12.0;
            pMessage.FontWeight = FontWeights.Bold;
            pMessage.Foreground = new SolidColorBrush(Colors.White);
            showMessage(pMessage, MessageType.Info);
            //MessageBox.Show(sMessage, "About WiiTUIO", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Called when the 'Connect' or 'Disconnect' button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            // If we are in reconnect mode..
            if (bReconnect)
            {
                disconnectProviders();
                disconnectTransmitter();
                disconnectWindowsTouch();
                btnConnect.Content = "Connect";
                bConnected = false;
                bReconnect = false;
            }

            // If we have been asked to connect.
            if (!bConnected)
            {
                // Connect.
                if (this.createProviders() && this.connectTransmitter() && this.connectWindowsTouch())
                {
                    // Update the button to say we are connected.
                    btnConnect.Content = "Disconnect";
                    bConnected = true;

                    // Load calibration data.
                    PersistentCalibrationData oData = loadPersistentCalibration("./Calibration.dat");
                    if (oData != null)
                        this.pWiiProvider.setCalibrationData(oData.Source, oData.Destination, oData.ScreenSize);
                }
                else
                {
                    disconnectProviders();
                    disconnectTransmitter();
                    disconnectWindowsTouch();
                    btnConnect.Content = "Connect";
                    bConnected = false;
                }
            }

            // Otherwise be sure I am disconnected.
            else
            {
                disconnectProviders();
                disconnectTransmitter();
                disconnectWindowsTouch();
                btnConnect.Content = "Connect";
                bConnected = false;
            }
        }

        private static int iFrame = 0;
        /// <summary>
        /// Process an event frame and convert the data into a TUIO message.
        /// </summary>
        /// <param name="e"></param>
        private void processEventFrame(FrameEventArgs e)
        {
            // If Windows 7 events are enabled.
            if (true)
            {
                // For every contact in the list of contacts.
                foreach (WiiContact pContact in e.Contacts)
                {
                    // Construct a new HID frame based on the contact type.
                    switch (pContact.Type)
                    {
                        case ContactType.Start:
                            this.pTouchDevice.enqueueContact(HidContactState.Adding, pContact);
                            break;
                        case ContactType.Move:
                            this.pTouchDevice.enqueueContact(HidContactState.Updated, pContact);
                            break;
                        case ContactType.End:
                            this.pTouchDevice.enqueueContact(HidContactState.Removing, pContact);
                            break;
                    }
                }

                // Flush the contacts?
                // this.pTouchDevice.sendContacts();
            }


            // If TUIO events are enabled.
            if (true)
            {
                // Create an new TUIO Bundle
                OSCBundle pBundle = new OSCBundle();

                // Create a fseq message and save it.  This is to associate a unique frame id with a bundle of SET and ALIVE.
                OSCMessage pMessageFseq = new OSCMessage("/tuio/2Dcur");
                pMessageFseq.Append("fseq");
                pMessageFseq.Append(++iFrame);//(int)e.Timestamp);
                pBundle.Append(pMessageFseq);

                // Create a alive message.
                OSCMessage pMessageAlive = new OSCMessage("/tuio/2Dcur");
                pMessageAlive.Append("alive");

                // Now we want to take the raw frame data and draw points based on its data.
                foreach (WiiContact pContact in e.Contacts)
                {
                    // Compile the set message.
                    OSCMessage pMessage = new OSCMessage("/tuio/2Dcur");
                    pMessage.Append("set");                 // set
                    pMessage.Append((int)pContact.ID);           // session
                    pMessage.Append((float)pContact.NormalPosition.X);   // x
                    pMessage.Append((float)pContact.NormalPosition.Y);   // y
                    pMessage.Append(0f);                 // dx
                    pMessage.Append(0f);                 // dy
                    pMessage.Append(0f);                 // motion
                    pMessage.Append((float)pContact.Size.X);   // height
                    pMessage.Append((float)pContact.Size.Y);   // width

                    // Append it to the bundle.
                    pBundle.Append(pMessage);

                    // Append the alive message for this contact to tbe bundle.
                    pMessageAlive.Append((int)pContact.ID);
                }

                // Save the alive message.
                pBundle.Append(pMessageAlive);

                // Send the message off.
                this.pUDPWriter.Send(pBundle);
            }
        }

        /// <summary>
        /// This is called when the WiiProvider has a new set of input to send.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pWiiProvider_OnNewFrame(object sender, FrameEventArgs e)
        {
            // Are we calibrating.
            if (this.pCalibrationWindow != null)
                if (this.pCalibrationWindow.CalibrationCanvas.IsCalibrating)
                    return;

            // If dispatching events is enabled.
            if (bConnected)
            {
                // Call these in another thread.
                Dispatcher.BeginInvoke(new Action(delegate()
                {
                    processEventFrame(e);
                }), null);
            }
        }

        /// <summary>
        /// This is called when the battery state changes.
        /// </summary>
        /// <param name="obj"></param>
        private void pWiiProvider_OnBatteryUpdate(int obj)
        {
            // Dispatch it.
            Dispatcher.BeginInvoke(new Action(delegate()
            {
                this.barBattery.Value = obj;
            }), null);
        }

        enum MessageType { Info, Error };

        private void showMessage(string sMessage, MessageType eType)
        {
            TextBlock pMessage = new TextBlock();
            pMessage.Text = sMessage;
            pMessage.TextWrapping = TextWrapping.Wrap;
            pMessage.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            pMessage.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            pMessage.FontSize = 16.0;
            pMessage.FontWeight = FontWeights.Bold;
            pMessage.Foreground = new SolidColorBrush(Colors.White);
            showMessage(pMessage, 750.0, eType);
        }

        private void showMessage(UIElement pMessage, MessageType eType)
        {
            showMessage(pMessage, 750.0, eType);
        }

        private void showMessage(UIElement pElement, double fTimeout, MessageType eType)
        {
            // Show (and possibly initialise) the error message overlay
            brdOverlay.Height = this.ActualHeight - 8;
            brdOverlay.Width = this.ActualWidth - 8;
            brdOverlay.Opacity = 0.0;
            brdOverlay.Visibility = System.Windows.Visibility.Visible;
            switch (eType)
            {
                case MessageType.Error:
                    brdOverlay.Background = new SolidColorBrush(Color.FromArgb(192, 255, 0, 0));
                    break;
                case MessageType.Info:
                    brdOverlay.Background = new SolidColorBrush(Color.FromArgb(192, 0, 0, 255));
                    break;
            }

            // Set the message
            brdOverlay.Child = pElement;

            // Fade in and out.
            messageFadeIn(fTimeout, false);
        }

        private void messageFadeIn(double fTimeout, bool bFadeOut)
        {
            // Now fade it in with an animation.
            DoubleAnimation pAnimation = createDoubleAnimation(1.0, fTimeout, false);
            pAnimation.Completed += delegate(object sender, EventArgs pEvent)
            {
                if (bFadeOut)
                    this.messageFadeOut(fTimeout);
            };
            pAnimation.Freeze();
            brdOverlay.BeginAnimation(Canvas.OpacityProperty, pAnimation, HandoffBehavior.Compose);

        }
        private void messageFadeOut(double fTimeout)
        {
            // Now fade it in with an animation.
            DoubleAnimation pAnimation = createDoubleAnimation(0.0, fTimeout, false);
            pAnimation.Completed += delegate(object sender, EventArgs pEvent)
            {
                // We are now faded out so make us invisible again.
                brdOverlay.Visibility = Visibility.Hidden;
            };
            pAnimation.Freeze();
            brdOverlay.BeginAnimation(Canvas.OpacityProperty, pAnimation, HandoffBehavior.Compose);
        }

        #region Animation Helpers
        /**
         * @brief Helper method to create a double animation.
         * @param fNew The new value we want to move too.
         * @param fTime The time we want to allow in ms.
         * @param bFreeze Do we want to freeze this animation (so we can't modify it).
         */
        private static DoubleAnimation createDoubleAnimation(double fNew, double fTime, bool bFreeze)
        {
            // Create the animation.
            DoubleAnimation pAction = new DoubleAnimation(fNew, new Duration(TimeSpan.FromMilliseconds(fTime)))
            {
                // Specify settings.
                AccelerationRatio = 0.1,
                DecelerationRatio = 0.9,
                FillBehavior = FillBehavior.HoldEnd
            };

            // Pause the action before starting it and then return it.
            if (bFreeze)
                pAction.Freeze();
            return pAction;
        }
        #endregion


        #region Create and Die

        /// <summary>
        /// Create the link to the Windows 7 HID driver.
        /// </summary>
        /// <returns></returns>
        private bool connectWindowsTouch()
        {
            try
            {
                // Close any open connections.
                disconnectWindowsTouch();

                // Reconnect with the new API.
                this.pTouchDevice = new ProviderHandler();
                return true;
            }
            catch (Exception pError)
            {
                // Tear down.
                try
                {
                    this.disconnectWindowsTouch();
                }
                catch { }

                // Report the error.
                showMessage(pError.Message, MessageType.Error);
                //MessageBox.Show(pError.Message, "WiiTUIO", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Destroy the link to the Windows 7 HID driver.
        /// </summary>
        /// <returns></returns>
        private void disconnectWindowsTouch()
        {
            // Remove any provider links.
            //if (this.pTouchDevice != null)
            //    this.pTouchDevice.Provider = null;
            this.pTouchDevice = null;
        }

        #region UDP TUIO
        /// <summary>
        /// Connect the UDP transmitter using the port and IP specified above.
        /// </summary>
        /// <returns></returns>
        private bool connectTransmitter()
        {
            try
            {
                // Close any open connections.
                disconnectTransmitter();

                // Reconnect with the new API.
                pUDPWriter = new OSCTransmitter(txtIPAddress.Text, Int32.Parse(txtPort.Text));
                pUDPWriter.Connect();
                return true;
            }
            catch (Exception pError)
            {
                // Tear down.
                try
                {
                    this.disconnectTransmitter();
                }
                catch { }

                // Report the error.
                showMessage(pError.Message, MessageType.Error);
                //MessageBox.Show(pError.Message, "WiiTUIO", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Disconnect the UDP Transmitter.
        /// </summary>
        /// <returns></returns>
        private void disconnectTransmitter()
        {
            // Close any open connections.
            if (pUDPWriter != null)
                pUDPWriter.Close();
            pUDPWriter = null;
        }
        #endregion

        #region WiiProvider
        /// <summary>
        /// Try to create the WiiProvider (this involves connecting to the Wiimote).
        /// </summary>
        private bool createProviders()
        {
            try
            {
                // Connect a Wiimote, hook events then start.
                this.pWiiProvider = new WiiProvider();
                this.pWiiProvider.OnNewFrame += new EventHandler<FrameEventArgs>(pWiiProvider_OnNewFrame);
                this.pWiiProvider.OnBatteryUpdate += new Action<int>(pWiiProvider_OnBatteryUpdate);
                this.pWiiProvider.start();
                return true;
            }
            catch (Exception pError)
            {
                // Tear down.
                try
                {
                    this.disconnectProviders();
                }
                catch { }

                // Report the error.
                showMessage(pError.Message, MessageType.Error);
                //MessageBox.Show(pError.Message, "WiiTUIO", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Tear down the provider connections.
        /// </summary>
        private void disconnectProviders()
        {
            // Disconnect the Wiimote.
            if (this.pWiiProvider != null)
                this.pWiiProvider.stop();
            this.pWiiProvider = null;
        }
        #endregion

        #region Form Stuff
        /// <summary>
        /// Raises the <see cref="E:System.Windows.FrameworkElement.Initialized"/> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data.</param>
        protected override void OnInitialized(EventArgs e)
        {
            // Create the providers.
            //this.createProviders();

            // Add text change events
            txtIPAddress.TextChanged += txtIPAddress_TextChanged;
            txtPort.TextChanged += txtPort_TextChanged;

            // Call the base class.
            base.OnInitialized(e);
        }

        ~ClientForm()
        {
            // Disconnect the providers.
            this.disconnectProviders();
        }
        #endregion
        #endregion

        #region Persistent Calibration Data
        /// <summary>
        /// Creates and saves a file which contains the calibration data.
        /// </summary>
        /// <param name="sFile">The location of the file to persist to</param>
        /// <param name="oData">The calibration data to persist</param>
        public static bool savePersistentCalibration(string sFile, PersistentCalibrationData oData)
        {
            try
            {
                FileStream stream = File.Open(sFile, FileMode.Create);
                new BinaryFormatter().Serialize(stream, oData);
                stream.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads the specified file, which should contain calibration data.
        /// </summary>
        /// <param name="sFile">The location of the file to load</param>
        public static PersistentCalibrationData loadPersistentCalibration(string sFile)
        {
            try
            {
                if (File.Exists(sFile))
                {
                    // De-serialise data from file
                    Stream stream = File.Open(sFile, FileMode.Open);
                    PersistentCalibrationData data = (PersistentCalibrationData)new BinaryFormatter().Deserialize(stream);
                    stream.Close();
                    return data;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
        #endregion

        private void btnAboutWinTouch_Click(object sender, RoutedEventArgs e)
        {

        }

        private void brdOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            messageFadeOut(750.0);
        }
    }

    #region Persistent Calibration Data Serialisation Helper
    /// <summary>
    /// Wrapper class for persistent calibration data.
    /// This classes is required for saving data and is also returned from a load.
    /// </summary>
    [Serializable]
    public class PersistentCalibrationData
    {
        /// <summary> The source calibration rectangle. </summary>
        public WiiProvider.CalibrationRectangle Source { get; private set; }
        /// <summary> The destination calibration rectangle. </summary>
        public WiiProvider.CalibrationRectangle Destination { get; private set; }
        /// <summary> The screen size. </summary>
        public Vector ScreenSize { get; private set; }
        /// <summary> When the calibration data swas created. </summary>
        public DateTime TimeStamp { get; private set; }

        /// <summary>
        /// Creates a wrapper object for persisting calibration data.
        /// </summary>
        /// <param name="pSource">The source calibration rectangle</param>
        /// <param name="pDestination">The destination calibration rectangle</param>
        /// <param name="vScreenSize">The screen size</param>
        public PersistentCalibrationData(WiiProvider.CalibrationRectangle pSource, WiiProvider.CalibrationRectangle pDestination, Vector vScreenSize)
        {
            this.Source = pSource;
            this.Destination = pDestination;
            this.ScreenSize = vScreenSize;
            this.TimeStamp = DateTime.Now;
        }
    }
    #endregion
}
