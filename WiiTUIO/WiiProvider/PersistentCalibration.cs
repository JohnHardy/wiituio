using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows;

namespace WiiTUIO.Provider
{
    /// <summary>
    /// This class is responsible for saving, loading and deleting persistent calibration data.
    /// It enables users to start using a system without having to re-calibrate before every use.
    /// </summary>
    public class PersistentCalibration
    {
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
    }

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
}
