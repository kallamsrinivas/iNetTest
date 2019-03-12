using System;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to perform a discovery operation.
	/// </summary>
	public class DiscoveryOperation : NothingAction, IOperation
	{
		#region Fields
				
		#endregion
		
		#region Constructors

		/// <summary>
		/// Creates a new instance of DiscoveryOperation.
		/// </summary>
		public DiscoveryOperation()
		{
            InstrumentOff = true;
		}

		#endregion

		#region Methods

        /// <summary>
        /// The DiscoverOperation.Execute will set this property to false if it
        /// was able to communicate with the instrument. If it was able to, then
        /// that means the instrument (or, at least, its IrDA) is now On.
        /// </summary>
        public bool InstrumentOff { get; private set; }

		/// <summary>
		/// Executes a docking station / instrument discovery operation.
		/// </summary>
		/// <returns>
        /// A NothingEvent if nothing is docked containing information about the docking station.
        /// Otherwise, an InstrumentNothingEvent is returned with information regarding both the docking station
        /// and the docked instrument.
        /// </returns>
		public DockingStationEvent Execute()
		{
            // First, always discover the docking station.
            DockingStation ds = Controller.GetDockingStation();

            // Next, discover the instrument, if there's one that's docked.
            // But, we don't bother with the instrument if we're not currently activated on iNet.
			if ( Controller.IsDocked() && !Master.Instance.SwitchService.InitialReadSettingsNeeded )
			{
                Stopwatch stopwatch = Log.TimingBegin("DISCOVER OPERATION - INSTRUMENT");

                // Create the return event.
                InstrumentNothingEvent instrumentNothingEvent = DiscoverInstrument();

                instrumentNothingEvent.DockingStation = ds;

                Log.TimingEnd("DISCOVERY OPERATION - INSTRUMENT",stopwatch);

                return instrumentNothingEvent;
			}
            else // !IsDocked
			{
                NothingEvent nothingEvent = new NothingEvent();

				// Retrieve the docking station's information.
                nothingEvent.DockingStation = ds;

                return nothingEvent;
			}
        }

        private InstrumentNothingEvent DiscoverInstrument()
        {
            // Create the return event.
            InstrumentNothingEvent instrumentNothingEvent = new InstrumentNothingEvent( this );

            InstrumentController instrumentController = SwitchService.CreateInstrumentController();

            try
            {
                // Open the serial port connection needed to communicate with the instrument.
                instrumentController.Initialize( InstrumentController.Mode.Batch );

				// MX4 is the default instrument controller created for MX4 docking stations.
				if ( instrumentController is MX4 )
				{
					// VPRO instrument controller may need created instead depending on type of docked instrument.
					if ( instrumentController.GetInstrumentType() == DeviceType.VPRO )
					{
						// Clean up MX4 controller.
						instrumentController.Dispose();

						// Create and initialize VPRO controller.
						instrumentController = new VPRO();
						instrumentController.Initialize( InstrumentController.Mode.Batch );
					}
				}

                // If we make it through InstrumentController.Initialize without throwing, then
                // we assume the instrument is now on, or at least it's IrDA is.
                InstrumentOff = false;

                Stopwatch sw = new Stopwatch();
                sw.Start();

                // TxRxRetries value is returned by modbuslibrary.dll. It continally increments the value and never resets it back to zero.
                // So, before reading data from the instrument, we get the current value. Farther below, when we're finished reading, we get
                // the value again, and subtract this starting value to determine how many retries occurred during this particular discovery.
                // Getting this starting value also lets us subtract out any of the retries occurring during initializing above.
                int startTxRxRetries = instrumentController.Driver.TxRxRetries;

                // Retrieve the docked instrument.
                instrumentNothingEvent.DockedInstrument = instrumentController.DiscoverDockedInstrument(true);

                // INS-8228 RHP v7.6,  Service accounts need to perform auto-upgrade on instruments even in error/fail state                
                Master.Instance.SwitchService.IsInstrumentInSystemAlarm = instrumentController.IsInstrumentInSystemAlarm;

                sw.Stop();
				int txRxCount = instrumentController.Driver.TxRxCount;
                double txRxCountPerSecond = (double)txRxCount / ( sw.ElapsedMilliseconds / 1000.0 );
                int txRxRetries = instrumentController.Driver.TxRxRetries - startTxRxRetries;
				Log.Debug( string.Format( "Modbus statistics:  stopwatch={0}ms, TxRx={1} ({2}/s), retries={3}",
                   sw.ElapsedMilliseconds, txRxCount, txRxCountPerSecond.ToString("f0"), txRxRetries ) );
            }
            catch (InstrumentSystemAlarmException) // SGF  Nov-23-2009  DSW-355  (DS2 v7.6)
            {
                // If the user docked an instrument in system alarm, then just rethrow up to the service
                // that invoked this discovery and let it deal with it.
                throw;
            }
            catch (HardwareConfigurationException)
            {
                // If user failed to reconfigure the docking station hardware, then just rethrow up to the service
                // that invoked this discovery and let it deal with it.
                throw;
            }
            catch ( Exception e )
            {
                Log.Error( this.GetType().ToString() + ".Execute.DiscoverDockedInstrument", e );

				// ******************************************************************** 
				// See INS-6671 and INS-6682 as to why the second discover was removed. 
                // ********************************************************************

				throw;
            } // end-catch
            finally
            {
                instrumentController.Dispose();
            }

			return instrumentNothingEvent;
        }

        #endregion Methods
    }
}