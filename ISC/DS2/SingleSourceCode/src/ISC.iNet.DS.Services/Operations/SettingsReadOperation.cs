using System.Collections.Generic;
using System.Text;
using System.Threading;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to read docking station settings.
	/// </summary>	
	public class SettingsReadOperation : SettingsReadAction , IOperation
	{
		#region Constructors

		/// <summary>
		/// Creates a new instance of SettingsReadOperation class.
		/// </summary>
        public SettingsReadOperation()
        {
            Init();
        }

        public SettingsReadOperation( SettingsReadAction settingsReadAction ) : base( settingsReadAction  )
        {
            Init();
        }

        private void Init()
        {
            // The action parent class doesn't have acess to the Configuration.DockingStation, so
            // it is unable to initialize ChangedSmartCards to the proper length. Instead, it just
            // defaults it to an empty array.  When we detect the empty array, we can just re-size the
            // array to the proper size.
            // The parent class is explicity not setting the array to null, because null is used
            // to trigger a read of all iGas cards (see GetCylinders()), instead of just the changed ones.
            if ( ChangedSmartCards != null && ChangedSmartCards.Length == 0 )
                ChangedSmartCards = new bool[Configuration.DockingStation.NumGasPorts];
        }

		#endregion


		#region Method

		/// <summary>
		/// Executes an instrument read settings operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
			// Make the return event.
			SettingsReadEvent settingsReadEvent = new SettingsReadEvent( this );
            settingsReadEvent.PostUpdate = this.PostUpdate;

			// Retrieve the docking station's information.
            settingsReadEvent.DockingStation = GetDockingStation( ChangedSmartCards );

            // If this ReadEvent is occurring after an Update Event, then SettngsRefId will contain
            // the ID of the settings used during the Update Event.  Otherwise, will be Nullid.
            // We place it into the read event's DockingStation in order to uploading it to iNet
            settingsReadEvent.DockingStation.RefId = this.SettingsRefId;

			// iNet needs the SettingsRead to occur (and be uploaded) so it can generate a "Return equipment" alert 
			// for the docking station when it has been replaced (or requested for return). To ensure the
			// SettingsRead occurs, we load the list of replaced equipment during the SettingsRead operation.
			LoadReplacedEquipment();

            // Boy, what a hack...  Deliberately sleep 3 seconds to give the LCD time to display
            // (and the user time to read) the 'reading settings' message.
            Thread.Sleep(3000);

			// Return the populated event.
			return settingsReadEvent;
		}

        /// <summary>
        /// Retrieve the docking station's information.  This includes the docking station's
        /// serialization info, hardware info, current settings, etc. but does NOT include
        /// the cylinders.
        /// </summary>
        /// <returns>A populated docking station.</returns>
        static public DockingStation GetDockingStation()
        {
            return GetDockingStation( new bool[ Configuration.DockingStation.NumGasPorts ] );
        }

        /// <summary>
        /// Retrieve the docking station's information.
        /// </summary>
        /// <returns>A new instance of a populated docking station.
        /// The caller may do what they wish with it.</returns>
        static public DockingStation GetDockingStation( bool[] changedSmartCards )
        {
            DockingStation dockingStation = Controller.GetDockingStation();

            // Don't go any further if we're not serialized. If we're not serialized,
            // the device's clock may not even be synchronized with iNet yet, so
			// calling GetCylinders would be a bad thing to do since
            // it assigns InstallTime to whatever cylinders it detects.
            if ( dockingStation.SerialNumber == string.Empty )
            {
                Log.Warning( "GetDockingStation not calling GetCylinders due to unserialized device." );
                return dockingStation;
            }

            // INS-3800 - need to tell iNet whether the DNS address were provided by user (i.e. via configurator) 
            // or provided by the DHCP server. The only way to really tell is see if the user configuration ("boot vars")
            // matches the network adapter's current settings. If not, then assume DHCP server provided the DNS addresses.
            // *** Configuration.Dockingstation's network settings will have the values that are saved in the "bootbars" (i.e., configured by the user).
            // *** Controller.Dockingstation's network settings are the settings that are actually in effect on the NIC.
            // What is configured will often differ from what is actually in effect.  For example, 
            // NOT the settings that the nic is currently using.
            bool userProvidedDns1 = dockingStation.NetworkSettings.DnsPrimary == Configuration.DockingStation.NetworkSettings.DnsPrimary;
            bool userProvidedDns2 = dockingStation.NetworkSettings.DnsSecondary == Configuration.DockingStation.NetworkSettings.DnsSecondary;
            // If either DNS does not match what user provided, then assume the DNS addresses were provided by the DHCP server.
            dockingStation.NetworkSettings.DnsDhcp = ( !userProvidedDns1 || !userProvidedDns2 );
            Log.Trace( string.Format( "GetDockingStation: DnsPrimary={0}, BootDnsPrimary={1}, userProvidedDns1={2}", dockingStation.NetworkSettings.DnsPrimary, Configuration.DockingStation.NetworkSettings.DnsPrimary, userProvidedDns1 ) );
            Log.Trace( string.Format( "GetDockingStation: DnsSecondary={0}, BootDnsSecondary={1}, userProvidedDns2={2}", dockingStation.NetworkSettings.DnsSecondary, Configuration.DockingStation.NetworkSettings.DnsSecondary, userProvidedDns2 ) );
            Log.Trace( string.Format( "GetDockingStation: DnsDhcp=" + dockingStation.NetworkSettings.DnsDhcp ) );

            // Get size of databases.
            dockingStation.InetDatabaseTotalSize = DataAccess.DataAccess.GetTotalSize( DataAccess.DataAccess.DataSource.iNetData );
            dockingStation.InetDatabaseUnusedSize = DataAccess.DataAccess.GetFreeSize( DataAccess.DataAccess.DataSource.iNetData );

            QueueDataAccess qda = new QueueDataAccess( ISC.iNet.DS.DataAccess.DataAccess.DataSource.iNetQueue );
            dockingStation.InetQueueDatabaseTotalSize = DataAccess.DataAccess.GetTotalSize( DataAccess.DataAccess.DataSource.iNetQueue );
            dockingStation.InetQueueDatabaseUnusedSize = DataAccess.DataAccess.GetFreeSize( DataAccess.DataAccess.DataSource.iNetQueue );

            Controller.LogDockingStation( dockingStation );

			Log.Debug( "        iNet DB Total Size: " + dockingStation.InetDatabaseTotalSize );
			Log.Debug( "       iNet DB Unused Size: " + dockingStation.InetDatabaseUnusedSize );
			Log.Debug( "       Queue DB Total Size: " + dockingStation.InetQueueDatabaseTotalSize );
			Log.Debug( "      Queue DB Unused Size: " + dockingStation.InetQueueDatabaseUnusedSize );

            // Get the installed cylinders.
            GetCylinders( dockingStation, changedSmartCards );

            return dockingStation;
        }

        static private void GetCylinders( DockingStation ds, bool[] changedSmartCards )
        {
            ds.GasEndPoints = ds.ChangedGasEndPoints = null;

            // On initial boot (changedSmartCards will be null), call old logic to read ALL cards
            // At all other times, the array should be non-null, meaning only read cards we know
            // have been inserted/removed. The intent is that we only read ALL cards one time on bootup.

            if ( changedSmartCards == null )
            {
                Log.Debug( "readAllCards=true.  Calling ReadInstalledCylinders" );
                ReadInstalledCylinders( ds.GasEndPoints, ds.Port1Restrictions );
            }
            else
            {
                Log.Debug( ChangedSmartCardsToString( changedSmartCards ) + ". Calling ReadChangedCylinders" );
                ReadChangedCylinders( changedSmartCards, ds.ChangedGasEndPoints, ds.Port1Restrictions );
            }
        }

        private static string ChangedSmartCardsToString( bool[] changedSmartCards )
        {
            StringBuilder changedString = new StringBuilder( "changedSmartCards={", 50 );
            for ( int i = 0; i < changedSmartCards.Length; i++ )
                changedString.AppendFormat( "{0}{1}={2}", ( i > 0 ) ? "," : "", i, changedSmartCards[i] );
            changedString.Append( "}" );
            return changedString.ToString();
        }

        /// <summary>
		/// Return the information from all smart cards, manifolds and manual cylinders that are present at start-up.
        /// </summary>
		/// <param name="installedCylinders">Information about the cylinders is placed into this passed-in list.</param>
        static private void ReadInstalledCylinders( List<GasEndPoint> gasEndPoints, PortRestrictions port1Restrictions )
        {
            const string funcName = "ReadInstalledCylinders: ";

            // Get all currently attached manifolds and manually-assigned cylinders.
            List<GasEndPoint> manGasEndPoints
                = new GasEndPointDataAccess().FindAll().FindAll ( m => m.InstallationType == GasEndPoint.Type.Manifold
					                                           || m.InstallationType == GasEndPoint.Type.Manual ); 

            for ( int position = 1; position <= Configuration.DockingStation.NumGasPorts; position++ )
            {
                Log.Debug( funcName + "POSITION " + position );

				// iGas cylinders take precendence
                if ( !SmartCardManager.IsCardPresent( position ) )
                {
                    Log.Debug( string.Format( "{0}Position {1}, No iGas card detected.", funcName, position ) );

                    // Does the port have a manifold or manual cylinder attached?  Then make sure we include
                    // that cylinder in the returned list. If no cylinder exists on port 1, then create a 
					// virtual fresh air cylinder.
                    GasEndPoint man = manGasEndPoints.Find( m => m.Position == position );
                    if (man != null)
                    {
                        Log.Debug(string.Format("{0}Position {1} {2} found (\"{3}\", \"{4}\", Pressure {5}).", funcName, position, 
							man.InstallationType == GasEndPoint.Type.Manifold ? "Manifold" : "Manual Cylinder",
                            man.Cylinder.FactoryId, man.Cylinder.PartNumber, man.Cylinder.Pressure ) );
                        gasEndPoints.Add( man );
                        
                    }
					else if (position == Controller.FRESH_AIR_GAS_PORT)
                    {
                        Log.Debug(string.Format("{0}Position {1} is assumed to be Fresh Air.", funcName, position ) );
                        GasEndPoint freshAirEndPoint = GasEndPoint.CreateFreshAir(position);
                        freshAirEndPoint.GasChangeType = GasEndPoint.ChangeType.Installed;
                        gasEndPoints.Add(freshAirEndPoint);
                    }
                    continue;
                }

                // IF WE MAKE IT TO HERE, THEN WE KNOW WE HAVE AN INSERTED SMART CARD WHICH MEANS iGas IS ATTACHED.

                Cylinder cylinder = SmartCardManager.ReadCard( position );
                if ( cylinder == null ) // Check for a valid cylinder.
                {
                    Log.Debug( string.Format( "{0}Position {1}, ReadCard returned null. SKIPPING cylinder.", funcName, position ) );
                    continue;
                }

                // Dates read from card will be in 'local' time, but everything we deal with is in UTC.
                cylinder.ExpirationDate = Configuration.ToUniversalTime( cylinder.ExpirationDate );
                cylinder.RefillDate = Configuration.ToUniversalTime( cylinder.RefillDate );

                Thread.Sleep( 1000 );

                if ( SmartCardManager.IsPressureSwitchPresent( position ) )
                {
                    if ( SmartCardManager.CheckPressureSwitch( position ) )
                        cylinder.Pressure = PressureLevel.Full;
                    else
                        cylinder.Pressure = PressureLevel.Low;

                    Log.Debug( string.Format( "{0}Position {1} Pressure Switch reports {2}.", funcName, position, cylinder.Pressure ) );
                }
                else
                    Log.Debug( string.Format( "{0}Position {1} Pressure Switch not detected.", funcName, position ) );

                GasEndPoint gasEndPoint = new GasEndPoint( cylinder, position, GasEndPoint.Type.iGas );

                // Add the installed cylinder to the DockingStation (IDS).
                gasEndPoints.Add( gasEndPoint );
            }

            return;
        }

        /// <summary>
        /// Return the information from all smart cards that are present.
        /// </summary>
        /// <returns>All of the installed cylinders.</returns>
        static private void ReadChangedCylinders( bool[] changedSmartCards, IList<GasEndPoint> changedGasEndPoints, PortRestrictions port1Restrictions )
        {
            const string funcName = "ReadChangedCylinders: ";

            Log.Assert( changedSmartCards != null, funcName + "changedSmartCards should not be null." );

            DockingStation dockingStation = Controller.GetDockingStation(); // SGF  8-Nov-2012  INS-2686

            changedGasEndPoints.Clear();

            for ( int i = 0; i < changedSmartCards.Length; i++ )
            {
                int position = i + 1;

                Log.Debug( funcName + "POSITION " + position );

                if ( changedSmartCards[ position - 1 ] == false ) // No detection of a card insertion, nor a removal?
                {
                    Log.Debug( string.Format( "{0}Position {1} Smart card SKIPPED; No insertion change detected.", funcName, position ) );

                    if ( position == Controller.FRESH_AIR_GAS_PORT )
                    {
                        GasEndPoint persistedPort1GasEndPoint = new GasEndPointDataAccess().FindByPosition( Controller.FRESH_AIR_GAS_PORT );

                        // If there's nothing known to be installed on port 1, then create a fresh air  
                        // cylinder for the port.
                        // This could happen if, while nothing was installed on the port, a SettingsUpdate
                        // just occurred previously where the port setting was changed to allow fresh air.
                        // In that situation, we have to then make the fresh air cylinder.
                        if ( persistedPort1GasEndPoint == null )
                        {
                            Log.Debug( string.Format( "{0}Position {1}, No persisted cylinder; assuming Fresh Air.", funcName, position ) );
                            GasEndPoint freshAirCylinder = GasEndPoint.CreateFreshAir( position );
                            freshAirCylinder.GasChangeType = GasEndPoint.ChangeType.Installed;
                            changedGasEndPoints.Add( freshAirCylinder );
                        }
                    }

                    // SGF  8-Nov-2012  INS-2686 -- begin
                    // Soon, we will check to see if a pressure switch is present, and if so, read the pressure level.
                    // Before we do that, we must check for two scenarios in which it is not necessary or appropriate 
                    // to read a pressure switch.
                    //     1. If there is no cylinder attached to the port, there is no reason to check for a pressure switch.
                    //     2. If this is the first port, and we know that the port is drawing fresh air, there is no reason 
                    //        to check for a pressure switch.  This case cannot be handled by case #1, as we define a "logical"
                    //        cylinder to represent fresh air.
                    // If we find either scenario for the current port, we skip further processing on this port, and proceed to
                    // the next one.
                    GasEndPoint currentInstalledCylinder = dockingStation.GasEndPoints.Find(ic => ic.Position == position);
                    if (currentInstalledCylinder == null)
                    {
                        Log.Debug(string.Format("{0}Position {1} No cylinder present; do not check for pressure switch.", funcName, position));
                        continue;
                    }
                    else
                    {
                        bool isFreshAir = currentInstalledCylinder.Cylinder.IsFreshAir;
                        if (isFreshAir)
                        {
                            Log.Debug(string.Format("{0}Position {1} Fresh air; do not check for pressure switch.", funcName, position));
                            continue;
                        }
                    }
                    // SGF  8-Nov-2012  INS-2686 -- end

                    // SMARTCARD NOT CHANGED (NO INSERT OR REMOVAL DETECTED).
                    // WE NEED TO AT LEAST ALWAYS READ THE PRESSURE SWITCH THEN.
                    if ( SmartCardManager.IsPressureSwitchPresent( position ) )
                    {
                        GasEndPoint pressureCylinder = new GasEndPoint();
                        pressureCylinder.Position = position;
                        pressureCylinder.Cylinder.Pressure = ReadPressureLevel( position );
                        pressureCylinder.GasChangeType = GasEndPoint.ChangeType.PressureChanged;

                        Log.Debug( string.Format( "{0}Position {1} Pressure Switch reports {2}.", funcName, position, pressureCylinder.Cylinder.Pressure ) );

                        changedGasEndPoints.Add( pressureCylinder );
                    }
                    else
                        Log.Debug( string.Format( "{0}Position {1} Pressure Switch not present.", funcName, position ) );

                    continue;
                }

                // IF WE MAKE IT TO HERE, THE CARD HAS BEEN EITHER INSERTED OR REMOVED.

                if ( !SmartCardManager.IsCardPresent( position ) )  // CARD REMOVED?
                {
                    Log.Debug( string.Format( "{0}Position {1} SmartCard not present, Returning CardRemoved", funcName, position ) );

                    // Server needs to know specifically that cylinder is missing.
                    // Indicate this with an InstalledCylinder containing an empty Cylinder object.
                    GasEndPoint missingCylinder = new GasEndPoint();
                    missingCylinder.Position = position;
                    missingCylinder.GasChangeType = GasEndPoint.ChangeType.Uninstalled;
                    changedGasEndPoints.Add( missingCylinder );

                    // If a cylinder is not installed on the fresh air port, then assume fresh air 
                    // for the port
                    if ( position == Controller.FRESH_AIR_GAS_PORT )
                    {
                        Log.Debug( string.Format( "{0}Position {1} is assumed to be Fresh Air.", funcName, position ) );
                        GasEndPoint freshAirCylinder = GasEndPoint.CreateFreshAir( position );
                        freshAirCylinder.GasChangeType = GasEndPoint.ChangeType.Installed;
                        changedGasEndPoints.Add( freshAirCylinder );
                    }

                    continue;
                }

                // IF WE MAKE IT TO HERE, THE CARD HAS BEEN INSERTED.

                Cylinder cylinder = SmartCardManager.ReadCard( position );
                if ( cylinder == null )  // Couldn't read valid cylinder?  Driver error or corrupt card.
                {
                    Log.Debug( string.Format( "{0}Position {1}, ReadCard returned null. SKIPPING cylinder.", funcName, position ) );
                    continue;
                }

                // Dates read from card will be in 'local' time, but everything we deal with is in UTC.
                cylinder.ExpirationDate = Configuration.ToUniversalTime( cylinder.ExpirationDate );
                cylinder.RefillDate = Configuration.ToUniversalTime( cylinder.RefillDate );

                Thread.Sleep( 1000 );

                cylinder.Pressure = ReadPressureLevel( position );

                GasEndPoint gasEndPoint = new GasEndPoint( cylinder, position, GasEndPoint.Type.iGas );
                gasEndPoint.GasChangeType = GasEndPoint.ChangeType.Installed;
                Log.Debug( string.Format( "{0}Position {1}, Returning CardInserted. Pressure={2}", funcName, position, cylinder.Pressure ) );

                changedGasEndPoints.Add( (GasEndPoint)gasEndPoint.Clone() );

            }  // end-for

            return;
        }

        /// <summary>
        /// If pressure switch is present, then this methods reads the pressure level from it,
        /// otherwise this method returns Full.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        static private PressureLevel ReadPressureLevel( int position )
        {
            const string funcName = "ReadPressureLevel";

            if ( !SmartCardManager.IsPressureSwitchPresent( position ) )
            {
                Log.Debug( funcName + ": Cylinder " + position + " has no pressure switch. Defaulting to Full" );
                return PressureLevel.Full;
            }

            if ( SmartCardManager.CheckPressureSwitch( position ) )
            {
                Log.Debug( funcName + ": Cylinder " + position + " pressure is Full" );
                return PressureLevel.Full;
            }

            Log.Debug( funcName + ": Cylinder " + position + " pressure is Low" );
            return PressureLevel.Low;
        }

		/// <summary>
		/// Loads the ReplacedEquipment serial numbers from the locally stored database.
		/// </summary>
		private void LoadReplacedEquipment()
		{
			List<string> replacedSerialNumberList;

			using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
			{
				replacedSerialNumberList = new ReplacedEquipmentDataAccess().FindAll( trx );
			}

			Dictionary<string, string> replacedEquipment = new Dictionary<string, string>();
			foreach ( string key in replacedSerialNumberList )
			{
				// if the key does not exist (which is expected), 
				// a new key/value pair is added to the dictionary
				replacedEquipment[key] = key;
			}

			Master.Instance.ExecuterService.ReplacedEquipment = replacedEquipment;
		}

        #endregion Methods

    } // end-class SettingsReadOperation

}
