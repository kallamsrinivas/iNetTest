using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;
using ISC.Instrument.Driver;


namespace ISC.iNet.DS.Instruments
{

    /// <summary>
    /// Summary description for GBPLUS.
    /// </summary>
    public class GBPLUS : InstrumentController
    {
        /// <summary>
        /// Instrument controller class for GasBadge Plus instruments.
        /// </summary>
        public GBPLUS() : base(new GbPlusDriver()) { }

		/// <summary>
		/// Used by the FactoryGBPLUS class.
		/// </summary>
		/// <param name="driver">The factory driver instance created by the FactoryGBPLUS class.</param>
		protected GBPLUS( GbPlusDriver driver ) : base( driver ) {}

        /// <summary>
        /// Get a list of a all of the users on an instrument, except the active one.
        /// </summary>
        /// <returns>An array list with all of the users, duplicates removed.</returns>
        public override List<string> GetUsers()
        {
            return new List<string>(); // GBPlus does not have a users list.
        }

        /// <summary>
        /// Sets the instrument users to the appropriate values.
        /// </summary>
        /// <param name="users">The list of users.</param>
        public override void SetUsers( List<string> users )
        {
            return; // GBPlus does not have users.
        }

        /// <summary>
        /// Get a list of a all of the sites on an instrument, except the active one.
        /// </summary>
        /// <returns>An array list with all of the sites, duplicates removed.</returns>
        public override List<string> GetSites()
        {
            return new List<string>(); // GBPlus does not have a site list.
        }

        /// <summary>
        /// Sets the instrument sites to the appropriate values.
        /// </summary>
        /// <param name="sites">The list of sites.</param>
        public override void SetSites( List<string> sites )
        {
            return; // GBPlus does not have sites.
        }

        /// <summary>
        /// // GBPlus does not support datalog, so do nothing here.
        /// </summary>
        /// <returns>The number of sessions cleared.</returns>
        public override int ClearDatalog()
        {
            return 0; // Return the number of datalog sessions cleared.
        }

        /// <summary>
        /// Retrieves the sensor setup date.  Since this is not supported on the GBPlus, return the minimum value.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor setup date</returns>
        public override DateTime GetSensorSetupDate(int sensorPosition)
        {
            // return DateTime.MinValue;
            // according to dev-JIRA INS-906, we should return the instrument setup date as the sensor setup date for the Plus.
            return GetSetupDate();
        }

// 12/14/07 JAM - removing this again.  The GBPlus IDS will now simply report the sensor setup version as being empty.
// It will be up to the server to report this as "NA" to iNet.

/*        public override string GetSensorSetupVersion(int sensorPosition)
        {
            // 12/10/07 JAM - dev-JIRA INS-920 says that iNet is getting back an empty sensor data version, which throws an error.
            // Instead, we should return "NA", which iNet accepts -- iNet then doesn't try to validate the sensor values.
            return NOT_APPLICABLE_FLAG;
        }
*/
        public override double GetSensorPeakReading(int sensorPosition, double resolution)
        {
            // not supported on GBPlus.
            return double.MinValue;
        }

        public override int GetSensorMinTemp(int sensorPosition) 
        { 
            // not supported by GBPlus
            return int.MinValue; 
        }

        public override int GetSensorMaxTemp(int sensorPosition) 
        {
            // not supported by GBPlus
            return int.MinValue; 
        }

    }  // end-class

} // end-namespace
