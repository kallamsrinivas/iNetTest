using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using ISC.iNet.DS;
using ISC.iNet.DS.DomainModel;
using ISC.Instrument.Driver;
using ISC.Instrument.TypeDefinition;
using ISC.WinCE.Logger;
using AlarmEvent = ISC.iNet.DS.DomainModel.AlarmEvent;
using SensorGasResponse = ISC.iNet.DS.DomainModel.SensorGasResponse;


namespace ISC.iNet.DS.Instruments
{
    /// <summary>
    /// Summary description for TX1.
    /// </summary>
    public class TX1 : InstrumentController
    {
        #region Constructors

        /// <summary>
        /// Instrument controller class for TX1 instruments.
        /// </summary>
        public TX1() : base( new Tx1Driver() ) { }

		/// <summary>
		/// Used by the FactoryTX1 class.
		/// </summary>
		/// <param name="driver">The factory driver instance created by the FactoryTX1 class.</param>
		protected TX1( Tx1Driver driver )
			: base( driver )
		{

		}

        #endregion

        #region Methods

        /// <summary>
        /// Returns the instrument's country of origin.
        /// </summary>
        /// <returns></returns>
        public override string GetCountryOfOriginCode()
        {
            return Driver.getCountryOfOrigin().ToString().ToUpper();
        }
        
        public override double GetSensorPeakReading( int sensorPosition, double resolution )
        {
            return Driver.getPeakReading( sensorPosition );
        }

        /// <summary>
        /// Get a list of all of the users on an instrument, except the active one.
        /// </summary>
        /// <returns>An array list with all of the users, duplicates removed.</returns>
        public override List<string> GetUsers()
        {
            List<string> list = new List<string>();

            // Only one user - the 'Active' user.
            string user = GetActiveUser();
            if (user.Length > 0)
                list.Add(user);

            return list;
        }

        /// <summary>
        /// Sets the instrument users to the appropriate values.
        /// </summary>
        /// <param name="users">The list of users.</param>
        public override void SetUsers(List<string> users)
        {
            string oldUser = GetActiveUser();

            if (users.Count == 0 && oldUser == string.Empty)
                return;

            if (users.Count > 1)
                Log.Error("WARNING: detected attempt to set " + users.Count + " users for TX1");

            // set active user only if it's different than what is current in the instrument
            string newUser = (users.Count > 0) ? (string)users[0] : string.Empty;
            if (oldUser != newUser)
                SetActiveUser(newUser);

            return;
        }

        /// <summary>
        /// Get a list of a all of the sites on an instrument, except the active one.
        /// </summary>
        /// <returns>An array list with all of the sites, duplicates removed.</returns>
        public override List<string> GetSites()
        {
            List<string> list = new List<string>();

            // Only one site - the 'Active' site.
            string site = GetActiveSite();

            if (site.Length > 0)
                list.Add(site);

            return list;
        }

        /// <summary>
        /// Sets the instrument sites to the appropriate values.
        /// </summary>
        /// <param name="sites">The list of sites.</param>
        public override void SetSites(List<string> sites)
        {
            string oldSite = GetActiveSite();

            if (sites.Count == 0 && oldSite == string.Empty)
                return;

            if (sites.Count > 1)
                Log.Debug("WARNING: detected attempt to set " + sites.Count + " sites for TX1");

            // set active site if it's different than what is current only the instrument

            string newSite = (sites.Count > 0) ? (string)sites[0] : string.Empty;
            if (oldSite != newSite)
                SetActiveSite(newSite);

            return;
        }

        public override SensorGasResponse[] GetManualGasOperations()
        {
            SensorGasResponse[] gasResponses = base.GetManualGasOperations();

            // Nobody (iNet server, nor iNetDS) is interested in SGR's for "virtual" sensors;
            // so, we need to just throw them away.
            List<SensorGasResponse> sgrList = new List<SensorGasResponse>( gasResponses.Length );

            foreach ( SensorGasResponse sgr in gasResponses )
            {
				if ( !InstrumentTypeDefinition.IsVirtualSerialNumber( sgr.SerialNumber ) )
                    sgrList.Add( sgr );
            }

            return sgrList.ToArray();
        }

        #endregion

    }  // end-class

} // end-namespace
