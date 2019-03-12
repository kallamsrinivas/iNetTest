using System;

namespace ISC.iNet.DS.Instruments
{
    public enum SensorMode
    {
        Uninstalled = 0,
        Installed = 1,
        Error = -1
    }

    /// <summary>
    /// 
    /// </summary>
    public class SensorPosition
    {
        private int _position;
        private SensorMode _mode;
		private bool _isDualSenseCapable;

        public SensorPosition( int position, SensorMode mode, bool isDualSenseCapable )
        {
            _position = position;
			_isDualSenseCapable = isDualSenseCapable;
            _mode = mode;
        }

        /// <summary>
        /// The position number of a sensor.  1 through N.
        /// </summary>
        public int Position
        {
            get 
            {
                return _position;
            }
        }

        /// <summary>
        /// Status of the sensor (Installed, Uninstalled, Error)
        /// </summary>
        public SensorMode Mode
        {
            get
            {
                return _mode;
            }
        }

		/// <summary>
		/// Is the sensor capable of being in DualSense mode.  This sensor will need 
		/// another DualSense capable sensor to pair with to actually be in DualSense mode.
		/// Other fields (sensor part number, sensor code, etc.) may also need to match between
		/// DualSenseCapable sensors for them to actually be DualSensed together.
		/// </summary>
		public bool IsDualSenseCapable
		{
			get
			{
				return _isDualSenseCapable;
			}
		}
    }
}
