namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Provides the functionality for determining the Sensor Calibration Limits. As of now it deals with Sensor Age, but it can be further expanded to deal with other sensor calibration limits
    /// </summary>
    public class SensorCalibrationLimits
    {
        public string SensorCode { get; set; }
        public int Age { get; set; }

        public SensorCalibrationLimits()
        {
        }

        /// <summary>
        /// Creates a new instance of SensorCalibrationLimits class.
        /// </summary>
        public SensorCalibrationLimits( string sensorCode, int age )
        {
            SensorCode = sensorCode;
            Age = age;
        }        
    }
}
